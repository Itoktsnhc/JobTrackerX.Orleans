using Orleans;
using System.Collections.Generic;
using System.Threading.Tasks;
using JobTrackerX.Entities.GrainStates;

namespace JobTrackerX.GrainInterfaces
{
    public interface IDescendantsRefGrain : IGrainWithIntegerKey
    {
        Task AttachToChildrenAsync(long childJobId);
        Task<IList<long>> GetChildrenAsync();
        Task SetStateAsync(DescendantsRefState state);
    }
}