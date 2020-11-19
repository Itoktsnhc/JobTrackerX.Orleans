using System.Collections.Generic;
using System.Threading.Tasks;
using JobTrackerX.Entities;
using Orleans;

namespace JobTrackerX.GrainInterfaces
{
    public interface IAggregateCounterGrain : IGrainWithIntegerKey
    {
        Task<List<string>> GetCountersAsync();
        Task AddAsync(int count = 1, string type = Constants.DefaultCounterType);
    }
}