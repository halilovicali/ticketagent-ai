using AiAgents.SupportTicketAgent.Application.Abstractions;
using AiAgents.SupportTicketAgent.Domain;

namespace AiAgents.SupportTicketAgent.Application.Services;

public sealed class ModelQueryService(ISupportTicketRepository repository)
{
    public Task<IReadOnlyList<ModelVersion>> ListAsync(CancellationToken cancellationToken) =>
        repository.ListModelsAsync(cancellationToken);
}

