using System.Collections.Generic;
using System.Threading.Tasks;
using JobTrackerX.SharedLibs;

namespace JobTrackerX.Client
{
    public interface IJobTrackerContext
    {
        Task<JobEntity> CreateNewJobAsync(AddJobDto dto);
        Task UpdateJobStatesAsync(long id, UpdateJobStateDto dto);
        Task UpdateJobOptionsAsync(long id, UpdateJobOptionsDto dto);
        Task<JobEntity> GetJobEntityAsync(long jobId);
        Task<List<BufferedContent>> GetContextContentAsync();
        Task CommitAndCloseAsync();
        Task CloseAsync();
        Task BatchOperationAsync(JobTrackerBatchOperation operation);
    }
}