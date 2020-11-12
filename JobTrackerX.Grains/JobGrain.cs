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

        public async Task<JobEntityState> AddJobAsync(AddJobDto addJobDto)
        {
            if (State.CurrentJobState != JobState.WaitingForActivation)
            {
                throw new InvalidOperationException($"job id duplicate: {this.GetPrimaryKeyLong()}");
            }

            try
            {
                const JobState state = JobState.WaitingToRun;
                var jobId = this.GetPrimaryKeyLong();
                State.JobId = jobId;
                State.ParentJobId = addJobDto.ParentJobId;
                State.CreatedBy = addJobDto.CreatedBy;
                State.Tags = addJobDto.Tags;
                State.Options = addJobDto.Options;
                if (addJobDto.TrackJobCount)
                {
                    State.TrackCountRef = jobId;
                }

                if (addJobDto.ParentJobId.HasValue)
                {
                    var parentGrain = GrainFactory.GetGrain<IJobGrain>(addJobDto.ParentJobId.Value);
                    var parent = await parentGrain.GetJobAsync();
                    if (parent.CurrentJobState == JobState.WaitingForActivation)
                    {
                        throw new JobNotFoundException($"parent job {addJobDto.ParentJobId} not exist");
                    }

                    State.AncestorJobId = parent.AncestorJobId;
                    State.TrackCountRef ??= parent.TrackCountRef;
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

                if (State.TrackCountRef.HasValue)
                {
                    var counter = GrainFactory.GetGrain<IAggregateCounterGrain>(State.TrackCountRef.Value);
                    await counter.AddAsync();
                }
            }
            catch (Exception)
            {
                DeactivateOnIdle();
                throw;
            }

            await WriteStateAsync();
            return State;
        }

        public async Task UpdateJobStateAsync(UpdateJobStateDto dto, bool outerCall = true)
        {
            var beforeCategory = Helper.GetJobStateCategory(State.CurrentJobState);
            if (dto.JobState == JobState.WaitingForActivation)
            {
                throw new Exception(
                    $"cannot set {this.GetPrimaryKeyLong()}'s state to {JobState.WaitingForActivation}");
            }

            if (outerCall && State.CurrentJobState == JobState.WaitingForActivation)
            {
                throw new JobNotFoundException($"job Id not exist: {this.GetPrimaryKeyLong()}");
            }

            if (_options.Value.CommonConfig.BlockStateUpdateAfterFinished
                && Helper.FinishedOrFaultedJobStates.Contains(State.CurrentJobState))
            {
                _logger.LogWarning(
                    $"{this.GetPrimaryKeyLong()} append {dto.JobState} {dto.Message} with {State.CurrentJobState} blocked");
                return;
            }

            try
            {
                State.StateChanges
                    .Add(new StateChangeDto(dto.JobState, dto.Message));
                await UpdateJobStatisticsAsync();
                var currentState = State.CurrentJobState;
                var currentCategory = Helper.GetJobStateCategory(currentState);
                if (State.ParentJobId.HasValue)
                {
                    var parentGrain = GrainFactory.GetGrain<IJobGrain>(State.ParentJobId.Value);
                    if (currentState == JobState.Running)
                    {
                        await parentGrain.OnChildRunningAsync(State.JobId);
                    }

                    if (beforeCategory != currentCategory)
                    {
                        await parentGrain.OnChildStateCategoryChangeAsync(State.JobId, currentCategory);
                    }
                }

                await TryTriggerActionAsync();
            }
            catch (Exception)
            {
                DeactivateOnIdle();
                throw;
            }

            if (outerCall)
            {
                await WriteStateAsync();
            }
        }

        public async Task OnChildStateCategoryChangeAsync(long childJobId, JobStateCategory category)
        {
            var beforeCategory = Helper.GetJobStateCategory(State.CurrentJobState);
            State.ChildrenStatesDic[childJobId] = category;
            await WriteStateAsync();

            var currentState = State.CurrentJobState;
            var currentCategory = Helper.GetJobStateCategory(State.CurrentJobState);
            if (State.ParentJobId.HasValue)
            {
                var parentGrain = GrainFactory.GetGrain<IJobGrain>(State.ParentJobId.Value);
                if (currentState == JobState.Running)
                {
                    await parentGrain.OnChildRunningAsync(State.JobId);
                }

                if (beforeCategory != currentCategory)
                {
                    await parentGrain.OnChildStateCategoryChangeAsync(State.JobId, currentCategory);
                }
            }


            await TryTriggerActionAsync();
            await UpdateJobStatisticsAsync(childJobId, currentState);
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
                        .WaitAndRetryAsync(Constants.GlobalRetryTimes,
                            _ => TimeSpan.FromSeconds(Constants.GlobalRetryWaitSec))
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
                        .WaitAndRetryAsync(Constants.GlobalRetryTimes,
                            _ => TimeSpan.FromSeconds(Constants.GlobalRetryWaitSec))
                        .ExecuteAsync(async () =>
                            await _wrapper.GetRandomStateCheckQueueClient()
                                .ScheduleMessageAsync(msg, config.CheckTime));
                }
            }
        }

        private async Task UpdateJobStatisticsAsync(long? sourceJobId = null, JobState? sourceJobState = null)
        {
            JobState currentJobState;
            if (sourceJobId.HasValue && sourceJobState.HasValue)
            {
                currentJobState = sourceJobState.Value;
            }
            else
            {
                currentJobState = State.CurrentJobState;
            }

            await UpdateJobStatisticsImplAsync(State.JobId, currentJobState, sourceJobId);
        }

        private async Task UpdateJobStatisticsImplAsync(long targetJobId, JobState jobState, long? sourceJobId = null)
        {
            if (jobState == JobState.Running || Helper.FinishedOrFaultedJobStates.Contains(jobState))
            {
                var statisticsGrain = GrainFactory.GetGrain<IJobTreeStatisticsGrain>(targetJobId);
                if (jobState == JobState.Running)
                {
                    await statisticsGrain.SetStartAsync(targetJobId, sourceJobId);
                    return;
                }

                if (Helper.FinishedOrFaultedJobStates.Contains(jobState))
                {
                    await statisticsGrain.SetEndAsync(targetJobId, sourceJobId);
                }
            }
        }

        public Task<JobState> GetCurrentJobStateAsync()
        {
            return Task.FromResult(State.CurrentJobState);
        }

        public async Task SetStateAsync(JobEntityState state)
        {
            State = state;
            await WriteStateAsync();
            if (!State.ParentJobId.HasValue)
            {
                var indexGrain = GrainFactory.GetGrain<IShardJobIndexGrain>(Helper.GetTimeIndex());
                await indexGrain.AddToIndexAsync(new JobIndexInternal(State.JobId, State.JobName, State.CreatedBy,
                    State.Tags));
            }
        }

        public async Task OnChildRunningAsync(long childJobId)
        {
            await UpdateJobStatisticsAsync(childJobId, JobState.Running);
        }

        public async Task<AddJobErrorResult> AddJobFromParentAsync(AddJobDto addJobDto, long ancestorJobId,
            long? trackCountRef)
        {
            if (State.CurrentJobState != JobState.WaitingForActivation)
            {
                throw new InvalidOperationException($"job id duplicate: {this.GetPrimaryKeyLong()}");
            }

            try
            {
                const JobState state = JobState.WaitingToRun;
                State.JobId = this.GetPrimaryKeyLong();
                State.ParentJobId = addJobDto.ParentJobId;
                State.CreatedBy = addJobDto.CreatedBy;
                State.Tags = addJobDto.Tags;
                State.TrackCountRef = trackCountRef;
                State.Options = addJobDto.Options;
                State.AncestorJobId = ancestorJobId;
                State.StateChanges.Add(new StateChangeDto(state));
                State.JobName = addJobDto.JobName;
                State.SourceLink = addJobDto.SourceLink;
                State.ActionConfigs = addJobDto.ActionConfigs;
                State.StateCheckConfigs = addJobDto.StateCheckConfigs;
                await ScheduleStateCheckMessageAsync(State.StateCheckConfigs, State.JobId);
                await WriteStateAsync();
            }
            catch (Exception ex)
            {
                var res = new AddJobErrorResult {JobId = this.GetPrimaryKeyLong(), Error = ex.ToString()};

                DeactivateOnIdle();
                return res;
            }

            return null;
        }

        public async Task BatchInitChildrenAsync(List<long> childrenIdList)
        {
            try
            {
                var counter = State.TrackCountRef.HasValue
                    ? GrainFactory.GetGrain<IAggregateCounterGrain>(State.AncestorJobId)
                    : null;
                if (counter != null)
                {
                    var innerCount = childrenIdList.Count(child => !State.ChildrenStatesDic.ContainsKey(child));

                    if (innerCount > 0)
                    {
                        await counter.AddAsync(innerCount);
                    }
                }

                var beforeCategory = Helper.GetJobStateCategory(State.CurrentJobState);
                foreach (var child in childrenIdList)
                {
                    State.ChildrenStatesDic[child] = JobStateCategory.Pending;
                }

                var currentCategory = Helper.GetJobStateCategory(State.CurrentJobState);
                if (State.ParentJobId.HasValue && beforeCategory != currentCategory)
                {
                    var parentGrain = GrainFactory.GetGrain<IJobGrain>(State.ParentJobId.Value);
                    await parentGrain.OnChildStateCategoryChangeAsync(State.JobId, currentCategory);
                }
            }
            catch (Exception)
            {
                DeactivateOnIdle();
                throw;
            }
        }
    }
}