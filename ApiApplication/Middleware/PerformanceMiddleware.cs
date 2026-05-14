using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ApiApplication.Middleware
{
    public class PerformanceMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<PerformanceMiddleware> _logger;

        public PerformanceMiddleware(RequestDelegate next, ILogger<PerformanceMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var requestPath = context.Request.Path;
            var requestMethod = context.Request.Method;

            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();
                _logger.LogInformation(
                    "Request {Method} {Path} completed in {ElapsedMilliseconds}ms with status code {StatusCode}",
                    requestMethod,
                    requestPath,
                    stopwatch.ElapsedMilliseconds,
                    context.Response.StatusCode);
            }
        }
    }
}
