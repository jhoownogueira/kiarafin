using KiaraFin.Api.Features.Finance;
using KiaraFin.Api.Features.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KiaraFin.Api.Data.Configurations;

internal static class FinanceConfigurationExtensions
{
    public static PropertyBuilder<decimal> IsMoney(this PropertyBuilder<decimal> property) =>
        property.HasPrecision(18, 2);

    public static PropertyBuilder<DateTime> IsUtcInstant(this PropertyBuilder<DateTime> property) =>
        property.HasColumnType("timestamp with time zone");

    public static PropertyBuilder<DateTime?> IsUtcInstant(this PropertyBuilder<DateTime?> property) =>
        property.HasColumnType("timestamp with time zone");
}

public sealed class FinancialAccountConfiguration : IEntityTypeConfiguration<FinancialAccount>
{
    public void Configure(EntityTypeBuilder<FinancialAccount> builder)
    {
        builder.ToTable("financial_accounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Institution).HasMaxLength(120);
        builder.Property(x => x.Color).HasMaxLength(30);
        builder.Property(x => x.Icon).HasMaxLength(60);
        builder.Property(x => x.InitialBalance).IsMoney();
        builder.Property(x => x.InitialBalanceDate).HasColumnType("date");
        builder.Property(x => x.CreatedAtUtc).IsUtcInstant();
        builder.Property(x => x.UpdatedAtUtc).IsUtcInstant();
        builder.HasIndex(x => new { x.HouseholdId, x.IsActive });
        builder.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ResponsibleUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Color).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Icon).HasMaxLength(60).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsUtcInstant();
        builder.Property(x => x.UpdatedAtUtc).IsUtcInstant();
        builder.HasIndex(x => new { x.HouseholdId, x.Type, x.Name }).IsUnique();
        builder.HasIndex(x => new { x.HouseholdId, x.IsActive });
        builder.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FinancialTransactionConfiguration : IEntityTypeConfiguration<FinancialTransaction>
{
    public void Configure(EntityTypeBuilder<FinancialTransaction> builder)
    {
        builder.ToTable("financial_transactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Description).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Amount).IsMoney();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CompetenceDate).HasColumnType("date");
        builder.Property(x => x.DueDate).HasColumnType("date");
        builder.Property(x => x.SettledDate).HasColumnType("date");
        builder.Property(x => x.Scope).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.ClientRequestId).HasMaxLength(100);
        builder.Property(x => x.CreatedAtUtc).IsUtcInstant();
        builder.Property(x => x.UpdatedAtUtc).IsUtcInstant();
        builder.Property(x => x.CanceledAtUtc).IsUtcInstant();
        builder.HasIndex(x => new { x.HouseholdId, x.ClientRequestId }).IsUnique();
        builder.HasIndex(x => new { x.HouseholdId, x.DueDate, x.Status });
        builder.HasIndex(x => new { x.AccountId, x.CompetenceDate });
        builder.HasIndex(x => new { x.RecurringTransactionId, x.DueDate }).IsUnique();
        builder.HasIndex(x => new { x.InstallmentPlanId, x.InstallmentNumber }).IsUnique();
        builder.HasIndex(x => new { x.InvoiceId, x.Status });
        builder.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FinancialAccount>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FinancialAccount>().WithMany().HasForeignKey(x => x.DestinationAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditCard>().WithMany().HasForeignKey(x => x.CreditCardId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Invoice>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RecurringTransaction>().WithMany().HasForeignKey(x => x.RecurringTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InstallmentPlan>().WithMany().HasForeignKey(x => x.InstallmentPlanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ResponsibleUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AccountMovementConfiguration : IEntityTypeConfiguration<AccountMovement>
{
    public void Configure(EntityTypeBuilder<AccountMovement> builder)
    {
        builder.ToTable("account_movements");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SignedAmount).IsMoney();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.MovementDate).HasColumnType("date");
        builder.Property(x => x.CreatedAtUtc).IsUtcInstant();
        builder.HasIndex(x => new { x.TransactionId, x.AccountId, x.Sequence }).IsUnique();
        builder.HasIndex(x => new { x.AccountId, x.MovementDate });
        builder.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FinancialAccount>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FinancialTransaction>().WithMany().HasForeignKey(x => x.TransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RecurringTransactionConfiguration : IEntityTypeConfiguration<RecurringTransaction>
{
    public void Configure(EntityTypeBuilder<RecurringTransaction> builder)
    {
        builder.ToTable("recurring_transactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ExpectedAmount).IsMoney();
        builder.Property(x => x.StartDate).HasColumnType("date");
        builder.Property(x => x.EndDate).HasColumnType("date");
        builder.Property(x => x.Frequency).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Scope).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CreatedAtUtc).IsUtcInstant();
        builder.Property(x => x.UpdatedAtUtc).IsUtcInstant();
        builder.HasIndex(x => new { x.HouseholdId, x.Status });
        builder.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FinancialAccount>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ResponsibleUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CreditCardConfiguration : IEntityTypeConfiguration<CreditCard>
{
    public void Configure(EntityTypeBuilder<CreditCard> builder)
    {
        builder.ToTable("credit_cards");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Institution).HasMaxLength(120);
        builder.Property(x => x.Brand).HasMaxLength(40);
        builder.Property(x => x.Color).HasMaxLength(30);
        builder.Property(x => x.TotalLimit).IsMoney();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CreatedAtUtc).IsUtcInstant();
        builder.Property(x => x.UpdatedAtUtc).IsUtcInstant();
        builder.HasIndex(x => new { x.HouseholdId, x.Status });
        builder.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ResponsibleUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FinancialAccount>().WithMany().HasForeignKey(x => x.DefaultPaymentAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CycleStartDate).HasColumnType("date");
        builder.Property(x => x.ClosingDate).HasColumnType("date");
        builder.Property(x => x.DueDate).HasColumnType("date");
        builder.Property(x => x.PaidDate).HasColumnType("date");
        builder.Property(x => x.TotalAmount).IsMoney();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CreatedAtUtc).IsUtcInstant();
        builder.Property(x => x.UpdatedAtUtc).IsUtcInstant();
        builder.HasIndex(x => new { x.CreditCardId, x.ClosingDate }).IsUnique();
        builder.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditCard>().WithMany().HasForeignKey(x => x.CreditCardId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FinancialTransaction>().WithMany().HasForeignKey(x => x.PaymentTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InstallmentPlanConfiguration : IEntityTypeConfiguration<InstallmentPlan>
{
    public void Configure(EntityTypeBuilder<InstallmentPlan> builder)
    {
        builder.ToTable("installment_plans");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Description).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TotalAmount).IsMoney();
        builder.Property(x => x.PurchaseDate).HasColumnType("date");
        builder.Property(x => x.FirstInstallmentDate).HasColumnType("date");
        builder.Property(x => x.SourceType).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Scope).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CreatedAtUtc).IsUtcInstant();
        builder.Property(x => x.UpdatedAtUtc).IsUtcInstant();
        builder.HasIndex(x => new { x.HouseholdId, x.PurchaseDate });
        builder.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ResponsibleUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
