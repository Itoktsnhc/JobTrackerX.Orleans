using System.Threading.Tasks;
using JobTrackerX.Entities;
using Orleans;

namespace JobTrackerX.GrainInterfaces
{
    public interface ICounterGrain: IGrainWithStringKey
    {
        Task AddAsync(int count, string type = Constants.DefaultCounterType);
        Task<long> GetAsync(string type = Constants.DefaultCounterType);
    }
}