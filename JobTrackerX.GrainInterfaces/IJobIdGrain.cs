using Orleans;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JobTrackerX.GrainInterfaces
{
    public interface IServiceBusJobIdGrain : IGrainWithStringKey
    {
        Task<long> GetNewIdAsync();
    }

    public interface IJobIdGrain : IGrainWithStringKey
    {
        Task<long> GetNewIdAsync();
    }
}