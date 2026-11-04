using AiAgents.SupportTicketAgent.Application.Abstractions;
using AiAgents.SupportTicketAgent.Domain;

namespace AiAgents.SupportTicketAgent.Application.Services;

public sealed class SettingsService(ISupportTicketRepository repository)
{
    public Task<SystemSettings> GetAsync(CancellationToken cancellationToken) =>
        repository.GetSettingsAsync(cancellationToken);

    public Task<SystemSettings> UpdateAsync(
        float autoRouteThreshold,
        int retrainGoldThreshold,
        bool retrainingEnabled,
        CancellationToken cancellationToken) =>
        repository.UpdateSettingsAsync(
            autoRouteThreshold,
            retrainGoldThreshold,
            retrainingEnabled,
            cancellationToken);
}

