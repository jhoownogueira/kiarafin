using KiaraFin.Api.Features.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KiaraFin.Api.Features.Pluggy;

public sealed class FinancialIntegrationConfiguration : IEntityTypeConfiguration<FinancialIntegration>
{
    public void Configure(EntityTypeBuilder<FinancialIntegration> b)
    {
        b.ToTable("financial_integrations"); b.HasKey(x => x.Id);
        b.Property(x => x.Provider).HasMaxLength(30).IsRequired(); b.Property(x => x.ItemId).HasMaxLength(120).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.LastError).HasMaxLength(500);
        b.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone"); b.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.ActivatedAtUtc).HasColumnType("timestamp with time zone"); b.Property(x => x.LastSuccessfulSyncAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.LastAttemptAtUtc).HasColumnType("timestamp with time zone"); b.Property(x => x.ConsentExpiresAtUtc).HasColumnType("timestamp with time zone");
        b.HasIndex(x => new { x.UserId, x.Provider }).IsUnique(); b.HasIndex(x => new { x.Provider, x.ItemId }).IsUnique();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FinancialIntegrationSyncConfiguration : IEntityTypeConfiguration<FinancialIntegrationSync>
{
    public void Configure(EntityTypeBuilder<FinancialIntegrationSync> b)
    {
        b.ToTable("financial_integration_syncs"); b.HasKey(x => x.Id); b.Property(x => x.Trigger).HasMaxLength(40).IsRequired(); b.Property(x => x.Error).HasMaxLength(500);
        b.Property(x => x.StartedAtUtc).HasColumnType("timestamp with time zone"); b.Property(x => x.FinishedAtUtc).HasColumnType("timestamp with time zone");
        b.HasIndex(x => new { x.IntegrationId, x.StartedAtUtc }); b.HasOne<FinancialIntegration>().WithMany().HasForeignKey(x => x.IntegrationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PluggySyncJobConfiguration : IEntityTypeConfiguration<PluggySyncJob>
{
    public void Configure(EntityTypeBuilder<PluggySyncJob> b)
    {
        b.ToTable("pluggy_sync_jobs"); b.HasKey(x => x.Id); b.Property(x => x.Trigger).HasMaxLength(40).IsRequired();
        b.Property(x => x.DeduplicationKey).HasMaxLength(180).IsRequired(); b.Property(x => x.Payload).HasColumnType("jsonb"); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.LastError).HasMaxLength(500); b.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.AvailableAtUtc).HasColumnType("timestamp with time zone"); b.Property(x => x.CompletedAtUtc).HasColumnType("timestamp with time zone");
        b.HasIndex(x => x.DeduplicationKey).IsUnique(); b.HasIndex(x => new { x.Status, x.AvailableAtUtc });
        b.HasOne<FinancialIntegration>().WithMany().HasForeignKey(x => x.IntegrationId).OnDelete(DeleteBehavior.Restrict);
    }
}
