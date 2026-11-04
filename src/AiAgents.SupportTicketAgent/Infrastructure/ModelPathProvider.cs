using AiAgents.SupportTicketAgent.Application.Abstractions;

namespace AiAgents.SupportTicketAgent.Infrastructure;

public sealed class ModelPathProvider(string rootDirectory) : IModelPathProvider
{
    private readonly string _rootDirectory = Path.GetFullPath(rootDirectory);

    public string GetModelDirectory(int versionNumber)
    {
        if (versionNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(versionNumber));
        return Path.Combine(_rootDirectory, $"model-v{versionNumber}");
    }
}

