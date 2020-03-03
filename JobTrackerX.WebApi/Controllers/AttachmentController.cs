using System.Collections.Generic;
using System.Threading.Tasks;
using JobTrackerX.Entities;
using JobTrackerX.WebApi.Services.Attachment;
using Microsoft.AspNetCore.Mvc;

namespace JobTrackerX.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AttachmentController : ControllerBase
    {
        private readonly IAttachmentService _svc;

        public AttachmentController(IAttachmentService svc)
        {
            _svc = svc;
        }

        [HttpGet("/all/{id}")]
        public async Task<Dictionary<string, string>> GetAllAsync([FromRoute]long id)
        {
            return await _svc.GetAllAsync(id);
        }

        [HttpGet("/{id}/{key}")]
        public async Task<string> GetAsync([FromRoute]long id, [FromRoute]string key)
        {
            return await _svc.GetAsync(id, key);
        }

        [HttpPost("/{id}/{key}")]
        public async Task<bool> UpdateAsync([FromRoute]long id, [FromRoute]string key, [FromBody]AttachmentBodyDto val)
        {
            return await _svc.UpdateAsync(id, key, val.Value);
        }
    }
}