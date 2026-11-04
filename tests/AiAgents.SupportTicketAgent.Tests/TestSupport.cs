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
    float confidence = 0.95f) : ISupportTicketClassifier
{
    public int TrainingCalls { get; private set; }

    public Task<ClassifierOutput> PredictAsync(
        string text,
        string modelPath,
        CancellationToken cancellationToken) =>
        Task.FromResult(new ClassifierOutput(category, priority, confidence, confidence));

    public Task TrainAsync(
        IReadOnlyCollection<TrainingExampleData> examples,
        string modelPath,
        CancellationToken cancellationToken)
    {
        TrainingCalls++;
        return Task.CompletedTask;
    }

    public Task<double> EvaluateAsync(
        IReadOnlyCollection<TrainingExampleData> examples,
        string modelPath,
        CancellationToken cancellationToken) =>
        Task.FromResult(0.75d);
}

