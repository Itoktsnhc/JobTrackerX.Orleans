using JobTrackerX.Entities.GrainStates;
using JobTrackerX.SharedLibs;
using Orleans;
using System.Threading.Tasks;

namespace JobTrackerX.GrainInterfaces
{
    public interface IJobGrain : IGrainWithIntegerKey
    {
        Task<JobEntityState> AddJobAsync(AddJobDto addJobDto);
        Task<JobEntityState> GetJobAsync(bool ignoreNotExist = false);
        Task<JobState> GetCurrentJobStateAsync();
        Task UpdateJobStateAsync(UpdateJobStateDto jobStateDto, bool outerCall = true);
        Task OnChildStateChangeAsync(long childJobId, JobState childState);
        Task UpdateJobOptionsAsync(UpdateJobOptionsDto dto);
        Task SetStateAsync(JobEntityState state);
    }
}