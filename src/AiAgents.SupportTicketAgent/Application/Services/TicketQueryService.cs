using AiAgents.SupportTicketAgent.Application.Abstractions;
using AiAgents.SupportTicketAgent.Application.Results;
using AiAgents.SupportTicketAgent.Domain;

namespace AiAgents.SupportTicketAgent.Application.Services;

public sealed class TicketQueryService(ISupportTicketRepository repository)
{
    public async Task<TicketDetails?> GetAsync(long ticketId, CancellationToken cancellationToken)
    {
        var ticket = await repository.GetTicketAsync(ticketId, cancellationToken);
        if (ticket is null)
            return null;

        var prediction = await repository.GetPredictionAsync(ticketId, cancellationToken);
        var review = await repository.GetReviewAsync(ticketId, cancellationToken);
        return new(ticket, prediction, review);
    }

    public Task<IReadOnlyList<SupportTicket>> ListAsync(int limit, CancellationToken cancellationToken) =>
        repository.ListTicketsAsync(Math.Clamp(limit, 1, 200), cancellationToken);
}

