namespace AiAgents.SupportTicketAgent.Application.Results;

public sealed record RetrainTickResult(
    bool Trained,
    string Reason,
    int? ModelVersion = null,
    double? TestAccuracy = null);

