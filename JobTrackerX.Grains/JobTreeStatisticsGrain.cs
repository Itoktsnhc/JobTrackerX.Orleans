using JobTrackerX.Entities;
using JobTrackerX.Entities.GrainStates;
using JobTrackerX.GrainInterfaces;
using Orleans;
using Orleans.Providers;
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

        public async Task SetEndAsync(long targetJobId, long? sourceJobId = null)
        {
            if (State.TreeEnd == null)
            {
                var jobGrain = GrainFactory.GetGrain<IJobGrain>(this.GetPrimaryKeyLong());
                var jobState = await jobGrain.GetCurrentJobStateAsync();
                if (Helper.FinishedOrFaultedJobStates.Contains(jobState))
                {
                    if (sourceJobId.HasValue)
                    {
                        State.TreeEnd = new JobTreeStateItemInternal(sourceJobId.Value);
                    }
                    else
                    {
                        State.TreeEnd = new JobTreeStateItemInternal(targetJobId);
                    }

                    await WriteStateAsync();
                }
            }
        }

        public async Task SetStartAsync(long targetJobId, long? sourceJobId = null)
        {
            if (State.TreeStart == null)
            {
                if (sourceJobId.HasValue)
                {
                    State.TreeStart = new JobTreeStateItemInternal(sourceJobId.Value);
                }
                else
                {
                    State.TreeStart = new JobTreeStateItemInternal(targetJobId);
                }
                await WriteStateAsync();
            }
        }
        
        public async Task SetStateAsync(JobTreeStatisticsState state)
        {
            State = state;
            await WriteStateAsync();
        }
    }
}
