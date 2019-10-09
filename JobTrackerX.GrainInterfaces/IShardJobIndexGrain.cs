using JobTrackerX.Entities.GrainStates;
using Microsoft.WindowsAzure.Storage.Table;
using Orleans;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JobTrackerX.GrainInterfaces
{
    public interface IShardJobIndexGrain : IGrainWithStringKey
    {
        Task AddToIndexAsync(JobIndexInner jobIndex);

        Task<TableQuerySegment<JobIndexInner>> FetchWithTokenAsync(TableContinuationToken token, int takeCount = 5000);

        [Obsolete("slow query")]
        Task<List<JobIndexInner>> QueryAsync(string queryStr);
    }
}