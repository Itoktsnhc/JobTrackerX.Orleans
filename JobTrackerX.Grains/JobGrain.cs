using JobTrackerX.Entities;
using JobTrackerX.Entities.GrainStates;
using JobTrackerX.GrainInterfaces;
using JobTrackerX.SharedLibs;
using Microsoft.Azure.ServiceBus;
using Newtonsoft.Json;
using Orleans;
using Orleans.Providers;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobTrackerX.Grains
{
    [StorageProvider(ProviderName = Constants.JobEntityStoreName)]
    public class JobGrain : Grain<JobEntityState>, IJobGrain
    {
        private readonly ServiceBusWrapper _wrapper;
        private readonly ILogger<JobGrain> _logger;
        private readonly IOptions<JobTrackerConfig> _options;

        public JobGrain(ServiceBusWrapper wrapper, IOptions<JobTrackerConfig> options, ILogger<JobGrain> logger)
        {
            _wrapper = wrapper;
            _logger = logger;
            _options = options;
        }

        public async Task<JobEntityState> AddJobAsync(AddJobDto dto)
        {
            if (State.CurrentJobState != JobState.WaitingForActivation)
            {
                throw new InvalidOperationException($"job id duplicate: {this.GetPrimaryKeyLong()}");
            }

            var addJobDto = new AddJobDtoInternal(dto);
            const JobState state = JobState.WaitingToRun;
            var jobId = this.GetPrimaryKeyLong();
            State.JobId = jobId;
            State.ParentJobId = addJobDto.ParentJobId;
            State.CreatedBy = addJobDto.CreatedBy;
            State.Tags = addJobDto.Tags;
            State.Options = addJobDto.Options;
            if (addJobDto.ParentJobId.HasValue)
            {
                var parentGrain = GrainFactory.GetGrain<IJobGrain>(addJobDto.ParentJobId.Value);
                var parent = await parentGrain.GetJobAsync();
                if (parent.CurrentJobState == JobState.WaitingForActivation)
                {
                    throw new JobNotFoundException($"parent job {addJobDto.ParentJobId} not exist");
                }

                State.AncestorJobId = parent.AncestorJobId;
                await UpdateJobStateAsync(new UpdateJobStateDto(JobState.WaitingToRun), false);
            }
            else
            {
                State.AncestorJobId = jobId;
                State.StateChanges.Add(new StateChangeDto(state));
            }

            var ancestorRefGrain = GrainFactory.GetGrain<IDescendantsRefGrain>(State.AncestorJobId);
            await ancestorRefGrain.AttachToChildrenAsync(jobId);
            State.JobName = addJobDto.JobName;
            State.SourceLink = addJobDto.SourceLink;
            State.ActionConfigs = addJobDto.ActionConfigs;
            State.StateCheckConfigs = addJobDto.StateCheckConfigs;
            await ScheduleStateCheckMessageAsync(State.StateCheckConfigs, State.JobId);
            if (!State.ParentJobId.HasValue)
            {
                var indexGrain = GrainFactory.GetGrain<IShardJobIndexGrain>(Helper.GetTimeIndex());
                await indexGrain.AddToIndexAsync(new JobIndexInternal(State.JobId, State.JobName, State.CreatedBy,
                    State.Tags));
            }
            await WriteStateAsync();
            return State;
        }

        public async Task UpdateJobStateAsync(UpdateJobStateDto dto, bool outerCall = true)
        {
            if (dto.JobState == JobState.WaitingForActivation)
            {
                throw new Exception($"cannot set {this.GetPrimaryKeyLong()}'s state to {JobState.WaitingForActivation}");
            }
            if (outerCall && State.CurrentJobState == JobState.WaitingForActivation)
            {
                throw new JobNotFoundException($"job Id not exist: {this.GetPrimaryKeyLong()}");
            }

            var jobStateDto = new UpdateJobStateDtoInternal(dto);
            if (_options.Value.CommonConfig.BlockStateUpdateAfterFinished
                && Helper.FinishedOrFaultedJobStates.Contains(State.CurrentJobState))
            {
                _logger.LogWarning($"{this.GetPrimaryKeyLong()} append {dto.JobState} {dto.Message} with {State.CurrentJobState} blocked");
                return;
            }
            State.StateChanges
                .Add(new StateChangeDto(jobStateDto.JobState, jobStateDto.AdditionMsg));
            if (State.ParentJobId.HasValue)
            {
                var summaryStateCategory = Helper.GetJobStateCategory(State.CurrentJobState);
                var parentGrain = GrainFactory.GetGrain<IJobGrain>(State.ParentJobId.Value);
                await parentGrain.OnChildStateChangeAsync(State.JobId, summaryStateCategory);
            }

            await TryTriggerActionAsync();

            if (outerCall)
            {
                await UpdateJobTreeStatisticsAsync();
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
            await TryTriggerActionAsync();
        }

        public async Task UpdateJobOptionsAsync(UpdateJobOptionsDto dto)
        {
            if (State.CurrentJobState == JobState.WaitingForActivation)
            {
                throw new JobNotFoundException($"job Id not exist: {this.GetPrimaryKeyLong()}");
            }
            State.Options = dto.Options;
            await WriteStateAsync();
        }

        public async Task<JobEntityState> GetJobAsync(bool ignoreNotExist = false)
        {
            if (State.CurrentJobState == JobState.WaitingForActivation)
            {
                if (ignoreNotExist)
                {
                    return null;
                }
                else
                {
                    throw new JobNotFoundException($"job Id not exist: {this.GetPrimaryKeyLong()}");
                }
            }
            return await Task.FromResult(State);
        }

        private async Task TryTriggerActionAsync()
        {
            if (State.ActionConfigs?.Any() == true)
            {
                var state = State.CurrentJobState;
                var targets = State.ActionConfigs.Where(
                    s => s.JobStateFilters?.Any() == true
                    && s.JobStateFilters.Contains(state)
                    && s.ActionWrapper != null);
                foreach (var target in targets)
                {
                    var actionDto = new ActionMessageDto()
                    {
                        ActionConfig = target,
                        JobId = State.JobId,
                        JobState = State.CurrentJobState
                    };
                    var msg = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(actionDto)));
                    await Policy.Handle<Exception>()
                        .WaitAndRetryAsync(Constants.GlobalRetryTimes, _ => TimeSpan.FromSeconds(Constants.GlobalRetryWaitSec))
                        .ExecuteAsync(async () => await _wrapper.GetRandomActionQueueClient().SendAsync(msg));
                }
            }
        }

        private async Task ScheduleStateCheckMessageAsync(List<StateCheckConfig> configs, long jobId)
        {
            if (configs?.Any() == true)
            {
                foreach (var config in configs)
                {
                    var stateCheckDto = new StateCheckMessageDto()
                    {
                        StateCheckConfig = config,
                        JobId = jobId
                    };
                    var msg = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(stateCheckDto)));
                    await Policy.Handle<Exception>()
                        .WaitAndRetryAsync(Constants.GlobalRetryTimes, _ => TimeSpan.FromSeconds(Constants.GlobalRetryWaitSec))
                        .ExecuteAsync(async () =>
                            await _wrapper.GetRandomStateCheckQueueClient().ScheduleMessageAsync(msg, config.CheckTime));
                }
            }
        }

        private async Task UpdateJobTreeStatisticsAsync()
        {
            var currentJobState = State.CurrentJobState;
            if (currentJobState == JobState.Running || Helper.FinishedOrFaultedJobStates.Contains(currentJobState))
            {
                var statisticsGrain = GrainFactory.GetGrain<IJobTreeStatisticsGrain>(State.AncestorJobId);
                if (currentJobState == JobState.Running)
                {
                    await statisticsGrain.SetStartAsync(State.JobId, State.StartTime);
                    return;
                }
                if (Helper.FinishedOrFaultedJobStates.Contains(currentJobState))
                {
                    await statisticsGrain.SetEndAsync(State.JobId, State.EndTime);
                    return;
                }
            }
        }

        public Task<JobState> GetCurrentJobStateAsync()
        {
            return Task.FromResult(State.CurrentJobState);
        }
    }
}