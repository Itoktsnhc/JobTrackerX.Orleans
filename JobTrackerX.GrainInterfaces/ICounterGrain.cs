using System.Threading.Tasks;
using Orleans;

namespace JobTrackerX.GrainInterfaces
{
    public interface ICounterGrain: IGrainWithStringKey
    {
        Task AddAsync(int count, string type);
        Task<long> GetAsync(string type);
    }
}