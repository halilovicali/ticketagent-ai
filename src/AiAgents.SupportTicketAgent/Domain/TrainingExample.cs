namespace AiAgents.SupportTicketAgent.Domain;

public sealed class TrainingExample
{
    private TrainingExample() { }

    public TrainingExample(string text, TicketCategory category, TicketPriority priority, DatasetSplit split)
    {
        Text = string.IsNullOrWhiteSpace(text)
            ? throw new ArgumentException("Training text is required.", nameof(text))
            : text.Trim();
        Category = category;
        Priority = priority;
        Split = split;
    }

    public int Id { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public TicketCategory Category { get; private set; }
    public TicketPriority Priority { get; private set; }
    public DatasetSplit Split { get; private set; }
}

public enum DatasetSplit
{
    Train = 1,
    Test = 2
}

