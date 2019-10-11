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
        Task AddToIndexAsync(JobIndexInternal jobIndex);

        Task<TableQuerySegment<JobIndexInternal>> FetchWithTokenAsync(TableContinuationToken token, int takeCount = 5000);

        [Obsolete("slow query")]
        Task<List<JobIndexInternal>> QueryAsync(string queryStr);
    }
}