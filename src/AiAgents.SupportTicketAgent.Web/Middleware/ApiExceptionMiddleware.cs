using Microsoft.AspNetCore.Mvc;

namespace AiAgents.SupportTicketAgent.Web.Middleware;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client disconnected; there is no response left to write.
        }
        catch (Exception exception)
        {
            var status = exception switch
            {
                ArgumentException => StatusCodes.Status400BadRequest,
                KeyNotFoundException => StatusCodes.Status404NotFound,
                InvalidOperationException => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError
            };

            if (status >= 500)
                logger.LogError(exception, "Unhandled API error");
            else
                logger.LogInformation(exception, "API request rejected");

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = status,
                Title = status >= 500 ? "Unexpected server error" : "Request could not be completed",
                Detail = status >= 500 ? "See server logs for details." : exception.Message
            });
        }
    }
}

