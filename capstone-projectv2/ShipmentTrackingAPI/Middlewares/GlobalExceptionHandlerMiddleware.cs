using System.Net;
using System.Text.Json;
using ShipmentTrackingAPI.Models.Exceptions; // Make sure this matches your namespace!

namespace ShipmentTrackingAPI.Middleware
{
    public class GlobalExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

        public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // Move on to the next piece of middleware (like the Controller)
                await _next(context);
            }
            catch (Exception ex)
            {
                // If anything throws an error, catch it here
                _logger.LogError(ex, "An error occurred processing the request.");
                await HandleExceptionAsync(context, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            // Default to 500 if we don't know what the error is
            var statusCode = (int)HttpStatusCode.InternalServerError;
            var message = "An internal server error occurred.";

            // If it's one of our custom domain exceptions, map it to the correct HTTP Code
            if (exception is AppException appException)
            {
                statusCode = exception switch
                {
                    BadRequestException => (int)HttpStatusCode.BadRequest,       // 400
                    UnauthorizedException => (int)HttpStatusCode.Unauthorized,   // 401
                    ForbiddenException => (int)HttpStatusCode.Forbidden,         // 403
                    NotFoundException => (int)HttpStatusCode.NotFound,           // 404
                    ConflictException => (int)HttpStatusCode.Conflict,           // 409
                    _ => (int)HttpStatusCode.BadRequest                          // Default
                };
                
                // Use the exact message you passed into the exception
                message = appException.Message; 
            }

            context.Response.StatusCode = statusCode;

            // Format the response into clean JSON
            var result = JsonSerializer.Serialize(new 
            { 
                statusCode = statusCode, 
                message = message 
            });

            return context.Response.WriteAsync(result);
        }
    }
}