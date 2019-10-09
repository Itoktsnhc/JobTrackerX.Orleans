using JobTrackerX.Entities;
using JobTrackerX.GrainInterfaces;
using JobTrackerX.SharedLibs;
using Microsoft.AspNetCore.Mvc;
using Orleans;
using System.Threading.Tasks;

namespace JobTrackerX.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConfigController : ControllerBase
    {
        private readonly IClusterClient _client;

        public ConfigController(IClusterClient client)
        {
            _client = client;
        }

        [HttpGet("offset")]
        public async Task<ReturnDto<long>> GetCurrentOffsetAsync()
        {
            var offsetGrain = _client.GetGrain<IJobIdOffsetGrain>(Constants.JobIdOffsetGrainDefaultName);
            return new ReturnDto<long>(await offsetGrain.GetCurrentOffsetAsync());
        }

        [HttpPut("offset")]
        public async Task<ReturnDto<long>> ApplyNewOffsetAsync(UpdateIdOffsetDto dto)
        {
            var offsetGrain = _client.GetGrain<IJobIdOffsetGrain>(Constants.JobIdOffsetGrainDefaultName);
            await offsetGrain.ApplyOffsetAsync(dto.Offset);
            return new ReturnDto<long>(await offsetGrain.GetCurrentOffsetAsync());
        }

        [HttpGet("id")]
        public async Task<ReturnDto<long>> GetNextIdAsync()
        {
            return new ReturnDto<long>(await _client.GetGrain<IJobIdGrain>(Constants.JobIdGrainDefaultName).GetNewIdAsync());
        }
    }
}