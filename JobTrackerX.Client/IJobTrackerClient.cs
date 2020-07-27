using JobTrackerX.SharedLibs;
using System.Collections.Generic;
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

        Task<List<JobEntity>> GetDescendantsAsync(long jobId);

        Task<List<JobEntity>> GetChildrenAsync(long jobId);

        Task<List<long>> GetDescendantIdsAsync(long jobId);

        Task<bool> AppendToJobLogAsync(long jobId, AppendLogDto dto);

        Task<JobTreeStatistics> GetJobTreeStatisticsAsync(long jobId);
    }
}