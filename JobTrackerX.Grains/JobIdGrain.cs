using JobTrackerX.Entities;
using JobTrackerX.GrainInterfaces;
using Orleans;
using System.Threading.Tasks;
using JobTrackerX.Entities.GrainStates;
using Microsoft.Extensions.Options;
using Orleans.Runtime;

namespace JobTrackerX.Grains
{
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