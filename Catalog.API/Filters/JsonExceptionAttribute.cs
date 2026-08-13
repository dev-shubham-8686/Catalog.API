using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Catalog.API.Extensions;
using Catalog.Domain.Responses;
using FluentValidation;
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
                // Handle FluentValidation exceptions
                if (context.Exception is ValidationException validationException)
                {
                    var validationFailureResponse = new ValidationFailureResponse
                    {
                        Errors = validationException.Errors.Select(x => new ValidationResponse
                        {
                            PropertyName = x.PropertyName,
                            Message = x.ErrorMessage
                        })
                    };

                    context.Result = new ObjectResult(validationFailureResponse)
                    {
                        StatusCode = StatusCodes.Status400BadRequest
                    };

                    context.ExceptionHandled = true;
                    return;
                }

                // Handle other exceptions
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