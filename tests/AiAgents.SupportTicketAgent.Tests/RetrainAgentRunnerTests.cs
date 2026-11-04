using AiAgents.SupportTicketAgent.Application.Runners;
using AiAgents.SupportTicketAgent.Application.Services;
using AiAgents.SupportTicketAgent.Infrastructure;

namespace AiAgents.SupportTicketAgent.Tests;

public sealed class RetrainAgentRunnerTests
{
    [Fact]
    public async Task ThresholdNotReached_SkipsTraining()
    {
        await using var database = await TestDatabase.CreateAsync();
        var classifier = new FakeClassifier();
        var training = new TrainingService(
            database.Repository,
            classifier,
            new ModelPathProvider(Path.GetTempPath()));
        var runner = new RetrainAgentRunner(database.Repository, training);

        var result = await runner.StepAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result.Trained);
        Assert.Equal(0, classifier.TrainingCalls);
    }

    [Fact]
    public async Task ThresholdReached_TrainsActivatesAndResetsCounter()
    {
        await using var database = await TestDatabase.CreateAsync();
        var settings = await database.Repository.GetSettingsAsync(CancellationToken.None);
        for (var i = 0; i < settings.RetrainGoldThreshold; i++)
            settings.RecordGoldLabel();
        await database.DbContext.SaveChangesAsync();

        var classifier = new FakeClassifier();
        var training = new TrainingService(
            database.Repository,
            classifier,
            new ModelPathProvider(Path.GetTempPath()));
        var runner = new RetrainAgentRunner(database.Repository, training);

        var result = await runner.StepAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Trained);
        Assert.Equal(1, classifier.TrainingCalls);
        Assert.Equal(0,
            (await database.Repository.GetSettingsAsync(CancellationToken.None)).NewGoldSinceLastTrain);
        Assert.NotNull(await database.Repository.GetActiveModelAsync(CancellationToken.None));
    }
}

