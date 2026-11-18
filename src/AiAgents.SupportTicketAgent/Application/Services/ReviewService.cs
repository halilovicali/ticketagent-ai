using AiAgents.SupportTicketAgent.Application.Abstractions;
using AiAgents.SupportTicketAgent.Domain;

namespace AiAgents.SupportTicketAgent.Application.Services;

public sealed class ReviewService(ISupportTicketRepository repository)
{
    public async Task<Review> AddGoldLabelAsync(
        long ticketId,
        TicketCategory category,
        TicketPriority priority,
        string reviewer,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(category))
            throw new ArgumentOutOfRangeException(nameof(category), "Category is not valid.");
        if (!Enum.IsDefined(priority))
            throw new ArgumentOutOfRangeException(nameof(priority), "Priority is not valid.");
        if (string.IsNullOrWhiteSpace(reviewer))
            throw new ArgumentException("Reviewer is required.", nameof(reviewer));

        var ticket = await repository.GetTicketAsync(ticketId, cancellationToken)
            ?? throw new KeyNotFoundException($"Ticket {ticketId} was not found.");

        if (ticket.Status is not (TicketStatus.PendingReview or TicketStatus.Routed))
            throw new InvalidOperationException("Only processed tickets can receive a gold label.");

        return await repository.AddReviewAsync(
            ticketId,
            category,
            priority,
            reviewer.Trim(),
            TriagePolicy.TeamFor(category),
            cancellationToken);
    }
}

