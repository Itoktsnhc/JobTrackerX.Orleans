using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JobTrackerX.Entities;
using JobTrackerX.Entities.GrainStates;
using JobTrackerX.GrainInterfaces;
using JobTrackerX.GrainInterfaces.InMem;
using Orleans;

namespace JobTrackerX.Grains.InMem
{
    [BufferInMem]
    public class DescendantsRefGrainInMem : Grain, IDescendantsRefGrainInMem
    {
        private readonly DescendantsRefState _state = new DescendantsRefState();

        public Task AttachToChildrenAsync(long childJobId)
        {
            (_state.DescendantJobs ?? (_state.DescendantJobs = new HashSet<long>())).Add(childJobId);
            return Task.CompletedTask;
        }

        public async Task<IList<long>> GetChildrenAsync()
        {
            if (_state.DescendantJobs == null)
            {
                return new List<long> {this.GetPrimaryKeyLong()};
            }

            return await Task.FromResult(_state.DescendantJobs.ToList());
        }

        public async Task DeactivateAsync(bool syncState)
        {
            if (syncState)
            {
                await GrainFactory.GetGrain<IDescendantsRefGrain>(this.GetPrimaryKeyLong()).SetStateAsync(_state);
            }

            DeactivateOnIdle();
        }
    }
}