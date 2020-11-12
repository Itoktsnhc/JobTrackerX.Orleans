using System.Threading.Tasks;
using JobTrackerX.Entities;
using Orleans;

namespace JobTrackerX.GrainInterfaces
{
    public interface IAggregateCounterGrain : IGrainWithIntegerKey
    {
        Task<long> GetAsync(string type = Constants.DefaultCounterType);
        Task AddAsync(int count = 1, string type = Constants.DefaultCounterType);
    }
}