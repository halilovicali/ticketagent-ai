using AiAgents.SupportTicketAgent.Domain;
using Microsoft.EntityFrameworkCore;

namespace AiAgents.SupportTicketAgent.Infrastructure;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(
        SupportTicketDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        // A host crash can leave a claimed ticket in Processing. Requeue it safely on restart.
        var recoveryTime = DateTimeOffset.UtcNow;
        await dbContext.Tickets
            .Where(x => x.Status == TicketStatus.Processing)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, TicketStatus.Queued)
                .SetProperty(x => x.UpdatedAtUtc, recoveryTime), cancellationToken);

        if (!await dbContext.Settings.AnyAsync(cancellationToken))
            dbContext.Settings.Add(SystemSettings.CreateDefault());

        if (!await dbContext.TrainingExamples.AnyAsync(cancellationToken))
            dbContext.TrainingExamples.AddRange(CreateTrainingExamples());

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyList<TrainingExample> CreateTrainingExamples()
    {
        // Every fifth row is a deterministic test sample. The remaining 80% train the model.
        var rows = new (string Text, TicketCategory Category, TicketPriority Priority)[]
        {
            ("Application crashes every time I open the dashboard", TicketCategory.Technical, TicketPriority.High),
            ("The export button does not produce a file", TicketCategory.Technical, TicketPriority.Medium),
            ("Page is slightly slow when loading reports", TicketCategory.Technical, TicketPriority.Low),
            ("Production service is completely unavailable for all users", TicketCategory.Technical, TicketPriority.Urgent),
            ("I receive an unexpected error while saving my work", TicketCategory.Technical, TicketPriority.High),

            ("Invoice contains an incorrect charge from last month", TicketCategory.Billing, TicketPriority.High),
            ("Please send me a copy of my latest invoice", TicketCategory.Billing, TicketPriority.Low),
            ("My card payment was declined but the card works elsewhere", TicketCategory.Billing, TicketPriority.Medium),
            ("We were charged twice and need an immediate refund", TicketCategory.Billing, TicketPriority.Urgent),
            ("How can I update the payment method for my subscription", TicketCategory.Billing, TicketPriority.Medium),

            ("I forgot my password and cannot sign in", TicketCategory.AccountAccess, TicketPriority.High),
            ("Please change the email address on my account", TicketCategory.AccountAccess, TicketPriority.Medium),
            ("How do I enable two factor authentication", TicketCategory.AccountAccess, TicketPriority.Low),
            ("Our administrator account is locked and work has stopped", TicketCategory.AccountAccess, TicketPriority.Urgent),
            ("The password reset link has already expired", TicketCategory.AccountAccess, TicketPriority.High),

            ("Please add a dark mode to the mobile application", TicketCategory.FeatureRequest, TicketPriority.Low),
            ("We would like an integration with our calendar", TicketCategory.FeatureRequest, TicketPriority.Medium),
            ("It would be useful to export reports as PDF", TicketCategory.FeatureRequest, TicketPriority.Low),
            ("We urgently need single sign on before company rollout", TicketCategory.FeatureRequest, TicketPriority.High),
            ("Could you add custom fields to customer profiles", TicketCategory.FeatureRequest, TicketPriority.Medium),

            ("What are your support working hours", TicketCategory.General, TicketPriority.Low),
            ("I need information about the business plan", TicketCategory.General, TicketPriority.Medium),
            ("Where can I find the user documentation", TicketCategory.General, TicketPriority.Low),
            ("Please contact me today about an important partnership", TicketCategory.General, TicketPriority.High),
            ("Can you explain how the free trial works", TicketCategory.General, TicketPriority.Low),

            ("Database connection fails and users cannot complete orders", TicketCategory.Technical, TicketPriority.Urgent),
            ("The search filter returns the wrong results", TicketCategory.Technical, TicketPriority.Medium),
            ("A minor icon is missing from the settings screen", TicketCategory.Technical, TicketPriority.Low),
            ("Notifications stopped working after the latest update", TicketCategory.Technical, TicketPriority.High),
            ("The API responds with server errors for every request", TicketCategory.Technical, TicketPriority.Urgent),

            ("Refund has not arrived after ten business days", TicketCategory.Billing, TicketPriority.High),
            ("I have a question about annual billing", TicketCategory.Billing, TicketPriority.Low),
            ("Please update the company name shown on our invoices", TicketCategory.Billing, TicketPriority.Medium),
            ("A fraudulent payment appears on our account", TicketCategory.Billing, TicketPriority.Urgent),
            ("The discount code was not applied at checkout", TicketCategory.Billing, TicketPriority.Medium),

            ("A former employee still has access to our organization", TicketCategory.AccountAccess, TicketPriority.Urgent),
            ("I cannot verify my email address", TicketCategory.AccountAccess, TicketPriority.Medium),
            ("Please tell me how to delete an unused user", TicketCategory.AccountAccess, TicketPriority.Low),
            ("Someone may have stolen my account credentials", TicketCategory.AccountAccess, TicketPriority.Urgent),
            ("Invited team member cannot accept the invitation", TicketCategory.AccountAccess, TicketPriority.High),

            ("Add support for another language in the interface", TicketCategory.FeatureRequest, TicketPriority.Low),
            ("We need automated weekly reports for management", TicketCategory.FeatureRequest, TicketPriority.Medium),
            ("Please provide an audit log for compliance", TicketCategory.FeatureRequest, TicketPriority.High),
            ("A keyboard shortcut for search would be convenient", TicketCategory.FeatureRequest, TicketPriority.Low),
            ("Could the system send alerts to our chat application", TicketCategory.FeatureRequest, TicketPriority.Medium),

            ("Who should I contact about becoming a reseller", TicketCategory.General, TicketPriority.Medium),
            ("Thank you for helping us with the migration", TicketCategory.General, TicketPriority.Low),
            ("I need a response today regarding our contract", TicketCategory.General, TicketPriority.High),
            ("Where is your privacy policy available", TicketCategory.General, TicketPriority.Low),
            ("Can I schedule a product demonstration next week", TicketCategory.General, TicketPriority.Medium)
        };

        return rows
            .Select((row, index) => new TrainingExample(
                row.Text,
                row.Category,
                row.Priority,
                index % 5 == 0 ? DatasetSplit.Test : DatasetSplit.Train))
            .ToArray();
    }
}

