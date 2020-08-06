using System.Collections.Generic;
using System.Threading.Tasks;
using JobTrackerX.Entities;
using Orleans;

namespace JobTrackerX.GrainInterfaces
{
    public interface IBufferManagerGrain : IGrainWithGuidKey
    {
        public Task AddToBufferAsync(AddToBufferDto dto);
        Task<List<AddToBufferDto>> GetBufferedAsync();
        Task DeactivateAsync();
    }
}