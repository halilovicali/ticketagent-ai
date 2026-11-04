namespace AiAgents.SupportTicketAgent.Domain;

public sealed class Review
{
    private Review() { }

    public Review(long ticketId, TicketCategory goldCategory, TicketPriority goldPriority, string reviewer)
    {
        if (string.IsNullOrWhiteSpace(reviewer))
            throw new ArgumentException("Reviewer is required.", nameof(reviewer));

        TicketId = ticketId;
        GoldCategory = goldCategory;
        GoldPriority = goldPriority;
        Reviewer = reviewer.Trim();
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public long Id { get; private set; }
    public long TicketId { get; private set; }
    public TicketCategory GoldCategory { get; private set; }
    public TicketPriority GoldPriority { get; private set; }
    public string Reviewer { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public SupportTicket? Ticket { get; private set; }
}

