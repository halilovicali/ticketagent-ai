using AiAgents.SupportTicketAgent.Application.Abstractions;
using AiAgents.SupportTicketAgent.Domain;
using Microsoft.EntityFrameworkCore;

namespace AiAgents.SupportTicketAgent.Infrastructure;

public class SupportTicketRepository : ISupportTicketRepository
{
    private const int MaxProcessingAttempts = 3;
    protected SupportTicketDbContext DbContext { get; }

    public SupportTicketRepository(SupportTicketDbContext dbContext)
    {
        DbContext = dbContext;
    }

    public async Task<SupportTicket> AddTicketAsync(
        SupportTicket ticket,
        CancellationToken cancellationToken)
    {
        DbContext.Tickets.Add(ticket);
        await DbContext.SaveChangesAsync(cancellationToken);
        return ticket;
    }

    public async Task<SupportTicket?> ClaimNextQueuedAsync(CancellationToken cancellationToken)
    {
        // Compare-and-swap makes dequeue safe when multiple workers race for one ticket.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var id = await DbContext.Tickets
                .AsNoTracking()
                .Where(x => x.Status == TicketStatus.Queued)
                .OrderBy(x => x.Id)
                .Select(x => (long?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (id is null)
                return null;

            var updatedAt = DateTimeOffset.UtcNow;
            var affected = await DbContext.Tickets
                .Where(x => x.Id == id && x.Status == TicketStatus.Queued)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, TicketStatus.Processing)
                    .SetProperty(x => x.ProcessingAttemptCount, x => x.ProcessingAttemptCount + 1)
                    .SetProperty(x => x.ClaimedAtUtc, updatedAt)
                    .SetProperty(x => x.FailureReason, (string?)null)
                    .SetProperty(x => x.UpdatedAtUtc, updatedAt), cancellationToken);
            if (affected == 1)
            {
                try
                {
                    // ExecuteUpdate bypasses tracked objects; clear stale queue state before loading the claim.
                    DbContext.ChangeTracker.Clear();
                    return await LoadClaimedTicketAsync(id.Value, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    await RecoverAfterClaimReadFailureAsync(id.Value, exception.Message, cancellationToken);
                    throw;
                }
            }
        }

        return null;
    }

    public Task<SupportTicket?> GetTicketAsync(long ticketId, CancellationToken cancellationToken) =>
        DbContext.Tickets.FirstOrDefaultAsync(x => x.Id == ticketId, cancellationToken);

    public async Task<IReadOnlyList<SupportTicket>> ListTicketsAsync(
        int limit,
        CancellationToken cancellationToken) =>
        await DbContext.Tickets
            .AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task<Prediction?> GetPredictionAsync(long ticketId, CancellationToken cancellationToken) =>
        DbContext.Predictions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TicketId == ticketId, cancellationToken);

    public Task<Review?> GetReviewAsync(long ticketId, CancellationToken cancellationToken) =>
        DbContext.Reviews.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TicketId == ticketId, cancellationToken);

    public async Task CompleteTriageAsync(
        SupportTicket ticket,
        Prediction prediction,
        TicketStatus status,
        string assignedTeam,
        CancellationToken cancellationToken)
    {
        ticket.CompleteTriage(status, assignedTeam);
        DbContext.Predictions.Add(prediction);
        await DbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(
        long ticketId,
        string reason,
        CancellationToken cancellationToken)
    {
        // A failed final SaveChanges may leave stale Routed state and a pending Prediction
        // in the tracker. Clear it so recovery never retries the same failed insert.
        DbContext.ChangeTracker.Clear();
        var failedAt = DateTimeOffset.UtcNow;
        await DbContext.Tickets
            .Where(x => x.Id == ticketId && x.Status == TicketStatus.Processing)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, TicketStatus.Failed)
                .SetProperty(x => x.FailureReason,
                    string.IsNullOrWhiteSpace(reason) ? "Unknown processing error" : reason)
                .SetProperty(x => x.ClaimedAtUtc, (DateTimeOffset?)null)
                .SetProperty(x => x.UpdatedAtUtc, failedAt), cancellationToken);
    }

    public async Task<Review> AddReviewAsync(
        long ticketId,
        TicketCategory category,
        TicketPriority priority,
        string reviewer,
        string assignedTeam,
        CancellationToken cancellationToken)
    {
        if (await DbContext.Reviews.AnyAsync(x => x.TicketId == ticketId, cancellationToken))
            throw new InvalidOperationException("This ticket already has a gold-label review.");

        await using var transaction = await DbContext.Database.BeginTransactionAsync(cancellationToken);
        var ticket = await DbContext.Tickets.SingleAsync(x => x.Id == ticketId, cancellationToken);
        var settings = await DbContext.Settings.SingleAsync(x => x.Id == 1, cancellationToken);
        var review = new Review(ticketId, category, priority, reviewer);

        ticket.ApplyHumanReview(assignedTeam);
        settings.RecordGoldLabel();
        DbContext.Reviews.Add(review);
        await DbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return review;
    }

    public Task<SystemSettings> GetSettingsAsync(CancellationToken cancellationToken) =>
        DbContext.Settings.SingleAsync(x => x.Id == 1, cancellationToken);

    public async Task<SystemSettings> UpdateSettingsAsync(
        float autoRouteThreshold,
        int retrainGoldThreshold,
        bool retrainingEnabled,
        CancellationToken cancellationToken)
    {
        var settings = await DbContext.Settings.SingleAsync(x => x.Id == 1, cancellationToken);
        settings.Update(autoRouteThreshold, retrainGoldThreshold, retrainingEnabled);
        await DbContext.SaveChangesAsync(cancellationToken);
        return settings;
    }

    public async Task<TrainingSnapshot> CreateTrainingSnapshotAsync(
        CancellationToken cancellationToken)
    {
        var cutoffReviewId = await DbContext.Reviews
            .AsNoTracking()
            .MaxAsync(x => (long?)x.Id, cancellationToken) ?? 0;

        var examples = await DbContext.TrainingExamples
            .AsNoTracking()
            .Where(x => x.Split == DatasetSplit.Train)
            .Select(x => new TrainingExampleData(x.Text, x.Category, x.Priority))
            .ToListAsync(cancellationToken);

        var reviews = await DbContext.Reviews
            .AsNoTracking()
            .Where(x => x.Id <= cutoffReviewId)
            .Join(
                DbContext.Tickets.AsNoTracking(),
                review => review.TicketId,
                ticket => ticket.Id,
                (review, ticket) => new
                {
                    review.Id,
                    Example = new TrainingExampleData(
                        ticket.Subject + " " + ticket.Description,
                        review.GoldCategory,
                        review.GoldPriority)
                })
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
        examples.AddRange(reviews.Select(x => x.Example));

        var activeModelId = await DbContext.ModelVersions
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => (int?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
        var alreadyUsed = activeModelId is null
            ? new HashSet<long>()
            : (await DbContext.ModelTrainingReviews
                .AsNoTracking()
                .Where(x => x.ModelVersionId == activeModelId)
                .Select(x => x.ReviewId)
                .ToListAsync(cancellationToken))
                .ToHashSet();
        var reviewIds = reviews.Select(x => x.Id).ToArray();
        var pendingCount = reviewIds.Count(id => !alreadyUsed.Contains(id));

        return new TrainingSnapshot(examples, reviewIds, pendingCount, cutoffReviewId);
    }

    public async Task<IReadOnlyList<TrainingExampleData>> GetTestDataAsync(
        CancellationToken cancellationToken) =>
        await DbContext.TrainingExamples
            .AsNoTracking()
            .Where(x => x.Split == DatasetSplit.Test)
            .Select(x => new TrainingExampleData(x.Text, x.Category, x.Priority))
            .ToListAsync(cancellationToken);

    public Task<ModelVersion?> GetActiveModelAsync(CancellationToken cancellationToken) =>
        DbContext.ModelVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.IsActive, cancellationToken);

    public async Task<IReadOnlyList<ModelVersion>> ListModelsAsync(CancellationToken cancellationToken) =>
        await DbContext.ModelVersions.AsNoTracking()
            .OrderByDescending(x => x.VersionNumber)
            .ToListAsync(cancellationToken);

    public async Task<int> GetNextModelVersionNumberAsync(CancellationToken cancellationToken)
    {
        var current = await DbContext.ModelVersions
            .MaxAsync(x => (int?)x.VersionNumber, cancellationToken);
        return (current ?? 0) + 1;
    }

    public async Task<ModelVersion> ActivateModelAsync(
        ModelVersion model,
        IReadOnlyCollection<long> trainingReviewIds,
        int consumedGoldLabelCount,
        CancellationToken cancellationToken)
    {
        await using var transaction = await DbContext.Database.BeginTransactionAsync(cancellationToken);
        var activeModels = await DbContext.ModelVersions
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var active in activeModels)
            active.Deactivate();

        model.Activate();
        DbContext.ModelVersions.Add(model);
        await DbContext.SaveChangesAsync(cancellationToken);

        DbContext.ModelTrainingReviews.AddRange(
            trainingReviewIds.Select(reviewId => new ModelTrainingReview(model.Id, reviewId)));

        var settings = await DbContext.Settings.SingleAsync(x => x.Id == 1, cancellationToken);
        // A review may have arrived while ML.NET was training in this same scope.
        await DbContext.Entry(settings).ReloadAsync(cancellationToken);
        settings.ConsumeGoldLabels(consumedGoldLabelCount);
        await DbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return model;
    }

    public async Task<IReadOnlyList<ModelTrainingReviewData>> ListModelTrainingReviewsAsync(
        int modelVersionId,
        CancellationToken cancellationToken) =>
        await DbContext.ModelTrainingReviews
            .AsNoTracking()
            .Where(x => x.ModelVersionId == modelVersionId)
            .Join(
                DbContext.Reviews.AsNoTracking(),
                link => link.ReviewId,
                review => review.Id,
                (link, review) => review)
            .OrderBy(review => review.Id)
            .Select(review => new ModelTrainingReviewData(
                review.Id,
                review.TicketId,
                review.GoldCategory,
                review.GoldPriority,
                review.Reviewer,
                review.CreatedAtUtc))
            .ToListAsync(cancellationToken);

    protected virtual Task<SupportTicket> LoadClaimedTicketAsync(
        long ticketId,
        CancellationToken cancellationToken) =>
        DbContext.Tickets.SingleAsync(x => x.Id == ticketId, cancellationToken);

    private async Task RecoverAfterClaimReadFailureAsync(
        long ticketId,
        string reason,
        CancellationToken cancellationToken)
    {
        DbContext.ChangeTracker.Clear();
        var recoveredAt = DateTimeOffset.UtcNow;
        var message = $"Claimed ticket could not be read: {reason}";
        var requeued = await DbContext.Tickets
            .Where(x => x.Id == ticketId &&
                        x.Status == TicketStatus.Processing &&
                        x.ProcessingAttemptCount < MaxProcessingAttempts)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, TicketStatus.Queued)
                .SetProperty(x => x.FailureReason, message)
                .SetProperty(x => x.ClaimedAtUtc, (DateTimeOffset?)null)
                .SetProperty(x => x.UpdatedAtUtc, recoveredAt), cancellationToken);

        if (requeued == 0)
        {
            await DbContext.Tickets
                .Where(x => x.Id == ticketId && x.Status == TicketStatus.Processing)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, TicketStatus.Failed)
                    .SetProperty(x => x.FailureReason, message)
                    .SetProperty(x => x.ClaimedAtUtc, (DateTimeOffset?)null)
                    .SetProperty(x => x.UpdatedAtUtc, recoveredAt), cancellationToken);
        }
    }
}

