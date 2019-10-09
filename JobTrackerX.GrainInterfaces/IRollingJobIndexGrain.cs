using JobTrackerX.Entities.GrainStates;
using Orleans;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JobTrackerX.GrainInterfaces
{
    public interface IRollingJobIndexGrain : IGrainWithStringKey
    {
        Task<List<JobIndexInner>> QueryAsync(string queryStr);

        Task MergeIntoIndicesAsync(List<JobIndexInner> indices);

        Task<long> GetItemSizeAsync();
    }
}