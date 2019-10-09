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

        public async Task<JobEntity> GetJobByIdAsync(long id)
        {
            var jobGrain = _client.GetGrain<IJobGrain>(id);
            var res = await jobGrain.GetJobAsync();

            return _mapper.Map<JobEntity>(res);
        }

        public async Task<List<JobEntity>> GetDescendantEntitiesAsync(long id)
        {
            await _client.GetGrain<IJobGrain>(id).GetJobAsync();
            var childrenIds = await _client.GetGrain<IDescendantsRefGrain>(id).GetChildrenAsync();
            var jobs = new ConcurrentBag<JobEntityState>();
            var getJobInfoProcessor = new ActionBlock<long>(async jobId =>
            {
                var innerJobGrain = _client.GetGrain<IJobGrain>(jobId);
                jobs.Add(await innerJobGrain.GetJobAsync());
            }, Helper.GetOutOfGrainExecutionOptions());

            foreach (var childJobId in childrenIds)
            {
                await getJobInfoProcessor.PostToBlockUntilSuccessAsync(childJobId);
            }

            getJobInfoProcessor.Complete();
            await getJobInfoProcessor.Completion;
            return _mapper.Map<List<JobEntity>>(jobs.OrderBy(s => s.JobId).ToList());
        }

        public async Task<IList<long>> GetDescendantIdsAsync(long id)
        {
            var res = await _client.GetGrain<IDescendantsRefGrain>(id).GetChildrenAsync();
            return res ?? new List<long>();
        }

        public async Task<JobEntity> AddNewJobAsync(AddJobDto dto)
        {
            var jobId = await _client.GetGrain<IJobIdGrain>(Constants.JobIdGrainDefaultName).GetNewIdAsync();
            return _mapper.Map<JobEntity>(await _client.GetGrain<IJobGrain>(jobId)
                .AddJobAsync(dto));
        }

        public async Task<string> UpdateJobStatusAsync(long id, UpdateJobStateDto dto)
        {
            await _client.GetGrain<IJobGrain>(id)
                .UpdateJobStateAsync(dto);
            return "success";
        }

        public async Task<string> UpdateJobOptionsAsync(long id, UpdateJobOptionsDto dto)
        {
            await _client.GetGrain<IJobGrain>(id)
                .UpdateJobOptionsAsync(dto);
            return "success";
        }
    }
}