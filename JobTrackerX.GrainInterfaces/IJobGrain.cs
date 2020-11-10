using System.Collections.Generic;
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
        Task OnChildStateCategoryChangeAsync(long childJobId, JobStateCategory category);
        Task UpdateJobOptionsAsync(UpdateJobOptionsDto dto);
        Task SetStateAsync(JobEntityState state);
        Task OnChildRunningAsync(long childJobId);
        Task BatchInitChildrenAsync(List<long> toList);
        Task<AddJobErrorResult> AddJobFromParentAsync(AddJobDto child, long parentAncestorJobId);
    }
}