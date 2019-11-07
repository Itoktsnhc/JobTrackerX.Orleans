using JobTrackerX.Entities;
using JobTrackerX.Entities.GrainStates;
using JobTrackerX.GrainInterfaces;
using JobTrackerX.SharedLibs;
using Orleans;
using Orleans.Providers;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace JobTrackerX.Grains
{
    [StorageProvider(ProviderName = Constants.JobEntityStoreName)]
    public class JobGrain : Grain<JobEntityState>, IJobGrain
    {
        public async Task<JobEntityState> AddJobAsync(AddJobDto dto)
        {
            var addJobDto = new AddJobDtoInternal(dto);
            if (State.CurrentJobState != JobState.WaitingForActivation)
            {
                throw new InvalidOperationException($"job id duplicate: {this.GetPrimaryKeyLong()}");
            }

            const JobState state = JobState.WaitingToRun;
            var jobId = this.GetPrimaryKeyLong();
            State.JobId = jobId;
            State.ParentJobId = addJobDto.ParentJobId;
            State.CreateBy = addJobDto.CreatedBy;
            State.Tags = addJobDto.Tags;
            State.Options = addJobDto.Options;
            if (addJobDto.ParentJobId.HasValue)
            {
                var parentGrain = GrainFactory.GetGrain<IJobGrain>(addJobDto.ParentJobId.Value);
                var parent = await parentGrain.GetJobAsync();
                if (parent.CurrentJobState == JobState.WaitingForActivation)
                {
                    throw new InvalidOperationException($"parent job {addJobDto.ParentJobId} not exist");
                }

                State.AncestorJobId = parent.AncestorJobId;
                await UpdateJobStateAsync(new UpdateJobStateDto(JobState.WaitingToRun), false);
            }
            else
            {
                State.AncestorJobId = jobId;
            }

            var ancestorRefGrain = GrainFactory.GetGrain<IDescendantsRefGrain>(State.AncestorJobId);
            await ancestorRefGrain.AttachToChildrenAsync(jobId);
            State.StateChanges.Add(new StateChangeDto(state));
            State.JobName = addJobDto.JobName;
            await WriteStateAsync();
            if (!State.ParentJobId.HasValue)
            {
                var indexGrain = GrainFactory.GetGrain<IShardJobIndexGrain>(Helper.GetTimeIndex());
                await indexGrain.AddToIndexAsync(new JobIndexInternal(State.JobId, State.JobName, State.CreateBy,
                    State.Tags));
            }

            return State;
        }

        public async Task UpdateJobStateAsync(UpdateJobStateDto dto, bool writeState = true)
        {
            var jobStateDto = new UpdateJobStateDtoInternal(dto);
            State.StateChanges
                .Add(new StateChangeDto(jobStateDto.JobState, jobStateDto.AdditionMsg));

            if (State.ParentJobId.HasValue)
            {
                var summaryStateCategory = Helper.GetJobStateCategory(State.CurrentJobState);
                var parentGrain = GrainFactory.GetGrain<IJobGrain>(State.ParentJobId.Value);
                await parentGrain.OnChildStateChangeAsync(State.JobId, summaryStateCategory);
            }

            if (writeState)
            {
                await WriteStateAsync();
            }
        }

        public async Task OnChildStateChangeAsync(long childJobId, JobStateCategory state)
        {
            State.ChildrenStatesDic[childJobId] = state;
            if (Helper.FinishedOrFaultedJobStates.Contains(State.CurrentJobState))
            {
                State.StateChanges
                    .Add(new StateChangeDto(JobState.RanToCompletion, $"(sys: fired by child {childJobId})"));
            }
            if (State.ParentJobId.HasValue)
            {
                var parentGrain = GrainFactory.GetGrain<IJobGrain>(State.ParentJobId.Value);
                var category = Helper.GetJobStateCategory(State.CurrentJobState);
                await parentGrain.OnChildStateChangeAsync(State.JobId, category);
            }

            await WriteStateAsync();
        }

        public async Task UpdateJobOptionsAsync(UpdateJobOptionsDto dto)
        {
            if (State.CurrentJobState == JobState.WaitingForActivation)
            {
                throw new Exception($"job Id not exist: {this.GetPrimaryKeyLong()}");
            }
            State.Options = dto.Options;
            await WriteStateAsync();
        }

        public async Task<JobEntityState> GetJobAsync()
        {
            if (State.CurrentJobState == JobState.WaitingForActivation)
            {
                throw new Exception($"job Id not exist: {this.GetPrimaryKeyLong()}");
            }
            return await Task.FromResult(State);
        }
    }
}