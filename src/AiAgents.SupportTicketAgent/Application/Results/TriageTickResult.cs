using AiAgents.SupportTicketAgent.Domain;

namespace AiAgents.SupportTicketAgent.Application.Results;

public sealed record TriageTickResult(
    long TicketId,
    TicketCategory Category,
    TicketPriority Priority,
    float CategoryConfidence,
    float PriorityConfidence,
    TicketStatus NewStatus,
    string AssignedTeam,
    int ModelVersion);

