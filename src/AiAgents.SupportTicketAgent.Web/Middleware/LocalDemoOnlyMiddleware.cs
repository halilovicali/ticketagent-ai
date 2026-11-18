using System.Net;

namespace AiAgents.SupportTicketAgent.Web.Middleware;

/// <summary>
/// This submission intentionally runs as a local demo without external identity.
/// External deployment must replace this guard with authenticated user/reviewer roles.
/// </summary>
public sealed class LocalDemoOnlyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var remoteAddress = context.Connection.RemoteIpAddress;
        if (remoteAddress is not null && !IPAddress.IsLoopback(remoteAddress))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "This build is restricted to localhost. Configure authentication before external access."
            });
            return;
        }

        await next(context);
    }
}
