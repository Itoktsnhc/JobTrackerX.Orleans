using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JobTrackerX.Entities;
using JobTrackerX.GrainInterfaces;
using Orleans;

namespace JobTrackerX.Grains
{
    public class AggregateCounterGrain : Grain, IAggregateCounterGrain
    {
        private readonly Random _rand = new Random();

        public async Task AddAsync(int count = 1, string type = Constants.DefaultCounterType)
        {
            var counter = GrainFactory.GetGrain<ICounterGrain>(
                $"{this.GetPrimaryKeyLong()}-{_rand.Next(Constants.CounterPerAggregateCounter)}");
            await counter.AddAsync(count, type);
        }

        public async Task<long> GetAsync(string type = Constants.DefaultCounterType)
        {
            var counters = Enumerable.Range(0, Constants.CounterPerAggregateCounter)
                .Select(s => GrainFactory.GetGrain<ICounterGrain>($"{this.GetPrimaryKeyLong()}-{s}")).ToList();
            long sum = 0;
            await Helper.RunWithActionBlockAsync(counters,
                async counter => Interlocked.Add(ref sum, await counter.GetAsync(type)),
                Helper.GetGrainInternalExecutionOptions());
            return sum;
        }
    }
}