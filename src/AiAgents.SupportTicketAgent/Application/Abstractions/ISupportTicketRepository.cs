using AiAgents.SupportTicketAgent.Domain;

namespace AiAgents.SupportTicketAgent.Application.Abstractions;

public interface ISupportTicketRepository
{
    Task<SupportTicket> AddTicketAsync(SupportTicket ticket, CancellationToken cancellationToken);
    Task<SupportTicket?> ClaimNextQueuedAsync(CancellationToken cancellationToken);
    Task<SupportTicket?> GetTicketAsync(long ticketId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SupportTicket>> ListTicketsAsync(int limit, CancellationToken cancellationToken);
    Task<Prediction?> GetPredictionAsync(long ticketId, CancellationToken cancellationToken);
    Task<Review?> GetReviewAsync(long ticketId, CancellationToken cancellationToken);
    Task CompleteTriageAsync(
        SupportTicket ticket,
        Prediction prediction,
        TicketStatus status,
        string assignedTeam,
        CancellationToken cancellationToken);
    Task MarkFailedAsync(long ticketId, string reason, CancellationToken cancellationToken);
    Task<Review> AddReviewAsync(
        long ticketId,
        TicketCategory category,
        TicketPriority priority,
        string reviewer,
        string assignedTeam,
        CancellationToken cancellationToken);
    Task<SystemSettings> GetSettingsAsync(CancellationToken cancellationToken);
    Task<SystemSettings> UpdateSettingsAsync(
        float autoRouteThreshold,
        int retrainGoldThreshold,
        bool retrainingEnabled,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<TrainingExampleData>> GetTrainingDataAsync(
        DatasetSplit split,
        bool includeGoldLabels,
        CancellationToken cancellationToken);
    Task<ModelVersion?> GetActiveModelAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ModelVersion>> ListModelsAsync(CancellationToken cancellationToken);
    Task<int> GetNextModelVersionNumberAsync(CancellationToken cancellationToken);
    Task<ModelVersion> ActivateModelAndResetCounterAsync(
        ModelVersion model,
        CancellationToken cancellationToken);
}

public sealed record TrainingExampleData(
    string Text,
    TicketCategory Category,
    TicketPriority Priority);

