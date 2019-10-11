using JobTrackerX.Entities.GrainStates;
using Orleans;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JobTrackerX.GrainInterfaces
{
    public interface IAggregateJobIndexGrain : IGrainWithStringKey
    {
        Task<List<JobIndexInternal>> QueryAsync(string queryStr);

        Task MergeIntoIndicesAsync(List<JobIndexInternal> indices);
    }
}