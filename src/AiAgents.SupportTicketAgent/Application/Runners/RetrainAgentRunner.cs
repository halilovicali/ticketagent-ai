using AiAgents.Core;
using AiAgents.SupportTicketAgent.Application.Abstractions;
using AiAgents.SupportTicketAgent.Application.Results;
using AiAgents.SupportTicketAgent.Application.Services;
using AiAgents.SupportTicketAgent.Domain;

namespace AiAgents.SupportTicketAgent.Application.Runners;

public sealed record RetrainPercept(SystemSettings Settings, ModelVersion? ActiveModel);
public sealed record RetrainAction(bool ShouldTrain, string Reason);
public sealed record RetrainExperience(int ModelVersion, double TestAccuracy);

/// <summary>
/// Performs one retraining check and, when needed, one bounded training operation.
/// </summary>
public sealed class RetrainAgentRunner(
    ISupportTicketRepository repository,
    TrainingService trainingService)
    : SoftwareAgent<RetrainPercept, RetrainAction, RetrainTickResult, RetrainExperience>
{
    public override async Task<RetrainTickResult?> StepAsync(CancellationToken cancellationToken)
    {
        // SENSE: observe settings, gold-label counter and current model state.
        var settings = await repository.GetSettingsAsync(cancellationToken);
        var activeModel = await repository.GetActiveModelAsync(cancellationToken);
        var percept = new RetrainPercept(settings, activeModel);

        // THINK: apply the shared retraining policy.
        var (shouldTrain, reason) = TrainingService.ShouldRetrain(percept.Settings);
        var action = new RetrainAction(shouldTrain, reason);
        if (!action.ShouldTrain)
            return new RetrainTickResult(false, action.Reason);

        // ACT: train a versioned model and activate it atomically in persistence.
        var outcome = await trainingService.TrainAndActivateAsync(cancellationToken);

        // LEARN: activation resets the gold counter; the new model is retained experience.
        _ = new RetrainExperience(outcome.VersionNumber, outcome.TestAccuracy);
        return new RetrainTickResult(
            true,
            action.Reason,
            outcome.VersionNumber,
            outcome.TestAccuracy);
    }
}

