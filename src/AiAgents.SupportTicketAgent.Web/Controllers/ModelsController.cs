using AiAgents.SupportTicketAgent.Application.Services;
using AiAgents.SupportTicketAgent.Web.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace AiAgents.SupportTicketAgent.Web.Controllers;

[ApiController]
[Route("api/models")]
public sealed class ModelsController(ModelQueryService queryService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ModelVersionResponse>>> List(
        CancellationToken cancellationToken)
    {
        var models = await queryService.ListAsync(cancellationToken);
        return Ok(models.Select(ModelVersionResponse.From));
    }

    [HttpGet("{id:int}/reviews")]
    public async Task<ActionResult<IReadOnlyList<ModelTrainingReviewResponse>>> ListTrainingReviews(
        int id,
        CancellationToken cancellationToken)
    {
        var reviews = await queryService.ListTrainingReviewsAsync(id, cancellationToken);
        return Ok(reviews.Select(x => new ModelTrainingReviewResponse(
            x.ReviewId,
            x.TicketId,
            x.Category,
            x.Priority,
            x.Reviewer,
            x.CreatedAtUtc)));
    }
}

