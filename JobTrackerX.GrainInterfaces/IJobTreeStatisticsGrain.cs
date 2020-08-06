using JobTrackerX.Entities.GrainStates;
using Orleans;
using System.Threading.Tasks;

namespace JobTrackerX.GrainInterfaces
{
    public interface IJobTreeStatisticsGrain : IGrainWithIntegerKey
    {
        Task<JobTreeStatisticsState> GetStatisticsAsync();
        Task SetStartAsync(long targetJobId, long? sourceJobId = null);
        Task SetEndAsync(long targetJobId, long? sourceJobId = null);
        Task SetStateAsync(JobTreeStatisticsState state);
    }
}
