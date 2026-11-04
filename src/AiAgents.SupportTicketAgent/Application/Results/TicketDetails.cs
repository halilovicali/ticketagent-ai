using AiAgents.SupportTicketAgent.Domain;

namespace AiAgents.SupportTicketAgent.Application.Results;

public sealed record TicketDetails(
    SupportTicket Ticket,
    Prediction? Prediction,
    Review? Review);

