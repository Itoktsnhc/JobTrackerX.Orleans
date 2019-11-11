using JobTrackerX.SharedLibs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace JobTrackerX.WebApi.Misc
{
    internal class GlobalExceptionFilter : IExceptionFilter
    {
        private readonly ILogger<GlobalExceptionFilter> _logger;

        public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger)
        {
            _logger = logger;
        }

        public void OnException(ExceptionContext context)
        {
            var info = $"Exception captured in {nameof(GlobalExceptionFilter)}";
            if (context.Exception is JobNotFoundException)
            {
                context.HttpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
                context.HttpContext.Response.Headers.Add(nameof(JobNotFoundException), "true");
                context.Result = new JsonResult(new ReturnDto<object>
                {
                    Msg = context.Exception.Message,
                    Result = false,
                    Data = null
                });
                _logger.LogWarning(context.Exception, info);
            }
            else
            {
                context.HttpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

                context.Result = new JsonResult(new ReturnDto<object>
                {
                    Msg = "InternalException : " + context.Exception.Message,
                    Result = false,
                    Data = null
                });
                _logger.LogError(context.Exception, info);
            }
        }
    }

    internal class CustomValidatorResponseAttribute : ActionFilterAttribute
    {
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!context.ModelState.IsValid)
            {
                context.HttpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Result = new JsonResult(new ReturnDto<string>
                {
                    Data = null,
                    Msg = string.Join(" | ", context.ModelState.Values
                        .SelectMany(e => e.Errors)
                        .Select(e => e.ErrorMessage)),
                    Result = false
                });
                return;
            }

            await next();
        }
    }
}