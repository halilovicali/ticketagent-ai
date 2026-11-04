using System.Text.Json.Serialization;
using AiAgents.SupportTicketAgent.Application.Abstractions;
using AiAgents.SupportTicketAgent.Application.Runners;
using AiAgents.SupportTicketAgent.Application.Services;
using AiAgents.SupportTicketAgent.Domain;
using AiAgents.SupportTicketAgent.Infrastructure;
using AiAgents.SupportTicketAgent.ML;
using AiAgents.SupportTicketAgent.Web.Hubs;
using AiAgents.SupportTicketAgent.Web.Middleware;
using AiAgents.SupportTicketAgent.Web.Workers;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSignalR().AddJsonProtocol(options =>
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddHealthChecks();

var dataDirectory = Path.GetFullPath(Path.Combine(
    builder.Environment.ContentRootPath, "..", "..", "data"));
Directory.CreateDirectory(dataDirectory);
var databasePath = Path.Combine(dataDirectory, "support-ticket-agent.db");
var modelDirectory = Path.Combine(dataDirectory, "models");

builder.Services.AddDbContext<SupportTicketDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));

// Infrastructure adapters.
builder.Services.AddScoped<ISupportTicketRepository, SupportTicketRepository>();
builder.Services.AddSingleton<ISupportTicketClassifier, MlNetSupportTicketClassifier>();
builder.Services.AddSingleton<IModelPathProvider>(new ModelPathProvider(modelDirectory));

// Shared application/domain services and runners.
builder.Services.AddSingleton<TriagePolicy>();
builder.Services.AddScoped<QueueService>();
builder.Services.AddScoped<ScoringService>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<TicketQueryService>();
builder.Services.AddScoped<ModelQueryService>();
builder.Services.AddScoped<TrainingService>();
builder.Services.AddScoped<TriageAgentRunner>();
builder.Services.AddScoped<RetrainAgentRunner>();

// Host-only scheduling. Workers contain no thresholds or domain decisions.
builder.Services.AddHostedService<TriageBackgroundWorker>();
builder.Services.AddHostedService<RetrainBackgroundWorker>();

var app = builder.Build();
app.UseMiddleware<ApiExceptionMiddleware>();
app.MapControllers();
app.MapHub<AgentEventsHub>("/hubs/agent-events");
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new
{
    name = "Support Ticket Triage Agent",
    architecture = "Sense -> Think -> Act -> Learn",
    endpoints = new[] { "/api/tickets", "/api/settings", "/api/models", "/health" }
}));

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SupportTicketDbContext>();
    await DatabaseSeeder.SeedAsync(dbContext);
    var training = scope.ServiceProvider.GetRequiredService<TrainingService>();
    await training.EnsureActiveModelAsync(CancellationToken.None);
}

await app.RunAsync();

public partial class Program;

