namespace AiAgents.SupportTicketAgent.Domain;

public enum TicketStatus
{
    Queued = 1,
    Processing = 2,
    Routed = 3,
    PendingReview = 4,
    Failed = 5
}

