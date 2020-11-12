using System;
using JobTrackerX.SharedLibs;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace JobTrackerX.Client
{
    // ReSharper disable once InconsistentNaming
    public abstract class IJobTrackerClient
    {
        public abstract Task<long> GetNextIdAsync();

        public abstract Task<JobEntity> CreateNewJobAsync(AddJobDto dto);

        public abstract Task UpdateJobStatesAsync(long id, UpdateJobStateDto dto);

        public abstract Task UpdateJobOptionsAsync(long id, UpdateJobOptionsDto dto);

        public abstract Task<JobEntity> GetJobEntityAsync(long jobId);

        public abstract Task<ReturnQueryIndexDto> QueryJobIndexAsync(QueryJobIndexDto dto);

        public abstract Task<List<JobEntity>> GetDescendantsAsync(long jobId);

        public abstract Task<List<JobEntity>> GetChildrenAsync(long jobId);

        public abstract Task<List<long>> GetDescendantIdsAsync(long jobId);

        public abstract Task<bool> AppendToJobLogAsync(long jobId, AppendLogDto dto);

        public abstract Task<JobTreeStatistics> GetJobTreeStatisticsAsync(long jobId);
        
        public abstract Task<List<AddJobErrorResult>> BatchAddChildrenAsync(BatchAddJobDto dto,
            ExecutionDataflowBlockOptions options = null);
        
        public abstract Task<long> GetDescendantsCountAsync(long jobId);

        internal abstract Task<JobEntity> CreateNewJobWithBufferAsync(AddJobDto dto, Guid bufferId);

        internal abstract Task UpdateJobStatesWithBufferAsync(long id, UpdateJobStateDto dto, Guid bufferId);

        internal abstract Task UpdateJobOptionsWithBufferAsync(long id, UpdateJobOptionsDto dto, Guid bufferId);

        internal abstract Task<JobEntity> GetJobEntityWithBufferAsync(long jobId, Guid bufferId);

        internal abstract Task<List<BufferedContent>> GetBufferedContentAsync(Guid bufferId);

        internal abstract Task FlushBufferedContentAsync(Guid bufferId);

        internal abstract Task DiscardBufferedContentAsync(Guid bufferId);
    }
}