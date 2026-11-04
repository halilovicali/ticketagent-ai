using AiAgents.SupportTicketAgent.Domain;

namespace AiAgents.SupportTicketAgent.Application.Abstractions;

public sealed record ClassifierOutput(
    TicketCategory Category,
    TicketPriority Priority,
    float CategoryConfidence,
    float PriorityConfidence);

public interface ISupportTicketClassifier
{
    Task<ClassifierOutput> PredictAsync(
        string text,
        string modelPath,
        CancellationToken cancellationToken);

    Task TrainAsync(
        IReadOnlyCollection<TrainingExampleData> examples,
        string modelPath,
        CancellationToken cancellationToken);

    Task<double> EvaluateAsync(
        IReadOnlyCollection<TrainingExampleData> examples,
        string modelPath,
        CancellationToken cancellationToken);
}

