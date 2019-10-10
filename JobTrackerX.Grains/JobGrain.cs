using JobTrackerX.Entities;
using JobTrackerX.Entities.GrainStates;
using JobTrackerX.GrainInterfaces;
using JobTrackerX.SharedLibs;
using Orleans;
using Orleans.Providers;
using System;
using System.Threading.Tasks;

namespace JobTrackerX.Grains
{
    [StorageProvider(ProviderName = Constants.JobEntityStoreName)]
    public class JobGrain : Grain<JobEntityState>, IJobGrain
    {
        public async Task<JobEntityState> AddJobAsync(AddJobDto dto)
        {
            var addJobDto = new AddJobDtoInner(dto);
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
                await indexGrain.AddToIndexAsync(new JobIndexInner(State.JobId, State.JobName, State.CreateBy,
                    State.Tags));
            }

            return State;
        }

        public async Task UpdateJobStateAsync(UpdateJobStateDto dto, bool writeState = true)
        {
            var jobStateDto = new UpdateJobStateDtoInner(dto);
            var previous = jobStateDto.JobState;
            if (Helper.FinishedOrWaitingForChildrenJobStates.Contains(jobStateDto.JobState))
            {
                if (State.PendingChildrenCount > 0)
                {
                    jobStateDto.JobState = JobState.WaitingForChildrenToComplete;
                }
                else
                {
                    jobStateDto.JobState =
                        State.FailedChildrenCount > 0 ? JobState.Faulted : JobState.RanToCompletion;
                }
            }
            if (previous != jobStateDto.JobState)
            {
                jobStateDto.AdditionMsg += $" (sys: {previous} -> {jobStateDto.JobState})";
            }

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

        #region Obsolete

        /*
               [Obsolete]
               public async Task<IList<JobEntity>> GetJobWithChildrenAsync()
               {
                   var jobs = new ConcurrentBag<JobEntity> {State};
                   var childrenIds = await GetAllWithChildrenIdsAsync(State.JobId);
                   var getJobHandler = new ActionBlock<long>(async jobId =>
                   {
                       var jobGrain = GrainFactory.GetGrain<IJobGrain>(jobId);
                       jobs.Add(await jobGrain.GetJobAsync());
                   }, Helper.GetInnerGrainExecutionOptions());
                   foreach (var childJobId in childrenIds)
                   {
                       await getJobHandler.PostToBlockUntilSuccess(childJobId);
                   }

                   getJobHandler.Complete();
                   await getJobHandler.Completion;

                   return jobs.OrderBy(s => s.JobId).ToList();
               }
       */

        /*
                [Obsolete]
                private async Task<IEnumerable<long>> GetAllWithChildrenIdsAsync(long jobId)
                {
                    var jobCount = 1;
                    var result = new ConcurrentBag<long>();
                    this.AsReference<IJobGrain>();
                    var buffer = new BufferBlock<long>(Helper.GetInnerGrainExecutionOptions());
                    var action = new ActionBlock<long>(async id =>
                    {
                        var jobRef = GrainFactory.GetGrain<IDescendantsRefGrain>(id);
                        foreach (var childId in await jobRef.GetChildrenAsync() ?? new List<long>())
                        {
                            result.Add(childId);
                            await buffer.PostToBlockUntilSuccess(childId);
                            Interlocked.Increment(ref jobCount);
                        }

                        Interlocked.Decrement(ref jobCount);
                    }, Helper.GetInnerGrainExecutionOptions());
                    buffer.LinkTo(action);
                    await buffer.PostToBlockUntilSuccess(jobId);
                    while (jobCount != 0)
                    {
                        await Task.Delay(10);
                    }

                    return result.ToList();
                }
        */

        #endregion Obsolete
    }
}