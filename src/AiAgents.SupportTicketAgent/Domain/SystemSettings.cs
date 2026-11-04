namespace AiAgents.SupportTicketAgent.Domain;

public sealed class SystemSettings
{
    private SystemSettings() { }

    public static SystemSettings CreateDefault() => new()
    {
        Id = 1,
        AutoRouteThreshold = 0.60f,
        RetrainGoldThreshold = 5,
        RetrainingEnabled = true,
        NewGoldSinceLastTrain = 0
    };

    public int Id { get; private set; }
    public float AutoRouteThreshold { get; private set; }
    public int RetrainGoldThreshold { get; private set; }
    public bool RetrainingEnabled { get; private set; }
    public int NewGoldSinceLastTrain { get; private set; }

    public void Update(float autoRouteThreshold, int retrainGoldThreshold, bool retrainingEnabled)
    {
        if (autoRouteThreshold is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(autoRouteThreshold));
        if (retrainGoldThreshold < 1)
            throw new ArgumentOutOfRangeException(nameof(retrainGoldThreshold));

        AutoRouteThreshold = autoRouteThreshold;
        RetrainGoldThreshold = retrainGoldThreshold;
        RetrainingEnabled = retrainingEnabled;
    }

    public void RecordGoldLabel() => NewGoldSinceLastTrain++;
    public void ResetGoldCounter() => NewGoldSinceLastTrain = 0;
}

