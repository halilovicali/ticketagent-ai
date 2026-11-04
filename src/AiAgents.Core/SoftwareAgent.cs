namespace AiAgents.Core;

public interface IPerceptionSource<TPercept>
{
    Task<TPercept?> SenseAsync(CancellationToken cancellationToken);
}

public interface IPolicy<TPercept, TAction>
{
    Task<TAction> ThinkAsync(TPercept percept, CancellationToken cancellationToken);
}

public interface IActuator<TAction, TResult>
{
    Task<TResult> ActAsync(TAction action, CancellationToken cancellationToken);
}

public interface ILearningComponent<TExperience>
{
    Task LearnAsync(TExperience experience, CancellationToken cancellationToken);
}

/// <summary>
/// Generic framework abstraction. A step is one small, observable agent cycle.
/// A null result means that the agent found no work and changed no state.
/// </summary>
public abstract class SoftwareAgent<TPercept, TAction, TResult, TExperience>
    where TPercept : class
    where TAction : class
    where TResult : class
    where TExperience : class
{
    public abstract Task<TResult?> StepAsync(CancellationToken cancellationToken);
}

