using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans;

namespace JobTrackerX.GrainInterfaces.InMem
{
    public interface IDescendantsRefGrainInMem : IGrainWithIntegerKey
    {
        Task AttachToChildrenAsync(long childJobId);
        Task<IList<long>> GetChildrenAsync();
        Task DeactivateAsync(bool syncState);
    }
}