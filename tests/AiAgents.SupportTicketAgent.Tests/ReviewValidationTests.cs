using System.ComponentModel.DataAnnotations;
using AiAgents.SupportTicketAgent.Application.Services;
using AiAgents.SupportTicketAgent.Domain;
using AiAgents.SupportTicketAgent.Web.Contracts;

namespace AiAgents.SupportTicketAgent.Tests;

public sealed class ReviewValidationTests
{
    [Fact]
    public void MissingPriority_IsRejectedByWebContract()
    {
        var request = new AddReviewRequest
        {
            Category = TicketCategory.Technical,
            Reviewer = "Reviewer"
        };

        var results = Validate(request);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(AddReviewRequest.Priority)));
    }

    [Fact]
    public void UndefinedNumericPriority_IsRejectedByWebContract()
    {
        var request = new AddReviewRequest
        {
            Category = TicketCategory.Technical,
            Priority = (TicketPriority)999,
            Reviewer = "Reviewer"
        };

        var results = Validate(request);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(AddReviewRequest.Priority)));
    }

    [Fact]
    public async Task UndefinedEnums_AreRejectedByApplicationServiceBeforeDatabaseWrite()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = new ReviewService(database.Repository);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.AddGoldLabelAsync(
                1,
                (TicketCategory)999,
                TicketPriority.High,
                "Reviewer",
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.AddGoldLabelAsync(
                1,
                TicketCategory.Technical,
                (TicketPriority)999,
                "Reviewer",
                CancellationToken.None));
        Assert.Empty(database.DbContext.Reviews);
    }

    private static List<ValidationResult> Validate(AddReviewRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, true);
        return results;
    }
}
