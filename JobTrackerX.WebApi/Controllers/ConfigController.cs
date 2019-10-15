using JobTrackerX.Entities;
using JobTrackerX.GrainInterfaces;
using JobTrackerX.SharedLibs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Orleans;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace JobTrackerX.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConfigController : ControllerBase
    {
        private readonly IClusterClient _client;
        private readonly JobTrackerConfig _config;

        public ConfigController(IClusterClient client, IOptions<JobTrackerConfig> options)
        {
            _client = client;
            _config = options.Value;
        }

        [HttpGet("offset")]
        public async Task<ReturnDto<long>> GetCurrentOffsetAsync()
        {
            var offsetGrain = _client.GetGrain<IJobIdOffsetGrain>(Constants.JobIdOffsetGrainDefaultName);
            return new ReturnDto<long>(await offsetGrain.GetCurrentOffsetAsync());
        }

        [HttpPut("offset")]
        public async Task<ReturnDto<long>> ApplyNewOffsetAsync([FromBody][Required]UpdateIdOffsetDto dto)
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

        [HttpGet("auth")]
        public ReturnDto<string> CheckAndAddAuthToken([Required] string token)
        {
            HttpContext.Response.Cookies.Append(Constants.TokenAuthKey, token, new CookieOptions()
            {
                Expires = new DateTimeOffset(2038, 1, 1, 0, 0, 0, TimeSpan.FromHours(0))
            });
            if (token != _config.CommonConfig.AuthToken)
            {
                return new ReturnDto<string>()
                {
                    Result = false,
                    Msg = "auth failed"
                };
            }
            return new ReturnDto<string>("auth success");
        }

        [HttpGet("exception")]
        public ReturnDto<string> FireException()
        {
            throw new Exception("sampleException");
        }
    }
}