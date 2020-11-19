using System;
using System.Collections.Generic;
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

        public Task<List<string>> GetCountersAsync()
        {
            return Task.FromResult(Enumerable.Range(0, Constants.CounterPerAggregateCounter)
                .Select(s => $"{this.GetPrimaryKeyLong()}-{s}").ToList());
        }
    }
}