using AiAgents.SupportTicketAgent.Application.Abstractions;
using AiAgents.SupportTicketAgent.Domain;
using Microsoft.EntityFrameworkCore;

namespace AiAgents.SupportTicketAgent.Infrastructure;

public sealed class SupportTicketRepository(SupportTicketDbContext dbContext)
    : ISupportTicketRepository
{
    public async Task<SupportTicket> AddTicketAsync(
        SupportTicket ticket,
        CancellationToken cancellationToken)
    {
        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ticket;
    }

    public async Task<SupportTicket?> ClaimNextQueuedAsync(CancellationToken cancellationToken)
    {
        // Compare-and-swap makes dequeue safe when multiple workers race for one ticket.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var id = await dbContext.Tickets
                .AsNoTracking()
                .Where(x => x.Status == TicketStatus.Queued)
                .OrderBy(x => x.Id)
                .Select(x => (long?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (id is null)
                return null;

            var updatedAt = DateTimeOffset.UtcNow;
            var affected = await dbContext.Tickets
                .Where(x => x.Id == id && x.Status == TicketStatus.Queued)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, TicketStatus.Processing)
                    .SetProperty(x => x.UpdatedAtUtc, updatedAt), cancellationToken);
            if (affected == 1)
            {
                // ExecuteUpdate bypasses tracked objects; clear stale queue state before loading the claim.
                dbContext.ChangeTracker.Clear();
                return await dbContext.Tickets.SingleAsync(x => x.Id == id, cancellationToken);
            }
        }

        return null;
    }

    public Task<SupportTicket?> GetTicketAsync(long ticketId, CancellationToken cancellationToken) =>
        dbContext.Tickets.FirstOrDefaultAsync(x => x.Id == ticketId, cancellationToken);

    public async Task<IReadOnlyList<SupportTicket>> ListTicketsAsync(
        int limit,
        CancellationToken cancellationToken) =>
        await dbContext.Tickets
            .AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task<Prediction?> GetPredictionAsync(long ticketId, CancellationToken cancellationToken) =>
        dbContext.Predictions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TicketId == ticketId, cancellationToken);

    public Task<Review?> GetReviewAsync(long ticketId, CancellationToken cancellationToken) =>
        dbContext.Reviews.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TicketId == ticketId, cancellationToken);

    public async Task CompleteTriageAsync(
        SupportTicket ticket,
        Prediction prediction,
        TicketStatus status,
        string assignedTeam,
        CancellationToken cancellationToken)
    {
        ticket.CompleteTriage(status, assignedTeam);
        dbContext.Predictions.Add(prediction);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(
        long ticketId,
        string reason,
        CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets.FindAsync([ticketId], cancellationToken);
        if (ticket is null)
            return;
        ticket.MarkFailed(reason);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Review> AddReviewAsync(
        long ticketId,
        TicketCategory category,
        TicketPriority priority,
        string reviewer,
        string assignedTeam,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Reviews.AnyAsync(x => x.TicketId == ticketId, cancellationToken))
            throw new InvalidOperationException("This ticket already has a gold-label review.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var ticket = await dbContext.Tickets.SingleAsync(x => x.Id == ticketId, cancellationToken);
        var settings = await dbContext.Settings.SingleAsync(x => x.Id == 1, cancellationToken);
        var review = new Review(ticketId, category, priority, reviewer);

        ticket.ApplyHumanReview(assignedTeam);
        settings.RecordGoldLabel();
        dbContext.Reviews.Add(review);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return review;
    }

    public Task<SystemSettings> GetSettingsAsync(CancellationToken cancellationToken) =>
        dbContext.Settings.SingleAsync(x => x.Id == 1, cancellationToken);

    public async Task<SystemSettings> UpdateSettingsAsync(
        float autoRouteThreshold,
        int retrainGoldThreshold,
        bool retrainingEnabled,
        CancellationToken cancellationToken)
    {
        var settings = await dbContext.Settings.SingleAsync(x => x.Id == 1, cancellationToken);
        settings.Update(autoRouteThreshold, retrainGoldThreshold, retrainingEnabled);
        await dbContext.SaveChangesAsync(cancellationToken);
        return settings;
    }

    public async Task<IReadOnlyList<TrainingExampleData>> GetTrainingDataAsync(
        DatasetSplit split,
        bool includeGoldLabels,
        CancellationToken cancellationToken)
    {
        var examples = await dbContext.TrainingExamples
            .AsNoTracking()
            .Where(x => x.Split == split)
            .Select(x => new TrainingExampleData(x.Text, x.Category, x.Priority))
            .ToListAsync(cancellationToken);

        if (includeGoldLabels)
        {
            var goldLabels = await dbContext.Reviews
                .AsNoTracking()
                .Join(
                    dbContext.Tickets.AsNoTracking(),
                    review => review.TicketId,
                    ticket => ticket.Id,
                    (review, ticket) => new TrainingExampleData(
                        ticket.Subject + " " + ticket.Description,
                        review.GoldCategory,
                        review.GoldPriority))
                .ToListAsync(cancellationToken);
            examples.AddRange(goldLabels);
        }

        return examples;
    }

    public Task<ModelVersion?> GetActiveModelAsync(CancellationToken cancellationToken) =>
        dbContext.ModelVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.IsActive, cancellationToken);

    public async Task<IReadOnlyList<ModelVersion>> ListModelsAsync(CancellationToken cancellationToken) =>
        await dbContext.ModelVersions.AsNoTracking()
            .OrderByDescending(x => x.VersionNumber)
            .ToListAsync(cancellationToken);

    public async Task<int> GetNextModelVersionNumberAsync(CancellationToken cancellationToken)
    {
        var current = await dbContext.ModelVersions
            .MaxAsync(x => (int?)x.VersionNumber, cancellationToken);
        return (current ?? 0) + 1;
    }

    public async Task<ModelVersion> ActivateModelAndResetCounterAsync(
        ModelVersion model,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var activeModels = await dbContext.ModelVersions
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var active in activeModels)
            active.Deactivate();

        model.Activate();
        dbContext.ModelVersions.Add(model);
        var settings = await dbContext.Settings.SingleAsync(x => x.Id == 1, cancellationToken);
        settings.ResetGoldCounter();
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return model;
    }
}

