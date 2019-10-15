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
            context.HttpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Result = new JsonResult(new ReturnDto<object>
            {
                Msg = "InternalException : " + context.Exception.Message,
                Result = false,
                Data = null
            });
            _logger.LogError(context.Exception, $"Exception captured in {nameof(GlobalExceptionFilter)}");
        }
    }

    internal class CustomValidatorResponseAttribute : ActionFilterAttribute
    {
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!context.ModelState.IsValid)
            {
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