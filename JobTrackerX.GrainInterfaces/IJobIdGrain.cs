using Orleans;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JobTrackerX.GrainInterfaces
{
    public interface IJobIdGrain : IGrainWithStringKey
    {
        Task<long> GetNewIdAsync();

        Task<IEnumerable<long>> GetNewIdsAsync(int count);
    }
}