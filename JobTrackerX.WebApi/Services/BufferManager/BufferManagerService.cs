﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using AutoMapper;
using JobTrackerX.Entities;
using JobTrackerX.GrainInterfaces;
using JobTrackerX.GrainInterfaces.InMem;
using JobTrackerX.SharedLibs;
using Orleans;
using Orleans.Runtime;

namespace JobTrackerX.WebApi.Services.BufferManager
{
    public class BufferManagerService : IBufferManagerService
    {
        private readonly IClusterClient _client;
        private readonly IMapper _mapper;

        public BufferManagerService(IClusterClient client, IMapper mapper)
        {
            _client = client;
            _mapper = mapper;
        }

        public async Task<JobEntity> AddNewJobAsync(AddJobDto dto, Guid bufferId)
        {
            RequestContext.Set(Constants.BufferIdKey, bufferId);
            var jobId = dto.JobId ??
                        await _client.GetGrain<IJobIdGrain>(Constants.JobIdGrainDefaultName).GetNewIdAsync();
            return _mapper.Map<JobEntity>(await _client.GetGrain<IJobGrainInMem>(jobId)
                .AddJobAsync(dto));
        }

        public async Task UpdateJobStatusAsync(long id, UpdateJobStateDto dto, Guid bufferId)
        {
            RequestContext.Set(Constants.BufferIdKey, bufferId);
            var grain = _client.GetGrain<IJobGrainInMem>(id);
            await grain.UpdateJobStateAsync(dto);
        }

        public async Task UpdateJobOptionsAsync(long id, UpdateJobOptionsDto dto, Guid bufferId)
        {
            RequestContext.Set(Constants.BufferIdKey, bufferId);
            var grain = _client.GetGrain<IJobGrainInMem>(id);
            await grain.UpdateJobOptionsAsync(dto);
        }

        public async Task<JobEntity> GetJobByIdAsync(long id, Guid bufferId)
        {
            RequestContext.Set(Constants.BufferIdKey, bufferId);
            var grain = _client.GetGrain<IJobGrainInMem>(id);
            var res = await grain.GetJobAsync(true);
            return res == null ? null : _mapper.Map<JobEntity>(res);
        }

        public async Task DiscardBufferedAsync(Guid bufferId)
        {
            RequestContext.Set(Constants.BufferIdKey, bufferId);
            var manager = _client.GetGrain<IBufferManagerGrain>(bufferId);
            var bufferedGrains = await manager.GetBufferedAsync();
            await ManipulateBufferAsync(bufferedGrains, false);
            await manager.DeactivateAsync();
        }

        public async Task FlushBufferToStorageAsync(Guid bufferId)
        {
            RequestContext.Set(Constants.BufferIdKey, bufferId);
            var manager = _client.GetGrain<IBufferManagerGrain>(bufferId);
            var bufferedGrains = await manager.GetBufferedAsync();
            await ManipulateBufferAsync(bufferedGrains);
            await manager.DeactivateAsync();
        }

        public async Task<List<BufferedContent>> GetBufferedAsync(Guid bufferId)
        {
            RequestContext.Set(Constants.BufferIdKey, bufferId);
            return _mapper.Map<List<BufferedContent>>(await _client.GetGrain<IBufferManagerGrain>(bufferId)
                .GetBufferedAsync());
        }

        private async Task ManipulateBufferAsync(IEnumerable<AddToBufferDto> bufferedGrains, bool syncState = true)
        {
            var flushAction = new ActionBlock<AddToBufferDto>(async item =>
            {
                switch (item.GrainType)
                {
                    case BufferedGrainInterfaceType.JobGrain:
                        await _client.GetGrain<IJobGrainInMem>(item.GrainIntId).DeactivateAsync(syncState);
                        break;
                    case BufferedGrainInterfaceType.DescendantsRefGrain:
                        await _client.GetGrain<IDescendantsRefGrainInMem>(item.GrainIntId).DeactivateAsync(syncState);
                        break;
                    case BufferedGrainInterfaceType.JobTreeStatisticsGrain:
                        await _client.GetGrain<IJobTreeStatisticsGrainInMem>(item.GrainIntId)
                            .DeactivateAsync(syncState);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }, Helper.GetOutOfGrainExecutionOptions());
            foreach (var item in bufferedGrains)
            {
                await flushAction.PostToBlockUntilSuccessAsync(item);
            }

            flushAction.Complete();
            await flushAction.Completion;
        }
    }
}
