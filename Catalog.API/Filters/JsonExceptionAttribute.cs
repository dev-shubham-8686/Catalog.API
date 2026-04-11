using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Catalog.API.Extensions;
using System.Net;

namespace Catalog.API.Filters
{
    public class JsonExceptionAttribute : TypeFilterAttribute
    {
        public JsonExceptionAttribute()
            : base(typeof(HttpCustomExceptionFilter))
        {
        }

        private class HttpCustomExceptionFilter : IExceptionFilter
        {
            private readonly IWebHostEnvironment _env;
            private readonly ILogger<HttpCustomExceptionFilter> _logger;

            public HttpCustomExceptionFilter(
                IWebHostEnvironment env,
                ILogger<HttpCustomExceptionFilter> logger)
            {
                _env = env;
                _logger = logger;
            }

            public void OnException(ExceptionContext context)
            {
                var eventId = new EventId(context.Exception.HResult);

                _logger.LogError(
                    eventId,
                    context.Exception,
                    context.Exception.Message);

                var json = new JsonErrorPayload
                {
                    EventId = eventId.Id,
                    DetailedMessage =
                        _env.IsDevelopment() || _env.IsIntegration()
                            ? context.Exception.ToString()
                            : "An unexpected error occurred."
                };

                context.Result = new ObjectResult(json)
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };

                context.ExceptionHandled = true;
            }
        }
    }
}