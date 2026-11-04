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
}

