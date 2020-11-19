using JobTrackerX.Entities.GrainStates;
using Orleans;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JobTrackerX.GrainInterfaces
{
    public interface IAggregateJobIndexGrain : IGrainWithStringKey
    {
        Task MergeIntoIndicesAsync(List<JobIndexInternal> indices);
        Task<int> GetRollingIndexCountAsync();
    }
}