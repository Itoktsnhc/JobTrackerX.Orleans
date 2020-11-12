using System.Collections.Generic;

namespace JobTrackerX.Entities.GrainStates
{
    public class CounterState
    {
        public Dictionary<string, long> CountMap { get; set; } = new Dictionary<string, long>();
    }

    public class AggregateCounterState
    {
        public int CounterCount { get; set; } = Constants.CounterPerAggregateCounter;
    }
}