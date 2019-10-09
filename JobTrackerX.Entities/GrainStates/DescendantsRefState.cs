using System.Collections.Generic;

namespace JobTrackerX.Entities.GrainStates
{
    public class DescendantsRefState
    {
        public HashSet<long> DescendantJobs { get; set; }
    }
}