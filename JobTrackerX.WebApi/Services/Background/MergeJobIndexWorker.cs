using JobTrackerX.Entities;
using JobTrackerX.Entities.GrainStates;
using JobTrackerX.GrainInterfaces;
using JobTrackerX.WebApi.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Orleans;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace JobTrackerX.WebApi.Services.Background
{
    public class MergeJobIndexWorker : BackgroundService
    {
        private readonly CloudStorageAccount _account;
        private readonly IClusterClient _client;
        private readonly IndexConfig _indexConfig;
        private readonly ILogger<MergeJobIndexWorker> _logger;
        private readonly string _tableName;

        public MergeJobIndexWorker(IClusterClient client, ILogger<MergeJobIndexWorker> logger,
            IOptions<JobTrackerConfig> config, IndexStorageAccountWrapper wrapper)
        {
            _client = client;
            _account = wrapper.Account;
            _logger = logger;
            _indexConfig = config.Value.JobIndexConfig;
            _tableName = _indexConfig.TableName;
        }

        private CloudTableClient Client => _account.CreateCloudTableClient();

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var current = DateTimeOffset.Now;
                    var deleteAction = new ActionBlock<TableEntity>(
                        entity => Client.GetTableReference(_tableName).ExecuteAsync(TableOperation.Delete(entity)),
                        Helper.GetOutOfGrainExecutionOptions());
                    var timeIndexSeq =
                        Helper.GetTimeIndexRange(current.AddHours(-_indexConfig.TrackTimeIndexCount),
                            current);
                    foreach (var index in timeIndexSeq)
                    {
                        var token = new TableContinuationToken();
                        var shardGrain = _client.GetGrain<IShardJobIndexGrain>(index);
                        var aggregator = _client.GetGrain<IAggregateJobIndexGrain>(index);
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
                    }

                    deleteAction.Complete();
                    await deleteAction.Completion;
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Error in {nameof(MergeJobIndexWorker)}");
                }
                SharedData.LastMergeTimePoint = DateTimeOffset.Now;
                await Task.Delay(_indexConfig.IndexMergeInterval, stoppingToken);
            }
        }
    }
}