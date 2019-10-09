using JobTrackerX.SharedLibs;
using JobTrackerX.WebApi.Services.JobTracker;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace JobTrackerX.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JobTrackerController : ControllerBase
    {
        private readonly IJobTrackerService _service;

        public JobTrackerController(IJobTrackerService service)
        {
            _service = service;
        }

        /// <summary>
        ///     获取该JobId的Job详情
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}")]
        public async Task<ReturnDto<JobEntity>> GetJobByIdAsync(long id)
        {
            return new ReturnDto<JobEntity>(await _service.GetJobByIdAsync(id));
        }

        /// <summary>
        ///     获取该Id的包括所有子节点详情
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}/descendants/detail")]
        public async Task<ReturnDto<List<JobEntity>>> GetDescendantEntitiesAsync(long id)
        {
            return new ReturnDto<List<JobEntity>>(await _service.GetDescendantEntitiesAsync(id));
        }

        /// <summary>
        ///     获取该节点向后的节点Id(包括自身节点Id)
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}/descendants")]
        public async Task<ReturnDto<IList<long>>> GetDescendantIdsAsync(long id)
        {
            return new ReturnDto<IList<long>>(await _service.GetDescendantIdsAsync(id));
        }

        /// <summary>
        ///     添加任务
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("new")]
        public async Task<ReturnDto<JobEntity>> AddNewJob(
            [FromBody] AddJobDto dto)
        {
            return new ReturnDto<JobEntity>(await _service.AddNewJobAsync(dto));
        }

        /// <summary>
        ///     更新任务状态
        /// </summary>
        /// <param name="id"></param>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPut("update/{id}")]
        public async Task<ReturnDto<string>> UpdateJobStatusAsync([FromRoute] long id,
            [FromBody] [Required] UpdateJobStateDto dto)
        {
            return new ReturnDto<string>(await _service.UpdateJobStatusAsync(id, dto));
        }

        [HttpPut("updateOptions/{id}")]
        public async Task<ReturnDto<string>> UpdateJobOptionsAsync([FromRoute] long id,
            [FromBody] [Required] UpdateJobOptionsDto dto)
        {
            return new ReturnDto<string>(await _service.UpdateJobOptionsAsync(id, dto));
        }
    }
}