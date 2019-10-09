using JobTrackerX.SharedLibs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace JobTrackerX.WebApi.Misc
{
    internal class GlobalExceptionFilter : IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            context.HttpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Result = new JsonResult(new ReturnDto<object>
            {
                Msg = "Exception！！！ -- " + context.Exception.Message,
                Result = false,
                Data = null
            });
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