using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JobTrackerX.SharedLibs;

namespace JobTrackerX.WebApi.Services.BufferManager
{
    public interface IBufferManagerService
    {
        Task<JobEntity> AddNewJobAsync(AddJobDto dto, Guid bufferId);
        Task UpdateJobStatusAsync(long id, UpdateJobStateDto dto, Guid bufferId);
        Task FlushBufferToStorageAsync(Guid bufferId);
        Task<List<BufferedContent>> GetBufferedAsync(Guid bufferId);
        Task UpdateJobOptionsAsync(long id, UpdateJobOptionsDto dto, Guid bufferId);
        Task<JobEntity> GetJobByIdAsync(long id, Guid bufferId);
        Task DiscardBufferedAsync(Guid bufferId);
    }
}