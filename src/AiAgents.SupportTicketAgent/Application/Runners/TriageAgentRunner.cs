using AiAgents.Core;
using AiAgents.SupportTicketAgent.Application.Abstractions;
using AiAgents.SupportTicketAgent.Application.Results;
using AiAgents.SupportTicketAgent.Application.Services;
using AiAgents.SupportTicketAgent.Domain;

namespace AiAgents.SupportTicketAgent.Application.Runners;

public sealed record TriagePercept(
    SupportTicket Ticket,
    SystemSettings Settings,
    ModelVersion ActiveModel);

public sealed record TriageAction(
    TriagePercept Percept,
    ClassifierOutput ClassifierOutput,
    TriageDecision Decision);

public sealed record TriageExperience(long TicketId);

/// <summary>
/// Processes exactly one queued ticket per step.
/// All Sense/Think/Act decisions live here in the shared module.
/// </summary>
public sealed class TriageAgentRunner(
    QueueService queueService,
    ScoringService scoringService,
    TriagePolicy policy,
    ISupportTicketRepository repository)
    : SoftwareAgent<TriagePercept, TriageAction, TriageTickResult, TriageExperience>
{
    public override async Task<TriageTickResult?> StepAsync(CancellationToken cancellationToken)
    {
        // SENSE: atomically claim one queued ticket and observe current world state.
        var ticket = await queueService.DequeueAsync(cancellationToken);
        if (ticket is null)
            return null;

        try
        {
            var settings = await repository.GetSettingsAsync(cancellationToken);
            var activeModel = await repository.GetActiveModelAsync(cancellationToken)
                ?? throw new InvalidOperationException("No active model is available.");
            var percept = new TriagePercept(ticket, settings, activeModel);

            // THINK: score the text and decide whether to route or request human review.
            var output = await scoringService.ScoreAsync(
                percept.Ticket.GetText(),
                percept.ActiveModel.FilePath,
                cancellationToken);
            var decision = policy.Decide(
                output.Category,
                output.Priority,
                output.CategoryConfidence,
                output.PriorityConfidence,
                percept.Settings);
            var action = new TriageAction(percept, output, decision);

            // ACT: persist prediction and status together as one consistent change.
            var prediction = new Prediction(
                action.Percept.Ticket.Id,
                action.Decision.Category,
                action.Decision.Priority,
                action.ClassifierOutput.CategoryConfidence,
                action.ClassifierOutput.PriorityConfidence,
                action.Decision.AssignedTeam,
                action.Percept.ActiveModel.Id);
            await repository.CompleteTriageAsync(
                action.Percept.Ticket,
                prediction,
                action.Decision.Status,
                action.Decision.AssignedTeam,
                cancellationToken);

            return new TriageTickResult(
                ticket.Id,
                decision.Category,
                decision.Priority,
                output.CategoryConfidence,
                output.PriorityConfidence,
                decision.Status,
                decision.AssignedTeam,
                activeModel.VersionNumber);
        }
        catch (Exception exception)
        {
            await repository.MarkFailedAsync(ticket.Id, exception.Message, cancellationToken);
            throw;
        }
    }
}

