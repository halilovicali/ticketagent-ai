using System.ComponentModel.DataAnnotations;
using AiAgents.SupportTicketAgent.Domain;

namespace AiAgents.SupportTicketAgent.Web.Contracts;

public sealed class UpdateSettingsRequest
{
    [Range(0, 1)]
    public float AutoRouteThreshold { get; init; }

    [Range(1, int.MaxValue)]
    public int RetrainGoldThreshold { get; init; }

    public bool RetrainingEnabled { get; init; }
}

public sealed record SettingsResponse(
    float AutoRouteThreshold,
    int RetrainGoldThreshold,
    bool RetrainingEnabled,
    int NewGoldSinceLastTrain)
{
    public static SettingsResponse From(SystemSettings settings) => new(
        settings.AutoRouteThreshold,
        settings.RetrainGoldThreshold,
        settings.RetrainingEnabled,
        settings.NewGoldSinceLastTrain);
}

public sealed record ModelVersionResponse(
    int Id,
    int VersionNumber,
    double TestAccuracy,
    bool IsActive,
    DateTimeOffset CreatedAtUtc)
{
    public static ModelVersionResponse From(ModelVersion model) => new(
        model.Id,
        model.VersionNumber,
        model.TestAccuracy,
        model.IsActive,
        model.CreatedAtUtc);
}

