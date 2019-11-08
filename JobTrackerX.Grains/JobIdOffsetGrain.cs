using JobTrackerX.Entities;
using JobTrackerX.GrainInterfaces;
using Orleans;
using Orleans.Providers;
using System.Threading.Tasks;

namespace JobTrackerX.Grains
{
    [StorageProvider(ProviderName = Constants.JobIdOffsetStoreName)]
    public class JobIdOffsetGrain : Grain<long>, IJobIdOffsetGrain
    {
        public async Task<long> ApplyOffsetAsync(long offset)
        {
            State = offset;
            await WriteStateAsync();
            return State;
        }

        public Task<long> GetCurrentOffsetAsync()
        {
            return Task.FromResult(State);
        }
    }
}