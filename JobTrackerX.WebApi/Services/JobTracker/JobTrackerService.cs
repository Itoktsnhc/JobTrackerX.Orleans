using AutoMapper;
using JobTrackerX.Entities;
using JobTrackerX.Entities.GrainStates;
using JobTrackerX.GrainInterfaces;
using JobTrackerX.SharedLibs;
using Orleans;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace JobTrackerX.WebApi.Services.JobTracker
{
    public class JobTrackerService : IJobTrackerService
    {
        private readonly IClusterClient _client;
        private readonly IMapper _mapper;

        public JobTrackerService(IClusterClient client, IMapper mapper)
        {
            _client = client;
            _mapper = mapper;
        }

        public async Task<long> GetNextIdAsync()
        {
            return await _client.GetGrain<IJobIdGrain>(Constants.JobIdGrainDefaultName).GetNewIdAsync();
        }

        public async Task<JobEntity> GetJobByIdAsync(long id)
        {
            var jobGrain = _client.GetGrain<IJobGrain>(id);
            var res = await jobGrain.GetJobAsync(true);
            if (res == null)
            {
                return null;
            }

            return _mapper.Map<JobEntity>(res);
        }

        public async Task<List<JobEntity>> GetDescendantEntitiesAsync(long id)
        {
            var childrenIds = await _client.GetGrain<IDescendantsRefGrain>(id).GetChildrenAsync();
            return await BatchGetJobEntitiesAsync(childrenIds);
        }

        public async Task<List<JobEntity>> GetChildrenEntitiesAsync(long id)
        {
            var root = await _client.GetGrain<IJobGrain>(id).GetJobAsync();
            return await BatchGetJobEntitiesAsync(root.ChildrenStatesDic.Keys);
        }

        public async Task<List<JobEntity>> BatchGetJobEntitiesAsync(IEnumerable<long> jobIds)
        {
            var jobs = new ConcurrentBag<JobEntityState>();
            var getJobInfoProcessor = new ActionBlock<long>(async jobId =>
            {
                var jobGrain = _client.GetGrain<IJobGrain>(jobId);
                jobs.Add(await jobGrain.GetJobAsync());
            }, Helper.GetOutOfGrainExecutionOptions());
            foreach (var childJobId in jobIds)
            {
                await getJobInfoProcessor.PostToBlockUntilSuccessAsync(childJobId);
            }

            getJobInfoProcessor.Complete();
            await getJobInfoProcessor.Completion;
            return _mapper.Map<List<JobEntity>>(jobs.OrderByDescending(s => s.JobId).ToList());
        }

        public async Task<IList<long>> GetDescendantIdsAsync(long id)
        {
            var res = await _client.GetGrain<IDescendantsRefGrain>(id).GetChildrenAsync();
            return res ?? new List<long>();
        }

        public async Task<JobEntity> AddNewJobAsync(AddJobDto dto)
        {
            var jobId = dto.JobId ??
                        await _client.GetGrain<IJobIdGrain>(Constants.JobIdGrainDefaultName).GetNewIdAsync();
            return _mapper.Map<JobEntity>(await _client.GetGrain<IJobGrain>(jobId)
                .AddJobAsync(dto));
        }

        public async Task<string> UpdateJobStatusAsync(long id, UpdateJobStateDto dto)
        {
            var grain = _client.GetGrain<IJobGrain>(id);
            await grain.UpdateJobStateAsync(dto);
            return "success";
        }

        public async Task<string> UpdateJobOptionsAsync(long id, UpdateJobOptionsDto dto)
        {
            await _client.GetGrain<IJobGrain>(id)
                .UpdateJobOptionsAsync(dto);
            return "success";
        }

        public async Task AppendToJobLogAsync(long id, AppendLogDto dto)
        {
            var logger = _client.GetGrain<IJobLoggerGrain>(id);
            await logger.AppendToJobLogAsync(dto);
        }

        public async Task<string> GetJobLogAsync(long id)
        {
            var logger = _client.GetGrain<IJobLoggerGrain>(id);
            return await logger.GetJobLogAsync();
        }

        public async Task<string> GetJobLogUrlAsync(long id)
        {
            var logger = _client.GetGrain<IJobLoggerGrain>(id);
            return await logger.GetJobLogUrlAsync();
        }

        public async Task<JobTreeStatistics> GetJobStatisticsByIdAsync(long id)
        {
            var statisticsGrain = _client.GetGrain<IJobTreeStatisticsGrain>(id);
            return _mapper.Map<JobTreeStatistics>(await statisticsGrain.GetStatisticsAsync());
        }

        public async Task<Dictionary<long, JobTreeStatistics>> GetJobStatisticsListByIdsAsync(IEnumerable<long> ids)
        {
            var statisticsContainer = new ConcurrentBag<JobTreeStatistics>();
            var getStatisticsProcessor = new ActionBlock<long>(async jobId =>
            {
                var statistics = await GetJobStatisticsByIdAsync(jobId);
                statisticsContainer.Add(statistics);
            }, Helper.GetOutOfGrainExecutionOptions());
            foreach (var id in ids)
            {
                await getStatisticsProcessor.PostToBlockUntilSuccessAsync(id);
            }

            getStatisticsProcessor.Complete();
            await getStatisticsProcessor.Completion;
            return statisticsContainer.ToDictionary(s => s.JobId, s => s);
        }

        public async Task<List<AddJobErrorResult>> BatchAddJobAsync(BatchAddJobDto dto)
        {
            var addErrorResult = new ConcurrentBag<AddJobErrorResult>();
            var parentGrain = _client.GetGrain<IJobGrain>(dto.ParentJobId);
            var parent = await parentGrain.GetJobAsync();
            var idGrain = _client.GetGrain<IJobIdGrain>(Constants.JobIdGrainDefaultName);
            var createChildBlock = new ActionBlock<AddJobDto>(async child =>
            {
                child.JobId ??= await idGrain.GetNewIdAsync();
                child.ParentJobId = dto.ParentJobId;
                var childGrain = _client.GetGrain<IJobGrain>(child.JobId.Value);
                var addChildError =
                    await childGrain.AddJobFromParentAsync(child, parent.AncestorJobId, parent.TrackCountRef);
                if (addChildError != null)
                {
                    addErrorResult.Add(addChildError);
                }
            }, Helper.GetOutOfGrainExecutionOptions());
            foreach (var child in dto.Children)
            {
                await createChildBlock.PostToBlockUntilSuccessAsync(child);
            }

            createChildBlock.Complete();
            await createChildBlock.Completion;

            // ReSharper disable once PossibleInvalidOperationException
            await parentGrain.BatchInitChildrenAsync(dto.Children.Select(s => s.JobId.Value).ToList());
            return addErrorResult.ToList();
        }
        
        public async Task<long> GetDescendantsCountAsync(long jobId)
        {
            return await _client.GetGrain<IAggregateCounterGrain>(jobId).GetAsync();
        }
    }
}