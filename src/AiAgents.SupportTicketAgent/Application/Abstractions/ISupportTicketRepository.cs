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
    Task<TrainingSnapshot> CreateTrainingSnapshotAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<TrainingExampleData>> GetTestDataAsync(CancellationToken cancellationToken);
    Task<ModelVersion?> GetActiveModelAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ModelVersion>> ListModelsAsync(CancellationToken cancellationToken);
    Task<int> GetNextModelVersionNumberAsync(CancellationToken cancellationToken);
    Task<ModelVersion> ActivateModelAsync(
        ModelVersion model,
        IReadOnlyCollection<long> trainingReviewIds,
        int consumedGoldLabelCount,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<ModelTrainingReviewData>> ListModelTrainingReviewsAsync(
        int modelVersionId,
        CancellationToken cancellationToken);
}

public sealed record TrainingExampleData(
    string Text,
    TicketCategory Category,
    TicketPriority Priority);

public sealed record TrainingSnapshot(
    IReadOnlyList<TrainingExampleData> Examples,
    IReadOnlyList<long> ReviewIds,
    int PendingCorrectionCount,
    long CutoffReviewId);

public sealed record ModelTrainingReviewData(
    long ReviewId,
    long TicketId,
    TicketCategory Category,
    TicketPriority Priority,
    string Reviewer,
    DateTimeOffset CreatedAtUtc);

