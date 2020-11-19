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

            await Helper.RunWithActionBlockAsync(timeIndices, async index =>
            {
                var indexCount = await _client.GetGrain<IAggregateJobIndexGrain>(index).GetRollingIndexCountAsync();
                var indexSeq = Enumerable.Range(0, indexCount + 1).ToList();

                await Helper.RunWithActionBlockAsync(indexSeq, async shardIndex =>
                {
                    var grain = _client.GetGrain<IRollingJobIndexGrain>(Helper.GetRollingIndexId(index, shardIndex));
                    foreach (var item in await grain.QueryAsync(dto.Predicate))
                    {
                        result[item.JobId] = item;
                    }
                });
            });
            return new ReturnQueryIndexDto
            {
                Indices = _mapper.Map<List<JobIndex>>(result.Values
                    .OrderByDescending(s => s.IndexTime)
                    .Skip((dto.PageNumber - 1) * dto.PageSize)
                    .Take(dto.PageSize)
                    .ToList()),
                IndexGrainHit = timeIndices.Count,
                TotalCount = result.Count
            };
        }
    }
}