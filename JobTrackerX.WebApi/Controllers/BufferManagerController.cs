using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using JobTrackerX.SharedLibs;
using JobTrackerX.WebApi.Services.BufferManager;
using Microsoft.AspNetCore.Mvc;

namespace JobTrackerX.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BufferManagerController : ControllerBase
    {
        private readonly IBufferManagerService _svc;

        public BufferManagerController(IBufferManagerService svc)
        {
            _svc = svc;
        }

        /// <summary>
        /// AddNewJobToBuffer
        /// </summary>
        /// <param name="dto"></param>
        /// <param name="bufferId"></param>
        /// <returns></returns>
        [HttpPost("{bufferId}/job/new")]
        public async Task<ReturnDto<JobEntity>> AddJobToBufferAsync([FromBody] AddJobDto dto, [FromRoute] Guid bufferId)
        {
            return new ReturnDto<JobEntity>(await _svc.AddNewJobAsync(dto, bufferId));
        }

        /// <summary>
        /// UpdateJobStatusWithBuffer
        /// </summary>
        /// <param name="bufferId"></param>
        /// <param name="id"></param>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPut("{bufferId}/job/update/{id}")]
        public async Task<ReturnDto<string>> UpdateJobStatusInBufferAsync([FromRoute] Guid bufferId,
            [FromRoute] long id,
            [FromBody] [Required] UpdateJobStateDto dto)
        {
            await _svc.UpdateJobStatusAsync(id, dto, bufferId);
            return new ReturnDto<string>("OK");
        }

        /// <summary>
        /// 更新Options
        /// </summary>
        /// <param name="id"></param>
        /// <param name="dto"></param>
        /// <param name="bufferId"></param>
        /// <returns></returns>
        [HttpPut("{bufferId}/job/updateOptions/{id}")]
        public async Task<ReturnDto<string>> UpdateJobOptionsInBufferAsync([FromRoute] long id,
            [FromBody] [Required] UpdateJobOptionsDto dto, [FromRoute] Guid bufferId)
        {
            await _svc.UpdateJobOptionsAsync(id, dto, bufferId);
            return new ReturnDto<string>("OK");
        }

        /// <summary>
        /// GetJob
        /// </summary>
        /// <param name="id"></param>
        /// <param name="bufferId"></param>
        /// <returns></returns>
        [HttpGet("{bufferId}/job/{id}")]
        public async Task<ReturnDto<JobEntity>> GetJobByIdFromBufferAsync([FromRoute] long id,
            [FromRoute] Guid bufferId)
        {
            return new ReturnDto<JobEntity>(await _svc.GetJobByIdAsync(id, bufferId));
        }


        /// <summary>
        /// FlushBuffered
        /// </summary>
        /// <param name="bufferId"></param>
        /// <returns></returns>
        [HttpPost("{bufferId}")]
        public async Task<ReturnDto<string>> FlushBufferAsync([FromRoute] Guid bufferId)
        {
            await _svc.FlushBufferToStorageAsync(bufferId);
            return new ReturnDto<string>("OK");
        }

        /// <summary>
        /// GetBuffered
        /// </summary>
        /// <param name="bufferId"></param>
        /// <returns></returns>
        [HttpGet("{bufferId}")]
        public async Task<ReturnDto<List<BufferedContent>>> GetBufferedAsync([FromRoute] Guid bufferId)
        {
            return new ReturnDto<List<BufferedContent>>(await _svc.GetBufferedAsync(bufferId));
        }

        [HttpDelete("{bufferId}")]
        public async Task<ReturnDto<string>> DiscardBufferedAsync([FromRoute] Guid bufferId)
        {
            await _svc.DiscardBufferedAsync(bufferId);
            return new ReturnDto<string>("Ok");
        }
    }
}