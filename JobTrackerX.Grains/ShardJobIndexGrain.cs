using JobTrackerX.Entities;
using JobTrackerX.Entities.GrainStates;
using JobTrackerX.GrainInterfaces;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Orleans;
using Orleans.Concurrency;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;

namespace JobTrackerX.Grains
{
    [StatelessWorker(50)]
    public class ShardJobIndexGrainWithTable : Grain, IShardJobIndexGrain
    {
        private readonly CloudStorageAccount _account;
        private readonly IndexConfig _indexConfig;
        private string _tableName;

        public ShardJobIndexGrainWithTable(IndexStorageAccountWrapper wrapper, IOptions<JobTrackerConfig> options)
        {
            _account = wrapper.Account;
            _indexConfig = options.Value.JobIndexConfig;
        }

        private CloudTableClient Client => _account.CreateCloudTableClient();

        public async Task AddToIndexAsync(JobIndexInner jobIndex)
        {
            jobIndex.PartitionKey = Helper.GetShardIndexPartitionKeyName(jobIndex, this.GetPrimaryKeyString());
            jobIndex.RowKey = jobIndex.JobId.ToString();
            var table = Client.GetTableReference(_tableName);
            await table.ExecuteAsync(TableOperation.Insert(jobIndex));
        }

        [Obsolete("slow query")]
        public async Task<List<JobIndexInner>> QueryAsync(string queryStr)
        {
            var token = new TableContinuationToken();
            var result = new List<JobIndexInner>();
            while (token != null)
            {
                var res = await FetchWithTokenAsync(token);
                result.AddRange(res.Results);
                token = res.ContinuationToken;
            }

            return string.IsNullOrEmpty(queryStr) ? result : result.AsQueryable().Where(queryStr).ToList();
        }

        public async Task<TableQuerySegment<JobIndexInner>> FetchWithTokenAsync(TableContinuationToken token,
            int takeCount = 5000)
        {
            var table = Client.GetTableReference(_tableName);
            var query = new TableQuery<JobIndexInner>
            {
                FilterString =
                    $"PartitionKey lt '{this.GetPrimaryKeyString()}.' and PartitionKey ge '{this.GetPrimaryKeyString()}-'",
                TakeCount = takeCount
            };
            return await table.ExecuteQuerySegmentedAsync(query, token);
        }

        public override async Task OnActivateAsync()
        {
            await base.OnActivateAsync();
            _tableName = _indexConfig.TableName;
            await Client.GetTableReference(_tableName).CreateIfNotExistsAsync();
        }
    }
}