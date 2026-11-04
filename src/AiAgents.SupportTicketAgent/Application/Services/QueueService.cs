using AiAgents.SupportTicketAgent.Application.Abstractions;
using AiAgents.SupportTicketAgent.Domain;

namespace AiAgents.SupportTicketAgent.Application.Services;

public sealed class QueueService(ISupportTicketRepository repository)
{
    public Task<SupportTicket> EnqueueAsync(
        string subject,
        string description,
        string requesterEmail,
        CancellationToken cancellationToken)
    {
        var ticket = new SupportTicket(subject, description, requesterEmail);
        return repository.AddTicketAsync(ticket, cancellationToken);
    }

    public Task<SupportTicket?> DequeueAsync(CancellationToken cancellationToken) =>
        repository.ClaimNextQueuedAsync(cancellationToken);
}

