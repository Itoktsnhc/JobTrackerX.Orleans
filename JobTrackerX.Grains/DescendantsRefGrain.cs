using JobTrackerX.Entities;
using JobTrackerX.Entities.GrainStates;
using JobTrackerX.GrainInterfaces;
using Orleans;
using Orleans.Providers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JobTrackerX.Grains
{
    [StorageProvider(ProviderName = Constants.JobRefStoreName)]
    public class DescendantsRefGrain : Grain<DescendantsRefState>, IDescendantsRefGrain
    {
        public async Task AttachToChildrenAsync(long childJobId)
        {
            (State.DescendantJobs ?? (State.DescendantJobs = new HashSet<long>())).Add(childJobId);
            await WriteStateAsync();
        }

        public async Task<IList<long>> GetChildrenAsync()
        {
            if (State.DescendantJobs == null)
            {
                return new List<long> { this.GetPrimaryKeyLong() };
            }

            return await Task.FromResult(State.DescendantJobs.ToList());
        }
    }
}