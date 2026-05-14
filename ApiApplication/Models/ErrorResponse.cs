using System;
using System.Collections.Generic;

namespace ApiApplication.Models
{
    /// <summary>
    /// Standardized error response model
    /// </summary>
    public class ErrorResponse
    {
        public string Message { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public string? TraceId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Dictionary<string, string[]>? Errors { get; set; }

        public ErrorResponse()
        {
        }

        public ErrorResponse(int statusCode, string message)
        {
            StatusCode = statusCode;
            Message = message;
        }
    }
}
