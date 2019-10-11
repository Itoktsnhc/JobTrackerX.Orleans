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

        public async Task<List<JobIndexInternal>> QueryAsync(string queryStr)
        {
            var queryResult = new ConcurrentBag<JobIndexInternal>();
            var queryActionBlock = new ActionBlock<int>(async index =>
            {
                var grain = GrainFactory.GetGrain<IRollingJobIndexGrain>(Helper.GetRollingIndexId(this.GetPrimaryKeyString(), index));
                foreach (var item in await grain.QueryAsync(queryStr))
                {
                    queryResult.Add(item);
                }
            }, Helper.GetGrainInternalExecutionOptions());

            for (var i = 0; i <= State.RollingIndexCount; i++)
            {
                await queryActionBlock.PostToBlockUntilSuccessAsync(i);
            }

            queryActionBlock.Complete();
            await queryActionBlock.Completion;
            return queryResult.ToList();
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
    }
}