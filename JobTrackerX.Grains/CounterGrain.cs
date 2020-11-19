using System.Threading.Tasks;
using JobTrackerX.Entities;
using JobTrackerX.Entities.GrainStates;
using JobTrackerX.GrainInterfaces;
using Orleans;
using Orleans.Runtime;

namespace JobTrackerX.Grains
{
    public class CounterGrain : Grain, ICounterGrain
    {
        private readonly IPersistentState<CounterState> _state;

        public CounterGrain([PersistentState(nameof(CounterState), Constants.CounterStoreName)]
            IPersistentState<CounterState> state)
        {
            _state = state;
        }

        public async Task AddAsync(int count, string type = Constants.DefaultCounterType)
        {
            if (!_state.State.CountMap.TryGetValue(type, out _))
            {
                _state.State.CountMap[type] = 0;
            }

            _state.State.CountMap[type] += count;
            await _state.WriteStateAsync();
        }

        public Task<long> GetAsync(string type = Constants.DefaultCounterType)
        {
            _state.State.CountMap.TryGetValue(type, out var count);
            return Task.FromResult(count);
        }
    }
}