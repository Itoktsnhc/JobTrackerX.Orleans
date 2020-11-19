using JobTrackerX.Entities;
using JobTrackerX.Entities.GrainStates;
using JobTrackerX.GrainInterfaces;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Providers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace JobTrackerX.Grains
{
    [StorageProvider(ProviderName = Constants.ReadOnlyJobIndexStoreName)]
    public class AggregateJobIndexGrain : Grain<AggregateJobIndexState>, IAggregateJobIndexGrain
    {
        private readonly IndexConfig _indexConfig;

        public AggregateJobIndexGrain(IOptions<JobTrackerConfig> options)
        {
            _indexConfig = options.Value.JobIndexConfig;
        }

        public async Task MergeIntoIndicesAsync(List<JobIndexInternal> indices)
        {
            var current = GrainFactory.GetGrain<IRollingJobIndexGrain>(Helper.GetRollingIndexId(
                this.GetPrimaryKeyString(),
                State.RollingIndexCount));
            await current.MergeIntoIndicesAsync(indices);
            if (await current.GetItemSizeAsync() > _indexConfig.MaxRollingSize)
            {
                State.RollingIndexCount++;
            }

            await WriteStateAsync();
        }

        public Task<int> GetRollingIndexCountAsync()
        {
            return Task.FromResult(State.RollingIndexCount);
        }
    }
}