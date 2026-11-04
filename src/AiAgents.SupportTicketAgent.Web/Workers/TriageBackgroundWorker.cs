using AiAgents.SupportTicketAgent.Application.Runners;
using AiAgents.SupportTicketAgent.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace AiAgents.SupportTicketAgent.Web.Workers;

public sealed class TriageBackgroundWorker(
    IServiceScopeFactory scopeFactory,
    IHubContext<AgentEventsHub> hubContext,
    ILogger<TriageBackgroundWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var runner = scope.ServiceProvider.GetRequiredService<TriageAgentRunner>();
                var result = await runner.StepAsync(stoppingToken);
                if (result is not null)
                {
                    logger.LogInformation(
                        "Triaged ticket {TicketId}: {Category}/{Priority} -> {Status}",
                        result.TicketId,
                        result.Category,
                        result.Priority,
                        result.NewStatus);
                    await hubContext.Clients.All.SendAsync("triageCompleted", result, stoppingToken);
                }

                await Task.Delay(result is null ? TimeSpan.FromSeconds(1) : TimeSpan.FromMilliseconds(100),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Triage worker tick failed");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }
}

