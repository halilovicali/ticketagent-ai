using AiAgents.SupportTicketAgent.Application.Results;
using AiAgents.SupportTicketAgent.Application.Services;
using AiAgents.SupportTicketAgent.Web.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace AiAgents.SupportTicketAgent.Web.Controllers;

[ApiController]
[Route("api/tickets")]
public sealed class TicketsController(
    QueueService queueService,
    TicketQueryService queryService,
    ReviewService reviewService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Create(
        CreateTicketRequest request,
        CancellationToken cancellationToken)
    {
        // Transport responsibility only: validate, enqueue, and return immediately.
        var ticket = await queueService.EnqueueAsync(
            request.Subject,
            request.Description,
            request.RequesterEmail,
            cancellationToken);
        var details = new TicketDetails(ticket, null, null);
        return AcceptedAtAction(nameof(GetById), new { id = ticket.Id }, TicketResponse.From(details));
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TicketSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var tickets = await queryService.ListAsync(limit, cancellationToken);
        return Ok(tickets.Select(TicketSummaryResponse.From));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        var details = await queryService.GetAsync(id, cancellationToken);
        return details is null ? NotFound() : Ok(TicketResponse.From(details));
    }

    [HttpPost("{id:long}/reviews")]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> AddReview(
        long id,
        AddReviewRequest request,
        CancellationToken cancellationToken)
    {
        await reviewService.AddGoldLabelAsync(
            id,
            request.Category,
            request.Priority,
            request.Reviewer,
            cancellationToken);
        var details = await queryService.GetAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Ticket {id} was not found.");
        return Ok(TicketResponse.From(details));
    }
}

