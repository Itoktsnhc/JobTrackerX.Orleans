using System.Threading.Tasks;
using JobTrackerX.Entities.GrainStates;
using Orleans;

namespace JobTrackerX.GrainInterfaces.InMem
{
    public interface IJobTreeStatisticsGrainInMem : IGrainWithIntegerKey
    {
        Task<JobTreeStatisticsState> GetStatisticsAsync();
        Task SetEndAsync(long targetJobId, long? sourceJobId = null);
        Task SetStartAsync(long targetJobId, long? sourceJobId = null);
        Task DeactivateAsync(bool syncState);
    }
}