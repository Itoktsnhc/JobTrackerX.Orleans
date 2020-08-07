using JobTrackerX.Entities;
using JobTrackerX.GrainInterfaces;
using Orleans;
using System.Collections.Generic;
using System.Threading.Tasks;
using JobTrackerX.Entities.GrainStates;
using Microsoft.Extensions.Options;
using Orleans.Runtime;

namespace JobTrackerX.Grains
{
    #region ServceBusIdGenerator

    public class ServiceBusServiceBusJobIdGrain : Grain, IServiceBusJobIdGrain
    {
        private readonly ServiceBusWrapper _wrapper;
        private long _idRangeEnd;

        public ServiceBusServiceBusJobIdGrain(ServiceBusWrapper wrapper)
        {
            _wrapper = wrapper;
        }

        private long Current { get; set; }

        public async Task<long> GetNewIdAsync()
        {
            var result = ++Current;
            await CheckAsync();
            return result + await GrainFactory
                .GetGrain<IJobIdOffsetGrain>(Constants.JobIdOffsetGrainDefaultName)
                .GetCurrentOffsetAsync();
        }

        public override async Task OnActivateAsync()
        {
            await base.OnActivateAsync();
            await AcquireNewIdRangeAsync();
        }

        private async Task AcquireNewIdRangeAsync()
        {
            var message = await _wrapper.IdQueueReceiver.ReceiveAsync();
            Current = _wrapper.ScaleSize * (message.SystemProperties.SequenceNumber - 1);
            _idRangeEnd = _wrapper.ScaleSize * message.SystemProperties.SequenceNumber;
        }

        private async Task CheckAsync()
        {
            if (Current + _wrapper.CrashDistance >= _idRangeEnd)
            {
                await AcquireNewIdRangeAsync();
            }
        }
    }

    #endregion

    #region GrainIdGenerator

    public class JobIdGrain : Grain, IJobIdGrain
    {
        private long _idRangeEnd;
        private long Current { get; set; }
        private readonly IPersistentState<JobIdState> _state;
        private readonly IOptions<JobTrackerConfig> _options;

        public JobIdGrain(
            [PersistentState(nameof(JobIdGrain), Constants.JobIdStoreName)]
            IPersistentState<JobIdState> state, IOptions<JobTrackerConfig> options)
        {
            _state = state;
            _options = options;
        }

        public async Task<long> GetNewIdAsync()
        {
            var result = ++Current;
            await CheckAsync();
            return result + await GrainFactory
                .GetGrain<IJobIdOffsetGrain>(Constants.JobIdOffsetGrainDefaultName)
                .GetCurrentOffsetAsync();
        }
        
        public override async Task OnActivateAsync()
        {
            await base.OnActivateAsync();
            await AcquireNewIdRangeAsync();
        }

        private async Task AcquireNewIdRangeAsync()
        {
            _state.State.JobId++;
            await _state.WriteStateAsync();
            Current = _options.Value.IdGeneratorConfig.ScaleSize * (_state.State.JobId - 1);
            _idRangeEnd = _options.Value.IdGeneratorConfig.ScaleSize * _state.State.JobId;
        }

        private async Task CheckAsync()
        {
            if (Current + _options.Value.IdGeneratorConfig.CrashDistance >= _idRangeEnd)
            {
                await AcquireNewIdRangeAsync();
            }
        }
    }

    #endregion
}