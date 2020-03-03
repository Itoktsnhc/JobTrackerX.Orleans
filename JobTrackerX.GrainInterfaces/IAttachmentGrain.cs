using Orleans;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JobTrackerX.GrainInterfaces
{
    public interface IAttachmentGrain : IGrainWithIntegerKey
    {
        Task<bool> UpdateAsync(string key, string val);
        Task<string> GetAsync(string key);
        Task<Dictionary<string, string>> GetAllAsync();
    }
}
