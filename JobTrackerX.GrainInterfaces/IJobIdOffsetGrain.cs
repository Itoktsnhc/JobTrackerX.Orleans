using Orleans;
using System.Threading.Tasks;

namespace JobTrackerX.GrainInterfaces
{
    public interface IJobIdOffsetGrain : IGrainWithStringKey
    {
        Task<long> ApplyOffsetAsync(long offset);

        Task<long> GetCurrentOffsetAsync();
    }
}