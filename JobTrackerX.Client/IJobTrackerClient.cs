using JobTrackerX.SharedLibs;
using System.Threading.Tasks;

namespace JobTrackerX.Client
{
    public interface IJobTrackerClient
    {
        Task<JobEntity> CreateNewJobAsync(AddJobDto dto);

        Task UpdateJobStatesAsync(long id, UpdateJobStateDto dto);

        Task UpdateJobOptionsAsync(long id, UpdateJobOptionsDto dto);

        Task<JobEntity> GetJobEntityAsync(long jobId);

        Task<ReturnQueryIndexDto> QueryJobIndexAsync(QueryJobIndexDto dto);
    }
}