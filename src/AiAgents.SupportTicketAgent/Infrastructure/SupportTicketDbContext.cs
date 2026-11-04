using AiAgents.SupportTicketAgent.Domain;
using Microsoft.EntityFrameworkCore;

namespace AiAgents.SupportTicketAgent.Infrastructure;

public sealed class SupportTicketDbContext(DbContextOptions<SupportTicketDbContext> options)
    : DbContext(options)
{
    public DbSet<SupportTicket> Tickets => Set<SupportTicket>();
    public DbSet<Prediction> Predictions => Set<Prediction>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ModelVersion> ModelVersions => Set<ModelVersion>();
    public DbSet<SystemSettings> Settings => Set<SystemSettings>();
    public DbSet<TrainingExample> TrainingExamples => Set<TrainingExample>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SupportTicket>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Subject).HasMaxLength(200);
            entity.Property(x => x.RequesterEmail).HasMaxLength(320);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.Status, x.Id });
        });

        modelBuilder.Entity<Prediction>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Category).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Priority).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(x => x.TicketId).IsUnique();
            entity.HasOne(x => x.Ticket)
                .WithOne()
                .HasForeignKey<Prediction>(x => x.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ModelVersion)
                .WithMany()
                .HasForeignKey(x => x.ModelVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.GoldCategory).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.GoldPriority).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(x => x.TicketId).IsUnique();
            entity.HasOne(x => x.Ticket)
                .WithOne()
                .HasForeignKey<Review>(x => x.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ModelVersion>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.VersionNumber).IsUnique();
            entity.HasIndex(x => x.IsActive);
        });

        modelBuilder.Entity<SystemSettings>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.ToTable("SystemSettings");
        });

        modelBuilder.Entity<TrainingExample>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Category).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Priority).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.Split).HasConversion<string>().HasMaxLength(16);
        });
    }
}

