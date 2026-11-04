namespace AiAgents.SupportTicketAgent.Domain;

public sealed class SupportTicket
{
    private SupportTicket() { }

    public SupportTicket(string subject, string description, string requesterEmail)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Subject is required.", nameof(subject));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));
        if (string.IsNullOrWhiteSpace(requesterEmail) || !requesterEmail.Contains('@'))
            throw new ArgumentException("A valid requester email is required.", nameof(requesterEmail));

        Subject = subject.Trim();
        Description = description.Trim();
        RequesterEmail = requesterEmail.Trim();
        Status = TicketStatus.Queued;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public long Id { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string RequesterEmail { get; private set; } = string.Empty;
    public TicketStatus Status { get; private set; }
    public string? AssignedTeam { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public string GetText() => $"{Subject} {Description}";

    public void MarkProcessing()
    {
        EnsureStatus(TicketStatus.Queued);
        Status = TicketStatus.Processing;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void CompleteTriage(TicketStatus newStatus, string assignedTeam)
    {
        EnsureStatus(TicketStatus.Processing);
        if (newStatus is not (TicketStatus.Routed or TicketStatus.PendingReview))
            throw new ArgumentOutOfRangeException(nameof(newStatus), "Invalid triage result status.");
        if (string.IsNullOrWhiteSpace(assignedTeam))
            throw new ArgumentException("Assigned team is required.", nameof(assignedTeam));

        Status = newStatus;
        AssignedTeam = assignedTeam;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void ApplyHumanReview(string assignedTeam)
    {
        if (Status is not (TicketStatus.PendingReview or TicketStatus.Routed))
            throw new InvalidOperationException($"Cannot review a ticket with status {Status}.");
        AssignedTeam = assignedTeam;
        Status = TicketStatus.Routed;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string reason)
    {
        if (Status != TicketStatus.Processing)
            return;
        Status = TicketStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "Unknown processing error" : reason;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private void EnsureStatus(TicketStatus expected)
    {
        if (Status != expected)
            throw new InvalidOperationException($"Expected ticket status {expected}, but was {Status}.");
    }
}

