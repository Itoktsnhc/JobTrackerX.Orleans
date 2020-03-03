using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JobTrackerX.WebApi.Services.Attachment
{
    public interface IAttachmentService
    {
        Task<bool> UpdateAsync(long id, string key, string val);
        Task<string> GetAsync(long id, string key);
        Task<Dictionary<string, string>> GetAllAsync(long id);
    }
}
