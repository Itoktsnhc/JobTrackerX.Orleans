using JobTrackerX.Entities;
using JobTrackerX.Entities.GrainStates;
using JobTrackerX.GrainInterfaces;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Providers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

        public Task<List<JobIndexInner>> QueryAsync(string queryStr)
        {
            var grains = new List<IRollingJobIndexGrain>();
            for (var i = 0; i <= State.RollingIndexCount; i++)
            {
                grains.Add(GrainFactory.GetGrain<IRollingJobIndexGrain>(
                    Helper.GetRollingIndexId(this.GetPrimaryKeyString(), State.RollingIndexCount)));
            }

            return Task.FromResult(grains.AsParallel().WithDegreeOfParallelism(Constants.DefaultDegreeOfParallelism)
                .SelectMany(s => s.QueryAsync(queryStr).Result).ToList());
        }

        public async Task MergeIntoIndicesAsync(List<JobIndexInner> indices)
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
    }
}