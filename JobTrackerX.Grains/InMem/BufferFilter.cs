using System;
using System.Threading.Tasks;
using JobTrackerX.Entities;
using JobTrackerX.GrainInterfaces;
using JobTrackerX.SharedLibs;
using Orleans;
using Orleans.Runtime;

namespace JobTrackerX.Grains.InMem
{
    public class BufferFilter : IIncomingGrainCallFilter
    {
        private readonly IGrainFactory _client;

        public BufferFilter(IGrainFactory client)
        {
            _client = client;
        }

        public async Task Invoke(IIncomingGrainCallContext context)
        {
            await context.Invoke();
            var grainType = context.Grain.GetType();
            var bufferIdObj = RequestContext.Get(Constants.BufferIdKey);
            if (bufferIdObj != null &&
                grainType.GetCustomAttributes(typeof(BufferInMemAttribute), true).Length > 0)
            {
                var bufferId = (Guid) bufferIdObj;
                var grainTypeEnum = grainType.Name switch
                {
                    nameof(JobGrainInMem) => BufferedGrainInterfaceType.JobGrain,
                    nameof(DescendantsRefGrainInMem) => BufferedGrainInterfaceType.DescendantsRefGrain,
                    nameof(JobTreeStatisticsGrainInMem) => BufferedGrainInterfaceType.JobTreeStatisticsGrain,
                    _ => BufferedGrainInterfaceType.JobGrain
                };

                await _client.GetGrain<IBufferManagerGrain>(bufferId)
                    .AddToBufferAsync(new AddToBufferDto(context.Grain.GetPrimaryKeyLong(),
                        grainTypeEnum));
            }
        }
    }
}