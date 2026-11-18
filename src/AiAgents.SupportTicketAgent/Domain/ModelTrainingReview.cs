namespace AiAgents.SupportTicketAgent.Domain;

/// <summary>
/// Audit link proving which human corrections were included in one model version.
/// </summary>
public sealed class ModelTrainingReview
{
    private ModelTrainingReview() { }

    public ModelTrainingReview(int modelVersionId, long reviewId)
    {
        if (modelVersionId < 1)
            throw new ArgumentOutOfRangeException(nameof(modelVersionId));
        if (reviewId < 1)
            throw new ArgumentOutOfRangeException(nameof(reviewId));

        ModelVersionId = modelVersionId;
        ReviewId = reviewId;
    }

    public int ModelVersionId { get; private set; }
    public long ReviewId { get; private set; }

    public ModelVersion? ModelVersion { get; private set; }
    public Review? Review { get; private set; }
}
