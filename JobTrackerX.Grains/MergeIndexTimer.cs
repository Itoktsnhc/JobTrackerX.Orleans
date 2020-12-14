using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using JobTrackerX.Entities;
using JobTrackerX.Entities.GrainStates;
using JobTrackerX.GrainInterfaces;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;

namespace JobTrackerX.Grains
{
    public class MergeIndexTimer : Grain, IMergeIndexTimer
    {
        private readonly CloudStorageAccount _account;
        private readonly IndexConfig _indexConfig;
        private readonly ILogger<IMergeIndexTimer> _logger;
        private readonly string _tableName;
        private IDisposable _timer;
        private CloudTableClient Client => _account.CreateCloudTableClient();

        public MergeIndexTimer(ILogger<IMergeIndexTimer> logger,
            IOptions<JobTrackerConfig> config, IndexStorageAccountWrapper wrapper)
        {
            _logger = logger;
            _account = wrapper.TableAccount;
            _indexConfig = config.Value.JobIndexConfig;
            _tableName = _indexConfig.TableName;
        }

        private async Task MergeShardingIndexAsync()
        {
            try
            {
                var current = DateTimeOffset.Now;
                var deleteAction = new ActionBlock<ITableEntity>(
                    async entity => await Client.GetTableReference(_tableName)
                        .ExecuteAsync(TableOperation.Delete(entity)),
                    Helper.GetOutOfGrainExecutionOptions());
                var timeIndexSeq =
                    Helper.GetTimeIndexRange(current.AddHours(-_indexConfig.TrackTimeIndexCount),
                        current);
                foreach (var index in timeIndexSeq)
                {
                    var token = new TableContinuationToken();
                    var shardGrain = GrainFactory.GetGrain<IShardJobIndexGrain>(index);
                    var aggregator = GrainFactory.GetGrain<IAggregateJobIndexGrain>(index);
                    var indexResults = new List<JobIndexInternal>();
                    while (token != null && indexResults.Count < _indexConfig.MaxRoundSize)
                    {
                        var result = await shardGrain.FetchWithTokenAsync(token);
                        token = result.ContinuationToken;
                        if (result.Results?.Count > 0)
                        {
                            indexResults.AddRange(result.Results);
                        }
                    }

                    if (indexResults.Count > 0)
                    {
                        await aggregator.MergeIntoIndicesAsync(indexResults);
                    }

                    foreach (var entity in indexResults)
                    {
                        await deleteAction.PostToBlockUntilSuccessAsync(
                            new TableEntity(entity.PartitionKey, entity.RowKey)
                            {
                                ETag = "*"
                            });
                    }

                    _logger.Info($"Merged index to {index}, count: {indexResults.Count}");
                }

                deleteAction.Complete();
                await deleteAction.Completion;
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Error in {nameof(MergeShardingIndexAsync)}");
            }

            SharedData.LastMergeTimePoint = DateTimeOffset.Now;
        }

        public Task KeepAliveAsync()
        {
            _timer ??= RegisterTimer(async _ => await MergeShardingIndexAsync(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));
            return Task.CompletedTask;
        }
    }
}