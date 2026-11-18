using AiAgents.SupportTicketAgent.Application.Services;
using AiAgents.SupportTicketAgent.Domain;
using AiAgents.SupportTicketAgent.Infrastructure;
using AiAgents.SupportTicketAgent.ML;
using Xunit.Abstractions;

namespace AiAgents.SupportTicketAgent.Tests;

public sealed class LearningScenarioTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "LearningDemo")]
    public async Task SameText_AfterGoldCorrections_UsesNewActiveModelAndChangesDecision()
    {
        await using var database = await TestDatabase.CreateAsync();
        var modelRoot = Path.Combine(Path.GetTempPath(), $"ticket-learning-{Guid.NewGuid():N}");
        try
        {
            var classifier = new MlNetSupportTicketClassifier();
            var training = new TrainingService(
                database.Repository,
                classifier,
                new ModelPathProvider(modelRoot));
            await training.EnsureActiveModelAsync(CancellationToken.None);
            var oldModel = await database.Repository.GetActiveModelAsync(CancellationToken.None)
                ?? throw new InvalidOperationException("Initial model was not activated.");

            const string subject = "ZORB ledger synchronization";
            const string description = "ZORB ledger synchronization needs a decision today";
            var text = $"{subject} {description}";
            var before = await classifier.PredictAsync(text, oldModel.FilePath, CancellationToken.None);
            var correctedCategory = before.Category == TicketCategory.Billing
                ? TicketCategory.FeatureRequest
                : TicketCategory.Billing;
            var correctedPriority = before.Priority == TicketPriority.Urgent
                ? TicketPriority.Low
                : TicketPriority.Urgent;

            for (var i = 0; i < 12; i++)
            {
                await TestData.AddReviewedTicketAsync(
                    database,
                    i,
                    correctedCategory,
                    correctedPriority,
                    subject,
                    description);
            }

            var outcome = await training.TrainAndActivateAsync(CancellationToken.None);
            var newModel = await database.Repository.GetActiveModelAsync(CancellationToken.None)
                ?? throw new InvalidOperationException("Retrained model was not activated.");
            var after = await classifier.PredictAsync(text, newModel.FilePath, CancellationToken.None);

            output.WriteLine(
                $"BEFORE modelDbId={oldModel.Id}, version={oldModel.VersionNumber}: " +
                $"{before.Category}/{before.Priority}");
            output.WriteLine(
                $"AFTER  modelDbId={newModel.Id}, version={newModel.VersionNumber}: " +
                $"{after.Category}/{after.Priority}");
            output.WriteLine(
                $"Used corrections={outcome.TrainingReviewCount}, cutoffReviewId={outcome.TrainingCutoffReviewId}");

            Assert.NotEqual(oldModel.Id, newModel.Id);
            Assert.Equal(2, newModel.VersionNumber);
            Assert.Equal(correctedCategory, after.Category);
            Assert.Equal(correctedPriority, after.Priority);
            Assert.NotEqual((before.Category, before.Priority), (after.Category, after.Priority));

            database.DbContext.ChangeTracker.Clear();
            await using var restartedContext = database.CreateContext();
            var restartedRepository = new SupportTicketRepository(restartedContext);
            var activeAfterRestart = await restartedRepository.GetActiveModelAsync(CancellationToken.None);
            Assert.NotNull(activeAfterRestart);
            Assert.Equal(newModel.Id, activeAfterRestart.Id);
            Assert.True(activeAfterRestart.IsActive);
        }
        finally
        {
            if (Directory.Exists(modelRoot))
                Directory.Delete(modelRoot, recursive: true);
        }
    }
}
