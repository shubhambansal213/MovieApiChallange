using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using ApiApplication.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ApiApplication.Middleware
{
    /// <summary>
    /// Global exception handler middleware - catches all unhandled exceptions
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        private readonly IHostEnvironment _environment;

        public GlobalExceptionMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionMiddleware> logger,
            IHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception occurred: {Message}", ex.Message);
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            var response = new ErrorResponse
            {
                TraceId = context.TraceIdentifier,
                Timestamp = DateTime.UtcNow
            };

            switch (exception)
            {
                case ArgumentNullException _:
                case ArgumentException _:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    response.Message = "Invalid request data";
                    break;

                case UnauthorizedAccessException _:
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    response.Message = "Unauthorized access";
                    break;

                case KeyNotFoundException _:
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    response.Message = "Resource not found";
                    break;

                case InvalidOperationException _:
                    response.StatusCode = (int)HttpStatusCode.Conflict;
                    response.Message = "Operation could not be completed";
                    break;

                default:
                    response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    response.Message = "An internal server error occurred";
                    break;
            }

            // Include exception details only in development
            if (_environment.IsDevelopment())
            {
                response.Message = exception.Message;
                response.Errors = new System.Collections.Generic.Dictionary<string, string[]>
                {
                    { "StackTrace", new[] { exception.StackTrace ?? string.Empty } },
                    { "ExceptionType", new[] { exception.GetType().Name } }
                };
            }

            context.Response.StatusCode = response.StatusCode;

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
        }
    }
}
