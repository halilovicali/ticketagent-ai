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
        await TestData.ActivateModelAsync(database);
        for (var i = 0; i < 5; i++)
            await TestData.AddReviewedTicketAsync(database, i);

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

    [Fact]
    public async Task CorrectionArrivingDuringTraining_RemainsForNextModel()
    {
        await using var database = await TestDatabase.CreateAsync();
        await TestData.ActivateModelAsync(database);
        for (var i = 0; i < 5; i++)
            await TestData.AddReviewedTicketAsync(database, i);

        var classifier = new FakeClassifier(onTrain: async _ =>
            await TestData.AddReviewedTicketAsync(database, 99));
        var training = new TrainingService(
            database.Repository,
            classifier,
            new ModelPathProvider(Path.GetTempPath()));

        var outcome = await training.TrainAndActivateAsync(CancellationToken.None);

        Assert.Equal(2, outcome.VersionNumber);
        Assert.Equal(5, outcome.TrainingReviewCount);
        Assert.Equal(5, outcome.TrainingCutoffReviewId);
        var settings = await database.Repository.GetSettingsAsync(CancellationToken.None);
        Assert.Equal(1, settings.NewGoldSinceLastTrain);
        var active = await database.Repository.GetActiveModelAsync(CancellationToken.None);
        Assert.NotNull(active);
        Assert.Equal(5, active.TrainingReviewCount);
        Assert.Equal(5,
            (await database.Repository.ListModelTrainingReviewsAsync(active.Id, CancellationToken.None)).Count);
        Assert.Equal(1,
            (await database.Repository.CreateTrainingSnapshotAsync(CancellationToken.None)).PendingCorrectionCount);

        await using var restartedContext = database.CreateContext();
        var restartedRepository = new SupportTicketRepository(restartedContext);
        Assert.Equal(2,
            (await restartedRepository.GetActiveModelAsync(CancellationToken.None))!.VersionNumber);
    }
}

