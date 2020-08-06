using System.Threading.Tasks;
using JobTrackerX.Entities;
using JobTrackerX.Entities.GrainStates;
using JobTrackerX.GrainInterfaces;
using JobTrackerX.GrainInterfaces.InMem;
using Orleans;

namespace JobTrackerX.Grains.InMem
{
    [BufferInMem]
    public class JobTreeStatisticsGrainInMem : Grain, IJobTreeStatisticsGrainInMem
    {
        private readonly JobTreeStatisticsState _state = new JobTreeStatisticsState();

        public override async Task OnActivateAsync()
        {
            await base.OnActivateAsync();
            _state.JobId = this.GetPrimaryKeyLong();
        }

        public Task<JobTreeStatisticsState> GetStatisticsAsync()
        {
            return Task.FromResult(_state);
        }

        public async Task SetEndAsync(long targetJobId, long? sourceJobId = null)
        {
            if (_state.TreeEnd == null)
            {
                var jobGrain = GrainFactory.GetGrain<IJobGrainInMem>(this.GetPrimaryKeyLong());
                var jobState = await jobGrain.GetCurrentJobStateAsync();
                if (Helper.FinishedOrFaultedJobStates.Contains(jobState))
                {
                    _state.TreeEnd = sourceJobId.HasValue
                        ? new JobTreeStateItemInternal(sourceJobId.Value)
                        : new JobTreeStateItemInternal(targetJobId);
                }
            }
        }

        public Task SetStartAsync(long targetJobId, long? sourceJobId = null)
        {
            if (_state.TreeStart == null)
            {
                _state.TreeStart = sourceJobId.HasValue
                    ? new JobTreeStateItemInternal(sourceJobId.Value)
                    : new JobTreeStateItemInternal(targetJobId);
            }

            return Task.CompletedTask;
        }

        public async Task DeactivateAsync(bool syncState)
        {
            if (syncState)
            {
                await GrainFactory.GetGrain<IJobTreeStatisticsGrain>(this.GetPrimaryKeyLong()).SetStateAsync(_state);
            }

            DeactivateOnIdle();
        }
    }
}