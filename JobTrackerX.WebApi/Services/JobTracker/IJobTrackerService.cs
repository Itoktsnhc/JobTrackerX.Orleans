using JobTrackerX.SharedLibs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JobTrackerX.WebApi.Services.JobTracker
{
    public interface IJobTrackerService
    {
        Task<JobEntity> GetJobByIdAsync(long id);

        Task<List<JobEntity>> GetDescendantEntitiesAsync(long id);

        Task<IList<long>> GetDescendantIdsAsync(long id);

        Task<JobEntity> AddNewJobAsync(AddJobDto dto);

        Task<string> UpdateJobStatusAsync(long id, UpdateJobStateDto dto);

        Task<string> UpdateJobOptionsAsync(long id, UpdateJobOptionsDto dto);

        Task<List<JobEntity>> GetChildrenEntitiesAsync(long id);

        Task<List<JobEntity>> BatchGetJobEntitiesAsync(IEnumerable<long> jobIds);

        Task AppendToJobLogAsync(long id, AppendLogDto dto);

        Task<string> GetJobLogAsync(long id);

        Task<string> GetJobLogUrlAsync(long id);
    }
}