using AutoMapper;
using JobTrackerX.Entities;
using JobTrackerX.Entities.GrainStates;
using JobTrackerX.GrainInterfaces;
using JobTrackerX.SharedLibs;
using Orleans;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace JobTrackerX.WebApi.Services.Query
{
    public class QueryIndexService : IQueryIndexService
    {
        private readonly IClusterClient _client;
        private readonly IMapper _mapper;

        public QueryIndexService(IClusterClient client, IMapper mapper)
        {
            _client = client;
            _mapper = mapper;
        }

        public async Task<ReturnQueryIndexDto> QueryJobsAsync(QueryJobIndexDto dto)
        {
            if (dto.Start > dto.End)
            {
                throw new InvalidOperationException("the start date cannot be greater than the end date");
            }

            if (dto.PageNumber <= 0)
            {
                dto.PageNumber = 1;
            }

            if (dto.PageSize <= 0)
            {
                dto.PageSize = 10;
            }

            var timeIndices = Helper.GetTimeIndexRange(dto.Start, dto.End);
            var result = new ConcurrentDictionary<long, JobIndexInternal>();
            var queryIndicesBlock = new ActionBlock<string>(
                async index =>
                {
                    var readOnlyGrainIndices =
                        await _client.GetGrain<IAggregateJobIndexGrain>(index).QueryAsync(dto.Predicate);
                    foreach (var innerIndex in readOnlyGrainIndices)
                    {
                        result[innerIndex.JobId] = innerIndex;
                    }
                }, Helper.GetOutOfGrainExecutionOptions());

            foreach (var index in timeIndices)
            {
                await queryIndicesBlock.PostToBlockUntilSuccessAsync(index);
            }

            queryIndicesBlock.Complete();
            await queryIndicesBlock.Completion;

            return new ReturnQueryIndexDto
            {
                Indices = _mapper.Map<List<JobIndex>>(result.Values
                    .OrderByDescending(s => s.JobId)
                    .Skip((dto.PageNumber - 1) * dto.PageNumber)
                    .Take(dto.PageSize)
                    .ToList()),
                IndexGrainHit = timeIndices.Count,
                TotalCount = result.Count
            };
        }
    }
}