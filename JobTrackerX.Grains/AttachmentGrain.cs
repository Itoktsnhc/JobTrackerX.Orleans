using JobTrackerX.Entities;
using JobTrackerX.Entities.GrainStates;
using JobTrackerX.GrainInterfaces;
using Orleans;
using Orleans.Providers;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JobTrackerX.Grains
{
    [StorageProvider(ProviderName = Constants.AttachmentStoreName)]
    public class AttachmentGrain : Grain<AttachmentState>, IAttachmentGrain
    {
        public async Task<bool> UpdateAsync(string key, string val)
        {
            State.Body[key] = val;
            await WriteStateAsync();
            return true;
        }

        public Task<string> GetAsync(string key)
        {
            if (State.Body.ContainsKey(key))
            {
                return Task.FromResult(State.Body[key]);
            }
            return Task.FromResult<string>(null);
        }

        public Task<Dictionary<string, string>> GetAllAsync() => Task.FromResult(State.Body);
    }
}
