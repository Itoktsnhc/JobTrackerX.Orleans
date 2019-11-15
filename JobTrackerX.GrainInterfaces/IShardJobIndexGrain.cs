using JobTrackerX.Entities.GrainStates;
using Microsoft.Azure.Cosmos.Table;
using Orleans;
using System.Threading.Tasks;

namespace JobTrackerX.GrainInterfaces
{
    public interface IShardJobIndexGrain : IGrainWithStringKey
    {
        Task AddToIndexAsync(JobIndexInternal jobIndex);

        Task<TableQuerySegment<JobIndexInternal>> FetchWithTokenAsync(TableContinuationToken token, int takeCount = 5000);
    }
}