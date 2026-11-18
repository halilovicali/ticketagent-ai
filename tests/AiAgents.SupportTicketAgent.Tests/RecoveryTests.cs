using AiAgents.SupportTicketAgent.Application.Runners;
using AiAgents.SupportTicketAgent.Application.Services;
using AiAgents.SupportTicketAgent.Domain;
using Microsoft.EntityFrameworkCore;

namespace AiAgents.SupportTicketAgent.Tests;

public sealed class RecoveryTests
{
    [Fact]
    public async Task ClaimWrittenButReadFails_TicketReturnsToQueueConsistently()
    {
        await using var database = await TestDatabase.CreateAsync();
        var ticket = await database.Repository.AddTicketAsync(
            new SupportTicket("Read recovery", "Simulate read after claim failure", "read@example.com"),
            CancellationToken.None);
        var failingRepository = new FailingReadRepository(database.DbContext);

        await Assert.ThrowsAsync<IOException>(() =>
            failingRepository.ClaimNextQueuedAsync(CancellationToken.None));

        database.DbContext.ChangeTracker.Clear();
        var recovered = await database.Repository.GetTicketAsync(ticket.Id, CancellationToken.None);
        Assert.NotNull(recovered);
        Assert.Equal(TicketStatus.Queued, recovered.Status);
        Assert.Equal(1, recovered.ProcessingAttemptCount);
        Assert.Null(recovered.ClaimedAtUtc);

        var nextClaim = await database.Repository.ClaimNextQueuedAsync(CancellationToken.None);
        Assert.NotNull(nextClaim);
        Assert.Equal(TicketStatus.Processing, nextClaim.Status);
        Assert.Equal(2, nextClaim.ProcessingAttemptCount);
    }

    [Fact]
    public async Task RepeatedClaimReadFailure_StopsAfterThreeAttempts()
    {
        await using var database = await TestDatabase.CreateAsync();
        var ticket = await database.Repository.AddTicketAsync(
            new SupportTicket("Repeated failure", "Read always fails", "retry@example.com"),
            CancellationToken.None);
        var failingRepository = new FailingReadRepository(database.DbContext);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await Assert.ThrowsAsync<IOException>(() =>
                failingRepository.ClaimNextQueuedAsync(CancellationToken.None));
        }

        database.DbContext.ChangeTracker.Clear();
        var stopped = await database.Repository.GetTicketAsync(ticket.Id, CancellationToken.None);
        Assert.NotNull(stopped);
        Assert.Equal(TicketStatus.Failed, stopped.Status);
        Assert.Equal(3, stopped.ProcessingAttemptCount);
        Assert.Null(await failingRepository.ClaimNextQueuedAsync(CancellationToken.None));
    }

    [Fact]
    public async Task FinalPredictionWriteFails_TicketIsFailedAndWriteIsNotRetried()
    {
        await using var database = await TestDatabase.CreateAsync();
        await TestData.ActivateModelAsync(database);
        var queue = new QueueService(database.Repository);
        var ticket = await queue.EnqueueAsync(
            "Write recovery",
            "Force prediction insert failure",
            "write@example.com",
            CancellationToken.None);
        await database.DbContext.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER FailPrediction BEFORE INSERT ON Predictions " +
            "BEGIN SELECT RAISE(ABORT, 'simulated final write failure'); END;");
        var runner = new TriageAgentRunner(
            queue,
            new ScoringService(new FakeClassifier()),
            new TriagePolicy(),
            database.Repository);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            runner.StepAsync(CancellationToken.None));

        database.DbContext.ChangeTracker.Clear();
        var failed = await database.Repository.GetTicketAsync(ticket.Id, CancellationToken.None);
        Assert.NotNull(failed);
        Assert.Equal(TicketStatus.Failed, failed.Status);
        Assert.Null(await database.Repository.GetPredictionAsync(ticket.Id, CancellationToken.None));

        // Failed is terminal: a later tick cannot retry the same prediction insert.
        Assert.Null(await runner.StepAsync(CancellationToken.None));
    }
}
