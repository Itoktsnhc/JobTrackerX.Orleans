using JobTrackerX.Entities;
using JobTrackerX.SharedLibs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Orleans;
using System;
using System.Threading.Tasks;

namespace JobTrackerX.WebApi.Misc
{
    public class TokenAuth
    {
        private readonly RequestDelegate _next;
        private readonly CommonConfig _commonConfig;

        public TokenAuth(RequestDelegate next, IOptions<JobTrackerConfig> options)
        {
            _next = next;
            _commonConfig = options.Value.CommonConfig;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            if (httpContext.Request.Path.Equals("/api/config/auth", StringComparison.InvariantCultureIgnoreCase))
            {
                await _next(httpContext);
            }
            else
            {
                if (IsTokenOk(httpContext) || AlreadyAuthenticated(httpContext))
                {
                    await _next(httpContext);
                }
                else
                {
                    var jsonStrResult = JsonConvert.SerializeObject(new ReturnDto<string>()
                    {
                        Result = false,
                        Msg = "Unauthorized"
                    }, new JsonSerializerSettings
                    {
                        ContractResolver = new CamelCasePropertyNamesContractResolver()
                    });
                    httpContext.Response.StatusCode = 401;
                    httpContext.Response.ContentType = "application/json";
                    await httpContext.Response.WriteAsync(jsonStrResult);
                }
            }
        }

        private static bool AlreadyAuthenticated(HttpContext httpContext)
        {
            return httpContext.User?.Identity.IsAuthenticated == true;
        }

        private bool IsTokenOk(HttpContext httpContext)
        {
            return httpContext.Request.Cookies.TryGetValue(Constants.TokenAuthKey, out var authTokenValue)
                     && authTokenValue == _commonConfig.AuthToken;
        }
    }
}