using System.ComponentModel.DataAnnotations;
using AiAgents.SupportTicketAgent.Application.Results;
using AiAgents.SupportTicketAgent.Domain;

namespace AiAgents.SupportTicketAgent.Web.Contracts;

public sealed class CreateTicketRequest
{
    [Required, StringLength(200)]
    public string Subject { get; init; } = string.Empty;

    [Required, StringLength(10_000)]
    public string Description { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(320)]
    public string RequesterEmail { get; init; } = string.Empty;
}

public sealed class AddReviewRequest
{
    [Required]
    public TicketCategory Category { get; init; }

    [Required]
    public TicketPriority Priority { get; init; }

    [Required, StringLength(100)]
    public string Reviewer { get; init; } = string.Empty;
}

public sealed record TicketSummaryResponse(
    long Id,
    string Subject,
    string RequesterEmail,
    TicketStatus Status,
    string? AssignedTeam,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public static TicketSummaryResponse From(SupportTicket ticket) => new(
        ticket.Id,
        ticket.Subject,
        ticket.RequesterEmail,
        ticket.Status,
        ticket.AssignedTeam,
        ticket.CreatedAtUtc,
        ticket.UpdatedAtUtc);
}

public sealed record PredictionResponse(
    TicketCategory Category,
    TicketPriority Priority,
    float CategoryConfidence,
    float PriorityConfidence,
    string AssignedTeam,
    int ModelVersionId,
    DateTimeOffset CreatedAtUtc);

public sealed record ReviewResponse(
    TicketCategory GoldCategory,
    TicketPriority GoldPriority,
    string Reviewer,
    DateTimeOffset CreatedAtUtc);

public sealed record TicketResponse(
    long Id,
    string Subject,
    string Description,
    string RequesterEmail,
    TicketStatus Status,
    string? AssignedTeam,
    string? FailureReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    PredictionResponse? Prediction,
    ReviewResponse? Review)
{
    public static TicketResponse From(TicketDetails details)
    {
        var ticket = details.Ticket;
        return new TicketResponse(
            ticket.Id,
            ticket.Subject,
            ticket.Description,
            ticket.RequesterEmail,
            ticket.Status,
            ticket.AssignedTeam,
            ticket.FailureReason,
            ticket.CreatedAtUtc,
            ticket.UpdatedAtUtc,
            details.Prediction is null
                ? null
                : new PredictionResponse(
                    details.Prediction.Category,
                    details.Prediction.Priority,
                    details.Prediction.CategoryConfidence,
                    details.Prediction.PriorityConfidence,
                    details.Prediction.AssignedTeam,
                    details.Prediction.ModelVersionId,
                    details.Prediction.CreatedAtUtc),
            details.Review is null
                ? null
                : new ReviewResponse(
                    details.Review.GoldCategory,
                    details.Review.GoldPriority,
                    details.Review.Reviewer,
                    details.Review.CreatedAtUtc));
    }
}

