using AiAgents.SupportTicketAgent.Application.Services;
using AiAgents.SupportTicketAgent.Web.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace AiAgents.SupportTicketAgent.Web.Controllers;

[ApiController]
[Route("api/settings")]
public sealed class SettingsController(SettingsService settingsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SettingsResponse>> Get(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAsync(cancellationToken);
        return Ok(SettingsResponse.From(settings));
    }

    [HttpPut]
    public async Task<ActionResult<SettingsResponse>> Update(
        UpdateSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var settings = await settingsService.UpdateAsync(
            request.AutoRouteThreshold,
            request.RetrainGoldThreshold,
            request.RetrainingEnabled,
            cancellationToken);
        return Ok(SettingsResponse.From(settings));
    }
}

