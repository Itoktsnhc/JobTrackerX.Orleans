using System.Collections.Generic;
using System.Threading.Tasks;
using JobTrackerX.SharedLibs;

namespace JobTrackerX.Client
{
    public interface IScaledJobTrackerClient
    {
        Task<long> GetNextIdAsync();

        Task<JobEntity> CreateNewJobAsync(AddJobDto dto);

        Task UpdateJobStatesAsync(long id, UpdateJobStateDto dto);

        Task UpdateJobOptionsAsync(long id, UpdateJobOptionsDto dto);

        Task<JobEntity> GetJobEntityAsync(long jobId);

        Task<ReturnQueryIndexDto> QueryJobIndexAsync(QueryJobIndexDto dto);

        Task<List<JobEntity>> GetChildrenAsync(long jobId);

        Task<bool> AppendToJobLogAsync(long jobId, AppendLogDto dto);

        Task<JobTreeStatistics> GetJobTreeStatisticsAsync(long jobId);
    }
}