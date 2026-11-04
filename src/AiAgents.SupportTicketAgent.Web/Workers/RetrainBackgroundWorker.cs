using AiAgents.SupportTicketAgent.Application.Runners;
using AiAgents.SupportTicketAgent.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace AiAgents.SupportTicketAgent.Web.Workers;

public sealed class RetrainBackgroundWorker(
    IServiceScopeFactory scopeFactory,
    IHubContext<AgentEventsHub> hubContext,
    ILogger<RetrainBackgroundWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var runner = scope.ServiceProvider.GetRequiredService<RetrainAgentRunner>();
                var result = await runner.StepAsync(stoppingToken);
                if (result is { Trained: true })
                {
                    logger.LogInformation(
                        "Activated model v{Version} with test accuracy {Accuracy:P1}",
                        result.ModelVersion,
                        result.TestAccuracy);
                    await hubContext.Clients.All.SendAsync("modelRetrained", result, stoppingToken);
                }

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Retraining worker tick failed");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}

