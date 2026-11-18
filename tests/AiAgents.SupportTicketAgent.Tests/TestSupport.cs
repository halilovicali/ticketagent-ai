using AiAgents.SupportTicketAgent.Application.Abstractions;
using AiAgents.SupportTicketAgent.Domain;
using AiAgents.SupportTicketAgent.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AiAgents.SupportTicketAgent.Tests;

internal sealed class TestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private TestDatabase(SqliteConnection connection, SupportTicketDbContext dbContext)
    {
        _connection = connection;
        DbContext = dbContext;
        Repository = new SupportTicketRepository(dbContext);
    }

    public SupportTicketDbContext DbContext { get; }
    public SupportTicketRepository Repository { get; }

    public SupportTicketDbContext CreateContext() => new(
        new DbContextOptionsBuilder<SupportTicketDbContext>()
            .UseSqlite(_connection)
            .Options);

    public static async Task<TestDatabase> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SupportTicketDbContext>()
            .UseSqlite(connection)
            .Options;
        var dbContext = new SupportTicketDbContext(options);
        await DatabaseSeeder.SeedAsync(dbContext);
        return new TestDatabase(connection, dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await DbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

internal sealed class FakeClassifier(
    TicketCategory category = TicketCategory.Technical,
    TicketPriority priority = TicketPriority.High,
    float confidence = 0.95f,
    Func<CancellationToken, Task>? onTrain = null) : ISupportTicketClassifier
{
    public int TrainingCalls { get; private set; }

    public Task<ClassifierOutput> PredictAsync(
        string text,
        string modelPath,
        CancellationToken cancellationToken) =>
        Task.FromResult(new ClassifierOutput(category, priority, confidence, confidence));

    public async Task TrainAsync(
        IReadOnlyCollection<TrainingExampleData> examples,
        string modelPath,
        CancellationToken cancellationToken)
    {
        TrainingCalls++;
        if (onTrain is not null)
            await onTrain(cancellationToken);
    }

    public Task<double> EvaluateAsync(
        IReadOnlyCollection<TrainingExampleData> examples,
        string modelPath,
        CancellationToken cancellationToken) =>
        Task.FromResult(0.75d);
}

internal sealed class FailingReadRepository(SupportTicketDbContext dbContext)
    : SupportTicketRepository(dbContext)
{
    protected override Task<SupportTicket> LoadClaimedTicketAsync(
        long ticketId,
        CancellationToken cancellationToken) =>
        throw new IOException("Simulated read failure after claim update.");
}

internal static class TestData
{
    public static async Task<ModelVersion> ActivateModelAsync(
        TestDatabase database,
        int version = 1)
    {
        var model = new ModelVersion(version, $"fake-model-v{version}", 1, 0, 0);
        return await database.Repository.ActivateModelAsync(
            model,
            [],
            0,
            CancellationToken.None);
    }

    public static async Task<Review> AddReviewedTicketAsync(
        TestDatabase database,
        int index,
        TicketCategory category = TicketCategory.Billing,
        TicketPriority priority = TicketPriority.High,
        string? subject = null,
        string? description = null)
    {
        var ticket = new SupportTicket(
            subject ?? $"Correction example {index}",
            description ?? $"Repeated learning phrase {index}",
            $"review{index}@example.com");
        ticket.MarkProcessing();
        ticket.CompleteTriage(TicketStatus.Routed, TriagePolicy.TeamFor(category));
        database.DbContext.Tickets.Add(ticket);
        await database.DbContext.SaveChangesAsync();
        return await database.Repository.AddReviewAsync(
            ticket.Id,
            category,
            priority,
            "Test reviewer",
            TriagePolicy.TeamFor(category),
            CancellationToken.None);
    }
}

