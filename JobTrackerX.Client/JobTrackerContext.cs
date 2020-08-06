using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using JobTrackerX.SharedLibs;

namespace JobTrackerX.Client
{
    public class JobTrackerContext : IJobTrackerContext
    {
        private readonly IJobTrackerClient _client;
        private readonly Guid _scopeBufferId;

        /// <summary>
        /// Create in memory buffer Context for Operations, you need call CloseAndCommitAsync to save all states to storage
        /// </summary>
        /// <param name="client"></param>
        /// <param name="bufferId"></param>
        public JobTrackerContext(IJobTrackerClient client, Guid? bufferId = null)
        {
            _client = client;
            _scopeBufferId = bufferId ?? Guid.NewGuid();
        }

        public async Task<JobEntity> CreateNewJobAsync(AddJobDto dto)
        {
            return await _client.CreateNewJobWithBufferAsync(dto, _scopeBufferId);
        }

        public async Task UpdateJobStatesAsync(long id, UpdateJobStateDto dto)
        {
            await _client.UpdateJobStatesWithBufferAsync(id, dto, _scopeBufferId);
        }

        public async Task UpdateJobOptionsAsync(long id, UpdateJobOptionsDto dto)
        {
            await _client.UpdateJobOptionsWithBufferAsync(id, dto, _scopeBufferId);
        }

        public async Task<JobEntity> GetJobEntityAsync(long jobId)
        {
            return await _client.GetJobEntityWithBufferAsync(jobId, _scopeBufferId);
        }

        public async Task<List<BufferedContent>> GetContextContentAsync()
        {
            return await _client.GetBufferedContentAsync(_scopeBufferId);
        }

        public async Task CommitAndCloseAsync()
        {
            await _client.FlushBufferedContentAsync(_scopeBufferId);
        }

        public async Task CloseAsync()
        {
            await _client.DiscardBufferedContentAsync(_scopeBufferId);
        }

        public async Task BatchOperationAsync(JobTrackerBatchOperation operation)
        {
            var actionBlock = new ActionBlock<Func<Task>>(async func => { await func.Invoke(); }, operation.Options);
            foreach (var func in operation.CachedFuncs)
            {
                var pushOk = false;
                while (!pushOk)
                {
                    pushOk = actionBlock.Post(func);
                    if (!pushOk)
                    {
                        await Task.Delay(10);
                    }
                }
            }

            actionBlock.Complete();
            await actionBlock.Completion;
        }
    }
}