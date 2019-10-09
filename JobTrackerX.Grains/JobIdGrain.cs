using JobTrackerX.Entities;
using JobTrackerX.GrainInterfaces;
using Orleans;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JobTrackerX.Grains
{
    #region ServceBusIdGenerator

    public class ServiceBusJobIdGrain : Grain, IJobIdGrain
    {
        private readonly ServiceBusWrapper _wrapper;
        private long _idRangeEnd;

        public ServiceBusJobIdGrain(ServiceBusWrapper wrapper)
        {
            _wrapper = wrapper;
        }

        private long Current { get; set; }

        public async Task<long> GetNewIdAsync()
        {
            var result = ++Current;
            await CheckAsync();
            return result + await GrainFactory
                       .GetGrain<IJobIdOffsetGrain>(Constants.JobIdOffsetGrainDefaultName)
                       .GetCurrentOffsetAsync();
        }

        public async Task<IEnumerable<long>> GetNewIdsAsync(int count)
        {
            var offset = await GrainFactory
                .GetGrain<IJobIdOffsetGrain>(Constants.JobIdOffsetGrainDefaultName)
                .GetCurrentOffsetAsync();
            var res = new List<long>(count);
            var last = Current;
            Current += count;
            for (var i = 0; i < count; i++)
            {
                res.Add(++last + offset);
            }

            await CheckAsync();
            return res;
        }

        public override async Task OnActivateAsync()
        {
            await base.OnActivateAsync();
            await AcquireNewIdRangeAsync();
        }

        private async Task AcquireNewIdRangeAsync()
        {
            var message = await _wrapper.Receiver.ReceiveAsync();
            Current = _wrapper.ScaleSize * (message.SystemProperties.SequenceNumber - 1);
            _idRangeEnd = _wrapper.ScaleSize * message.SystemProperties.SequenceNumber;
        }

        private async Task CheckAsync()
        {
            if (Current + _wrapper.CrashDistance >= _idRangeEnd)
            {
                await AcquireNewIdRangeAsync();
            }
        }
    }

    #endregion
}