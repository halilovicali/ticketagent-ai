namespace AiAgents.SupportTicketAgent.Domain;

public sealed class Prediction
{
    private Prediction() { }

    public Prediction(
        long ticketId,
        TicketCategory category,
        TicketPriority priority,
        float categoryConfidence,
        float priorityConfidence,
        string assignedTeam,
        int modelVersionId)
    {
        if (categoryConfidence is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(categoryConfidence));
        if (priorityConfidence is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(priorityConfidence));

        TicketId = ticketId;
        Category = category;
        Priority = priority;
        CategoryConfidence = categoryConfidence;
        PriorityConfidence = priorityConfidence;
        AssignedTeam = assignedTeam;
        ModelVersionId = modelVersionId;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public long Id { get; private set; }
    public long TicketId { get; private set; }
    public TicketCategory Category { get; private set; }
    public TicketPriority Priority { get; private set; }
    public float CategoryConfidence { get; private set; }
    public float PriorityConfidence { get; private set; }
    public string AssignedTeam { get; private set; } = string.Empty;
    public int ModelVersionId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public SupportTicket? Ticket { get; private set; }
    public ModelVersion? ModelVersion { get; private set; }
}

