using JobTrackerX.Entities;
using JobTrackerX.Entities.GrainStates;
using JobTrackerX.GrainInterfaces;
using Orleans;
using Orleans.Providers;
using System;
using System.Threading.Tasks;

namespace JobTrackerX.Grains
{
    [StorageProvider(ProviderName = Constants.JobEntityStoreName)]
    public class JobTreeStatisticsGrain : Grain<JobTreeStatisticsState>, IJobTreeStatisticsGrain
    {
        public override async Task OnActivateAsync()
        {
            await base.OnActivateAsync();
            State.JobId = this.GetPrimaryKeyLong();
        }

        public Task<JobTreeStatisticsState> GetStatisticsAsync()
        {
            return Task.FromResult(State);
        }

        public async Task SetEndAsync(long sourceJobId, DateTimeOffset? timePoint = null)
        {
            if (State.TreeEnd == null)
            {
                var jobGrain = GrainFactory.GetGrain<IJobGrain>(this.GetPrimaryKeyLong());
                var jobState = await jobGrain.GetCurrentJobStateAsync();
                if (Helper.FinishedOrFaultedJobStates.Contains(jobState))
                {
                    State.TreeEnd = new JobTreeStateItemInternal(sourceJobId);
                    if (timePoint.HasValue)
                    {
                        State.TreeEnd.TimePoint = timePoint.Value;
                    }
                    await WriteStateAsync();
                }
            }
        }

        public async Task SetStartAsync(long sourceJobId, DateTimeOffset? timePoint = null)
        {
            if (State.TreeStart == null)
            {
                State.TreeStart = new JobTreeStateItemInternal(sourceJobId);
                if (timePoint.HasValue)
                {
                    State.TreeStart.TimePoint = timePoint.Value;
                }
                await WriteStateAsync();
            }
        }
    }
}
