using JobTrackerX.SharedLibs;
using JobTrackerX.WebApi.Services.Query;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace JobTrackerX.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QueryIndexController : ControllerBase
    {
        private readonly IQueryIndexService _service;

        public QueryIndexController(IQueryIndexService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<ReturnDto<ReturnQueryIndexDto>> QueryJobIndicesAsync([FromBody] QueryJobIndexDto dto)
        {
            return new ReturnDto<ReturnQueryIndexDto>(await _service.QueryJobsAsync(dto));
        }

        [HttpGet]
        public async Task<ReturnDto<ReturnQueryIndexDto>> QueryDefaultIndicesAsync()
        {
            var currentTime = DateTimeOffset.Now;
            return new ReturnDto<ReturnQueryIndexDto>(await _service.QueryJobsAsync(new QueryJobIndexDto
            {
                Start = currentTime.AddHours(-1),
                End = currentTime
            }));
        }
    }
}