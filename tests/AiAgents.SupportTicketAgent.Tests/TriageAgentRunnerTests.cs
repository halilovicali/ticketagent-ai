using AiAgents.SupportTicketAgent.Application.Runners;
using AiAgents.SupportTicketAgent.Application.Services;
using AiAgents.SupportTicketAgent.Domain;

namespace AiAgents.SupportTicketAgent.Tests;

public sealed class TriageAgentRunnerTests
{
    [Fact]
    public async Task EmptyQueue_ReturnsNoWorkWithoutChangingState()
    {
        await using var database = await TestDatabase.CreateAsync();
        var classifier = new FakeClassifier();
        var runner = CreateRunner(database, classifier);

        var result = await runner.StepAsync(CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(await database.Repository.ListTicketsAsync(10, CancellationToken.None));
    }

    [Fact]
    public async Task OneTick_ProcessesExactlyOneTicketAndPersistsPrediction()
    {
        await using var database = await TestDatabase.CreateAsync();
        await ActivateModelAsync(database);
        var queue = new QueueService(database.Repository);
        var first = await queue.EnqueueAsync(
            "System is down",
            "Every API request fails in production",
            "user@example.com",
            CancellationToken.None);
        var second = await queue.EnqueueAsync(
            "Invoice question",
            "Please explain this charge",
            "other@example.com",
            CancellationToken.None);
        var runner = CreateRunner(database, new FakeClassifier());

        var result = await runner.StepAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(first.Id, result.TicketId);
        Assert.Equal(TicketStatus.Routed, result.NewStatus);
        Assert.NotNull(await database.Repository.GetPredictionAsync(first.Id, CancellationToken.None));
        Assert.Equal(
            TicketStatus.Queued,
            (await database.Repository.GetTicketAsync(second.Id, CancellationToken.None))!.Status);
    }

    private static TriageAgentRunner CreateRunner(TestDatabase database, FakeClassifier classifier) =>
        new(
            new QueueService(database.Repository),
            new ScoringService(classifier),
            new TriagePolicy(),
            database.Repository);

    private static async Task ActivateModelAsync(TestDatabase database)
    {
        var model = new ModelVersion(1, "fake-model", 1);
        await database.Repository.ActivateModelAndResetCounterAsync(model, CancellationToken.None);
    }
}

