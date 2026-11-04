namespace AiAgents.SupportTicketAgent.Application.Abstractions;

public interface IModelPathProvider
{
    string GetModelDirectory(int versionNumber);
}

