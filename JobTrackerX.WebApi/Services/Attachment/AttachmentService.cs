using AutoMapper;
using JobTrackerX.GrainInterfaces;
using Orleans;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JobTrackerX.WebApi.Services.Attachment
{
    public class AttachmentService : IAttachmentService
    {
        private readonly IClusterClient _client;

        public AttachmentService(IClusterClient client)
        {
            _client = client;
        }

        public async Task<Dictionary<string, string>> GetAllAsync(long id)
        {
            return await _client.GetGrain<IAttachmentGrain>(id).GetAllAsync();
        }

        public async Task<string> GetAsync(long id, string key)
        {
            return await _client.GetGrain<IAttachmentGrain>(id).GetAsync(key);
        }

        public async Task<bool> UpdateAsync(long id, string key, string val)
        {
            return await _client.GetGrain<IAttachmentGrain>(id).UpdateAsync(key, val);
        }
    }
}
