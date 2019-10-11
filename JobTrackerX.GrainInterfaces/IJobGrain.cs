using JobTrackerX.Entities.GrainStates;
using JobTrackerX.SharedLibs;
using Orleans;
using System.Threading.Tasks;

namespace JobTrackerX.GrainInterfaces
{
    public interface IJobGrain : IGrainWithIntegerKey
    {
        Task<JobEntityState> AddJobAsync(AddJobDto addJobDto);

        Task<JobEntityState> GetJobEntityAsync();

        Task UpdateJobStateAsync(UpdateJobStateDto jobStateDto, bool writeState = true);

        Task OnChildStateChangeAsync(long childJobId, JobStateCategory state);

        Task UpdateJobOptionsAsync(UpdateJobOptionsDto dto);
    }
}