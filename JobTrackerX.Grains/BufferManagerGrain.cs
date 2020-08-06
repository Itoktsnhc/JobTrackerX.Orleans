using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JobTrackerX.Entities;
using JobTrackerX.GrainInterfaces;
using Orleans;

namespace JobTrackerX.Grains
{
    public class BufferManagerGrain : Grain, IBufferManagerGrain
    {
        private readonly HashSet<AddToBufferDto> _buffer =
            new HashSet<AddToBufferDto>(new AddToBufferDtoEqualityComparer());

        public Task AddToBufferAsync(AddToBufferDto dto)
        {
            _buffer.Add(dto);
            return Task.CompletedTask;
        }

        public Task<List<AddToBufferDto>> GetBufferedAsync()
        {
            return Task.FromResult(_buffer.ToList());
        }

        public Task DeactivateAsync()
        {
            DeactivateOnIdle();
            return Task.CompletedTask;
        }
    }
}