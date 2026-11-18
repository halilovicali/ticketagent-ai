using AiAgents.SupportTicketAgent.Application.Abstractions;
using AiAgents.SupportTicketAgent.Domain;

namespace AiAgents.SupportTicketAgent.Application.Services;

public sealed record TrainingOutcome(
    int VersionNumber,
    double TestAccuracy,
    string ModelPath,
    int TrainingReviewCount,
    long TrainingCutoffReviewId);

public sealed class TrainingService(
    ISupportTicketRepository repository,
    ISupportTicketClassifier classifier,
    IModelPathProvider modelPathProvider)
{
    public async Task<TrainingOutcome?> EnsureActiveModelAsync(CancellationToken cancellationToken)
    {
        var active = await repository.GetActiveModelAsync(cancellationToken);
        return active is null ? await TrainAndActivateAsync(cancellationToken) : null;
    }

    public static (bool ShouldRetrain, string Reason) ShouldRetrain(SystemSettings settings)
    {
        if (!settings.RetrainingEnabled)
            return (false, "Retraining is disabled.");
        if (settings.NewGoldSinceLastTrain < settings.RetrainGoldThreshold)
        {
            return (false,
                $"Waiting for gold labels ({settings.NewGoldSinceLastTrain}/{settings.RetrainGoldThreshold}).");
        }

        return (true, "Gold-label threshold reached.");
    }

    public async Task<TrainingOutcome> TrainAndActivateAsync(CancellationToken cancellationToken)
    {
        // The snapshot freezes the correction cutoff before the long-running ML operation.
        // Corrections arriving afterwards are intentionally left for the next model.
        var snapshot = await repository.CreateTrainingSnapshotAsync(cancellationToken);
        if (snapshot.Examples.Count == 0)
            throw new InvalidOperationException("Training cannot start without examples.");

        var versionNumber = await repository.GetNextModelVersionNumberAsync(cancellationToken);
        var modelPath = modelPathProvider.GetModelDirectory(versionNumber);
        await classifier.TrainAsync(snapshot.Examples, modelPath, cancellationToken);

        var testData = await repository.GetTestDataAsync(cancellationToken);
        var accuracy = await classifier.EvaluateAsync(testData, modelPath, cancellationToken);

        var model = new ModelVersion(
            versionNumber,
            modelPath,
            accuracy,
            snapshot.CutoffReviewId,
            snapshot.ReviewIds.Count);
        await repository.ActivateModelAsync(
            model,
            snapshot.ReviewIds,
            snapshot.PendingCorrectionCount,
            cancellationToken);
        return new(
            versionNumber,
            accuracy,
            modelPath,
            snapshot.ReviewIds.Count,
            snapshot.CutoffReviewId);
    }
}

