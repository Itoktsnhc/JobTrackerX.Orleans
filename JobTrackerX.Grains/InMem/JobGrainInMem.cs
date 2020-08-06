using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JobTrackerX.Entities;
using JobTrackerX.Entities.GrainStates;
using JobTrackerX.GrainInterfaces;
using JobTrackerX.GrainInterfaces.InMem;
using JobTrackerX.SharedLibs;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Orleans;
using Polly;

namespace JobTrackerX.Grains.InMem
{
    [BufferInMem]
    public class JobGrainInMem : Grain, IJobGrainInMem
    {
        private readonly JobEntityState _state = new JobEntityState();
        private readonly ServiceBusWrapper _wrapper;
        private readonly ILogger<JobGrainInMem> _logger;
        private readonly IOptions<JobTrackerConfig> _options;

        public JobGrainInMem(ServiceBusWrapper wrapper, IOptions<JobTrackerConfig> options,
            ILogger<JobGrainInMem> logger)
        {
            _wrapper = wrapper;
            _logger = logger;
            _options = options;
        }

        public async Task<JobEntityState> AddJobAsync(AddJobDto addJobDto)
        {
            if (_state.CurrentJobState != JobState.WaitingForActivation)
            {
                throw new InvalidOperationException($"job id duplicate: {this.GetPrimaryKeyLong()}");
            }

            const JobState state = JobState.WaitingToRun;
            var jobId = this.GetPrimaryKeyLong();
            _state.JobId = jobId;
            _state.ParentJobId = addJobDto.ParentJobId;
            _state.CreatedBy = addJobDto.CreatedBy;
            _state.Tags = addJobDto.Tags;
            _state.Options = addJobDto.Options;
            if (addJobDto.ParentJobId.HasValue)
            {
                var parentGrain = GrainFactory.GetGrain<IJobGrainInMem>(addJobDto.ParentJobId.Value);
                var parent = await parentGrain.GetJobAsync();
                if (parent.CurrentJobState == JobState.WaitingForActivation)
                {
                    throw new JobNotFoundException($"parent job {addJobDto.ParentJobId} not exist");
                }

                _state.AncestorJobId = parent.AncestorJobId;
                await UpdateJobStateAsync(new UpdateJobStateDto(JobState.WaitingToRun), false);
            }
            else
            {
                _state.AncestorJobId = jobId;
                _state.StateChanges.Add(new StateChangeDto(state));
            }

            var ancestorRefGrain = GrainFactory.GetGrain<IDescendantsRefGrainInMem>(_state.AncestorJobId);
            await ancestorRefGrain.AttachToChildrenAsync(jobId);
            _state.JobName = addJobDto.JobName;
            _state.SourceLink = addJobDto.SourceLink;
            _state.ActionConfigs = addJobDto.ActionConfigs;
            _state.StateCheckConfigs = addJobDto.StateCheckConfigs;
            await ScheduleStateCheckMessageAsync(_state.StateCheckConfigs, _state.JobId);
            return _state;
        }

        public async Task UpdateJobStateAsync(UpdateJobStateDto dto, bool outerCall = true)
        {
            if (dto.JobState == JobState.WaitingForActivation)
            {
                throw new Exception(
                    $"cannot set {this.GetPrimaryKeyLong()}'s state to {JobState.WaitingForActivation}");
            }

            if (outerCall && _state.CurrentJobState == JobState.WaitingForActivation)
            {
                throw new JobNotFoundException($"job Id not exist: {this.GetPrimaryKeyLong()}");
            }

            if (_options.Value.CommonConfig.BlockStateUpdateAfterFinished
                && Helper.FinishedOrFaultedJobStates.Contains(_state.CurrentJobState))
            {
                _logger.LogWarning(
                    $"{this.GetPrimaryKeyLong()} append {dto.JobState} {dto.Message} with {_state.CurrentJobState} blocked");
                return;
            }

            _state.StateChanges
                .Add(new StateChangeDto(dto.JobState, dto.Message));
            await UpdateJobStatisticsAsync();
            if (_state.ParentJobId.HasValue)
            {
                var parentGrain = GrainFactory.GetGrain<IJobGrainInMem>(_state.ParentJobId.Value);
                await parentGrain.OnChildStateChangeAsync(_state.JobId, _state.CurrentJobState);
            }

            await TryTriggerActionAsync();
        }

        public async Task OnChildStateChangeAsync(long childJobId, JobState childJobState)
        {
            _state.ChildrenStatesDic[childJobId] = Helper.GetJobStateCategory(childJobState);

            if (_state.ParentJobId.HasValue)
            {
                var parentGrain = GrainFactory.GetGrain<IJobGrainInMem>(_state.ParentJobId.Value);
                await parentGrain.OnChildStateChangeAsync(_state.JobId, _state.CurrentJobState);
            }


            await TryTriggerActionAsync();
            await UpdateJobStatisticsAsync(childJobId, childJobState);
        }

        public Task UpdateJobOptionsAsync(UpdateJobOptionsDto dto)
        {
            if (_state.CurrentJobState == JobState.WaitingForActivation)
            {
                throw new JobNotFoundException($"job Id not exist: {this.GetPrimaryKeyLong()}");
            }

            _state.Options = dto.Options;
            return Task.CompletedTask;
        }

        public async Task DeactivateAsync(bool syncState)
        {
            if (syncState && _state.CurrentJobState != JobState.WaitingForActivation)
            {
                await GrainFactory.GetGrain<IJobGrain>(this.GetPrimaryKeyLong()).SetStateAsync(_state);
            }

            DeactivateOnIdle();
        }

        public async Task<JobEntityState> GetJobAsync(bool ignoreNotExist = false)
        {
            if (_state.CurrentJobState == JobState.WaitingForActivation)
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

            return await Task.FromResult(_state);
        }

        private async Task TryTriggerActionAsync()
        {
            if (_state.ActionConfigs?.Any() == true)
            {
                var state = _state.CurrentJobState;
                var targets = _state.ActionConfigs.Where(
                    s => s.JobStateFilters?.Any() == true
                         && s.JobStateFilters.Contains(state)
                         && s.ActionWrapper != null);
                foreach (var target in targets)
                {
                    var actionDto = new ActionMessageDto()
                    {
                        ActionConfig = target,
                        JobId = _state.JobId,
                        JobState = _state.CurrentJobState
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
                currentJobState = _state.CurrentJobState;
            }

            await UpdateJobStatisticsImplAsync(_state.JobId, currentJobState, sourceJobId);
        }

        private async Task UpdateJobStatisticsImplAsync(long targetJobId, JobState jobState, long? sourceJobId = null)
        {
            if (jobState == JobState.Running || Helper.FinishedOrFaultedJobStates.Contains(jobState))
            {
                var statisticsGrain = GrainFactory.GetGrain<IJobTreeStatisticsGrainInMem>(targetJobId);
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
            return Task.FromResult(_state.CurrentJobState);
        }
    }
}