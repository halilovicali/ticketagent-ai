using AiAgents.SupportTicketAgent.Application.Abstractions;

namespace AiAgents.SupportTicketAgent.Application.Services;

public sealed class ScoringService(ISupportTicketClassifier classifier)
{
    public Task<ClassifierOutput> ScoreAsync(
        string text,
        string modelPath,
        CancellationToken cancellationToken) =>
        classifier.PredictAsync(text, modelPath, cancellationToken);
}

