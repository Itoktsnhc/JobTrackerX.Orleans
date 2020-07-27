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
        ///     GetJobDetail
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}")]
        public async Task<ReturnDto<JobEntity>> GetJobByIdAsync(long id)
        {
            return new ReturnDto<JobEntity>(await _service.GetJobByIdAsync(id));
        }

        /// <summary>
        ///     GetJobDetailDescendants
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}/descendants/detail")]
        public async Task<ReturnDto<List<JobEntity>>> GetDescendantEntitiesAsync(long id)
        {
            return new ReturnDto<List<JobEntity>>(await _service.GetDescendantEntitiesAsync(id));
        }

        /// <summary>
        ///     GetJobDetailDescendantsIds
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}/descendants")]
        public async Task<ReturnDto<IList<long>>> GetDescendantIdsAsync(long id)
        {
            return new ReturnDto<IList<long>>(await _service.GetDescendantIdsAsync(id));
        }

        /// <summary>
        /// GetChildrenDetails
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}/children/detail")]
        public async Task<ReturnDto<List<JobEntity>>> GetChildrenEntitiesAsync(long id)
        {
            return new ReturnDto<List<JobEntity>>(await _service.GetChildrenEntitiesAsync(id));
        }

        /// <summary>
        ///     AddJob
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
        ///     UpdateJobState
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

        /// <summary>
        /// UpdateOptions
        /// </summary>
        /// <param name="id"></param>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPut("updateOptions/{id}")]
        public async Task<ReturnDto<string>> UpdateJobOptionsAsync([FromRoute] long id,
            [FromBody] [Required] UpdateJobOptionsDto dto)
        {
            return new ReturnDto<string>(await _service.UpdateJobOptionsAsync(id, dto));
        }

        /// <summary>
        /// 追加任务日志
        /// </summary>
        /// <param name="id"></param>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPut("appendLog/{id}")]
        public async Task<ReturnDto<string>> AppendToJobLogAsync([FromRoute] long id, [FromBody][Required] AppendLogDto dto)
        {
            await _service.AppendToJobLogAsync(id, dto);
            return new ReturnDto<string>("OK");
        }

        /// <summary>
        /// 获取任务日志文件地址
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("logUrl/{id}")]
        public async Task<ReturnDto<string>> GetJobLogUrlAsync([FromRoute] long id)
        {
            return new ReturnDto<string>(await _service.GetJobLogUrlAsync(id));
        }

        /// <summary>
        /// 获取任务日志文件的内容
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("log/{id}")]
        public async Task<string> GetJobLogAsync([FromRoute] long id)
        {
            return await _service.GetJobLogAsync(id);
        }

        [HttpGet("jobTreeStatistics/{id}")]
        public async Task<ReturnDto<JobTreeStatistics>> GetJobTreeStatisticsAsync([FromRoute] long id)
        {
            return new ReturnDto<JobTreeStatistics>(await _service.GetJobStatisticsByIdAsync(id));
        }
    }
}