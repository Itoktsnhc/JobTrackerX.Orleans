using Orleans;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JobTrackerX.GrainInterfaces
{
    public interface IDescendantsRefGrain : IGrainWithIntegerKey
    {
        Task AttachToChildrenAsync(long childJobId);

        Task<IList<long>> GetChildrenAsync();
    }
}