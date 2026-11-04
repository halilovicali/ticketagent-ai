using AiAgents.SupportTicketAgent.Application.Abstractions;
using AiAgents.SupportTicketAgent.Domain;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace AiAgents.SupportTicketAgent.ML;

/// <summary>
/// ML.NET is deliberately treated as a black box behind the application port.
/// Two text-classification pipelines predict category and priority independently.
/// </summary>
public sealed class MlNetSupportTicketClassifier : ISupportTicketClassifier
{
    private const string CategoryFileName = "category-model.zip";
    private const string PriorityFileName = "priority-model.zip";
    private readonly MLContext _mlContext = new(seed: 42);
    private readonly object _predictionLock = new();
    private string? _loadedPath;
    private PredictionEngine<ModelInput, ModelOutput>? _categoryEngine;
    private PredictionEngine<ModelInput, ModelOutput>? _priorityEngine;

    public Task<ClassifierOutput> PredictAsync(
        string text,
        string modelPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text is required for classification.", nameof(text));

        lock (_predictionLock)
        {
            EnsureLoaded(modelPath);
            var input = new ModelInput { Text = text };
            var category = _categoryEngine!.Predict(input);
            var priority = _priorityEngine!.Predict(input);

            if (!Enum.TryParse<TicketCategory>(category.PredictedLabel, true, out var parsedCategory))
                throw new InvalidDataException($"Unknown category label '{category.PredictedLabel}'.");
            if (!Enum.TryParse<TicketPriority>(priority.PredictedLabel, true, out var parsedPriority))
                throw new InvalidDataException($"Unknown priority label '{priority.PredictedLabel}'.");

            return Task.FromResult(new ClassifierOutput(
                parsedCategory,
                parsedPriority,
                GetConfidence(category.Score),
                GetConfidence(priority.Score)));
        }
    }

    public Task TrainAsync(
        IReadOnlyCollection<TrainingExampleData> examples,
        string modelPath,
        CancellationToken cancellationToken) =>
        Task.Run(() => Train(examples, modelPath, cancellationToken), cancellationToken);

    public async Task<double> EvaluateAsync(
        IReadOnlyCollection<TrainingExampleData> examples,
        string modelPath,
        CancellationToken cancellationToken)
    {
        if (examples.Count == 0)
            return 0;

        var correct = 0;
        foreach (var example in examples)
        {
            var output = await PredictAsync(example.Text, modelPath, cancellationToken);
            if (output.Category == example.Category)
                correct++;
            if (output.Priority == example.Priority)
                correct++;
        }

        return correct / (examples.Count * 2d);
    }

    private void Train(
        IReadOnlyCollection<TrainingExampleData> examples,
        string modelPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(modelPath);

        var inputs = examples.Select(x => new ModelInput
        {
            Text = x.Text,
            CategoryLabel = x.Category.ToString(),
            PriorityLabel = x.Priority.ToString()
        });
        var data = _mlContext.Data.LoadFromEnumerable(inputs);

        var categoryPipeline = _mlContext.Transforms.Conversion
            .MapValueToKey("Label", nameof(ModelInput.CategoryLabel))
            .Append(_mlContext.Transforms.Text.FeaturizeText("Features", nameof(ModelInput.Text)))
            .Append(_mlContext.MulticlassClassification.Trainers
                .SdcaMaximumEntropy("Label", "Features"))
            .Append(_mlContext.Transforms.Conversion
                .MapKeyToValue("PredictedLabel", "PredictedLabel"));

        var priorityPipeline = _mlContext.Transforms.Conversion
            .MapValueToKey("Label", nameof(ModelInput.PriorityLabel))
            .Append(_mlContext.Transforms.Text.FeaturizeText("Features", nameof(ModelInput.Text)))
            .Append(_mlContext.MulticlassClassification.Trainers
                .SdcaMaximumEntropy("Label", "Features"))
            .Append(_mlContext.Transforms.Conversion
                .MapKeyToValue("PredictedLabel", "PredictedLabel"));

        var categoryModel = categoryPipeline.Fit(data);
        cancellationToken.ThrowIfCancellationRequested();
        var priorityModel = priorityPipeline.Fit(data);

        _mlContext.Model.Save(categoryModel, data.Schema, Path.Combine(modelPath, CategoryFileName));
        _mlContext.Model.Save(priorityModel, data.Schema, Path.Combine(modelPath, PriorityFileName));

        lock (_predictionLock)
        {
            if (string.Equals(_loadedPath, modelPath, StringComparison.Ordinal))
            {
                _loadedPath = null;
                _categoryEngine = null;
                _priorityEngine = null;
            }
        }
    }

    private void EnsureLoaded(string modelPath)
    {
        if (string.Equals(_loadedPath, modelPath, StringComparison.Ordinal) &&
            _categoryEngine is not null &&
            _priorityEngine is not null)
        {
            return;
        }

        var categoryPath = Path.Combine(modelPath, CategoryFileName);
        var priorityPath = Path.Combine(modelPath, PriorityFileName);
        if (!File.Exists(categoryPath) || !File.Exists(priorityPath))
            throw new FileNotFoundException($"Model files are missing from '{modelPath}'.");

        var categoryModel = _mlContext.Model.Load(categoryPath, out _);
        var priorityModel = _mlContext.Model.Load(priorityPath, out _);
        _categoryEngine = _mlContext.Model.CreatePredictionEngine<ModelInput, ModelOutput>(categoryModel);
        _priorityEngine = _mlContext.Model.CreatePredictionEngine<ModelInput, ModelOutput>(priorityModel);
        _loadedPath = modelPath;
    }

    private static float GetConfidence(float[] scores)
    {
        if (scores.Length == 0)
            return 0;

        var sum = scores.Sum(x => (double)x);
        if (scores.All(x => x is >= 0 and <= 1) && Math.Abs(sum - 1d) < 0.01d)
            return scores.Max();

        var max = scores.Max();
        var exponentials = scores.Select(x => Math.Exp(x - max)).ToArray();
        var denominator = exponentials.Sum();
        return denominator == 0 ? 0 : (float)(exponentials.Max() / denominator);
    }

    private sealed class ModelInput
    {
        public string Text { get; init; } = string.Empty;
        public string CategoryLabel { get; init; } = string.Empty;
        public string PriorityLabel { get; init; } = string.Empty;
    }

    private sealed class ModelOutput
    {
        [ColumnName("PredictedLabel")]
        public string PredictedLabel { get; init; } = string.Empty;

        [ColumnName("Score")]
        public float[] Score { get; init; } = [];
    }
}

