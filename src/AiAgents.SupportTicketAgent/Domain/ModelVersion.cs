namespace AiAgents.SupportTicketAgent.Domain;

public sealed class ModelVersion
{
    private ModelVersion() { }

    public ModelVersion(int versionNumber, string filePath, double testAccuracy)
    {
        if (versionNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(versionNumber));
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Model path is required.", nameof(filePath));

        VersionNumber = versionNumber;
        FilePath = filePath;
        TestAccuracy = testAccuracy;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public int Id { get; private set; }
    public int VersionNumber { get; private set; }
    public string FilePath { get; private set; } = string.Empty;
    public double TestAccuracy { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}

