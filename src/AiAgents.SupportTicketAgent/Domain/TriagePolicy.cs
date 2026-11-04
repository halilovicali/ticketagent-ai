namespace AiAgents.SupportTicketAgent.Domain;

public sealed record TriageDecision(
    TicketCategory Category,
    TicketPriority Priority,
    TicketStatus Status,
    string AssignedTeam,
    float Confidence);

public sealed class TriagePolicy
{
    public const string ManualTriageTeam = "Manual Triage";

    public TriageDecision Decide(
        TicketCategory category,
        TicketPriority priority,
        float categoryConfidence,
        float priorityConfidence,
        SystemSettings settings)
    {
        var confidence = Math.Min(categoryConfidence, priorityConfidence);
        if (confidence < settings.AutoRouteThreshold)
        {
            return new(category, priority, TicketStatus.PendingReview, ManualTriageTeam, confidence);
        }

        return new(category, priority, TicketStatus.Routed, TeamFor(category), confidence);
    }

    public static string TeamFor(TicketCategory category) => category switch
    {
        TicketCategory.Technical => "Technical Support",
        TicketCategory.Billing => "Finance Support",
        TicketCategory.AccountAccess => "Account Services",
        TicketCategory.FeatureRequest => "Product Team",
        TicketCategory.General => "Customer Care",
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };
}

