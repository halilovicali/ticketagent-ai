using AiAgents.SupportTicketAgent.Domain;

namespace AiAgents.SupportTicketAgent.Tests;

public sealed class TriagePolicyTests
{
    [Fact]
    public void LowConfidence_IsSentToManualReview()
    {
        var settings = SystemSettings.CreateDefault();
        var policy = new TriagePolicy();

        var decision = policy.Decide(
            TicketCategory.Billing,
            TicketPriority.Medium,
            0.59f,
            0.90f,
            settings);

        Assert.Equal(TicketStatus.PendingReview, decision.Status);
        Assert.Equal(TriagePolicy.ManualTriageTeam, decision.AssignedTeam);
    }

    [Fact]
    public void ConfidentPrediction_IsRoutedByCategory()
    {
        var settings = SystemSettings.CreateDefault();
        var policy = new TriagePolicy();

        var decision = policy.Decide(
            TicketCategory.AccountAccess,
            TicketPriority.Urgent,
            0.90f,
            0.80f,
            settings);

        Assert.Equal(TicketStatus.Routed, decision.Status);
        Assert.Equal("Account Services", decision.AssignedTeam);
    }
}

