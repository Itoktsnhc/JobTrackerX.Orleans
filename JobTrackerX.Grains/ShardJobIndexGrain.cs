using JobTrackerX.Entities;
using JobTrackerX.Entities.GrainStates;
using JobTrackerX.GrainInterfaces;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Concurrency;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos.Table;

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
            _account = wrapper.TableAccount;
            _indexConfig = options.Value.JobIndexConfig;
        }

        private CloudTableClient Client => _account.CreateCloudTableClient();

        public async Task AddToIndexAsync(JobIndexInternal jobIndex)
        {
            jobIndex.PartitionKey = Helper.GetShardIndexPartitionKeyName(jobIndex, this.GetPrimaryKeyString());
            jobIndex.RowKey = jobIndex.JobId.ToString();
            var table = Client.GetTableReference(_tableName);
            await table.ExecuteAsync(TableOperation.Insert(jobIndex));
        }

        public async Task<TableQuerySegment<JobIndexInternal>> FetchWithTokenAsync(TableContinuationToken token,
            int takeCount = 1000)
        {
            var table = Client.GetTableReference(_tableName);
            var query = new TableQuery<JobIndexInternal>()
                .Where(TableQuery.CombineFilters(
                    TableQuery.GenerateFilterCondition("PartitionKey",
                    QueryComparisons.LessThanOrEqual, this.GetPrimaryKeyString() + "."),
                    TableOperators.And,
                    TableQuery.GenerateFilterCondition("PartitionKey",
                        QueryComparisons.GreaterThan, this.GetPrimaryKeyString() + "-")));
            if (takeCount > 1000)
            {
                takeCount = 1000;
            }
            query.TakeCount = takeCount;
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