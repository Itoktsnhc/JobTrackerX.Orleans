using Orleans;
using System.Threading.Tasks;

namespace JobTrackerX.GrainInterfaces
{
    public interface IJobIdGrain : IGrainWithStringKey
    {
        Task<long> GetNewIdAsync();
    }
}