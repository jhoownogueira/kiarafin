using KiaraFin.Api.Features.Cards;
using KiaraFin.Api.Features.Finance;
using KiaraFin.Api.Features.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KiaraFin.Api.Data.Configurations;

internal static class FinanceConfigurationExtensions
{
    public static PropertyBuilder<decimal> IsMoney(this PropertyBuilder<decimal> property) =>
        property.HasPrecision(18, 2);

    public static PropertyBuilder<decimal?> IsMoney(this PropertyBuilder<decimal?> property) =>
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
        builder.Ignore(x => x.InstitutionControlled);
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Institution).HasMaxLength(120);
        builder.Property(x => x.Color).HasMaxLength(30);
        builder.Property(x => x.Icon).HasMaxLength(60);
        builder.Property(x => x.SourceProvider).HasMaxLength(30);
        builder.Property(x => x.ExternalId).HasMaxLength(160);
        builder.Property(x => x.Balance).IsMoney();
        builder.Property(x => x.CreatedAtUtc).IsUtcInstant();
        builder.Property(x => x.UpdatedAtUtc).IsUtcInstant();
        builder.HasIndex(x => new { x.HouseholdId, x.IsActive });
        builder.HasIndex(x => new { x.HouseholdId, x.ExternalId }).IsUnique().HasFilter("\"ExternalId\" IS NOT NULL");
        builder.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BankTransactionConfiguration : IEntityTypeConfiguration<BankTransaction>
{
    public void Configure(EntityTypeBuilder<BankTransaction> builder)
    {
        builder.ToTable("bank_transactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalId).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Amount).IsMoney();
        builder.Property(x => x.Date).HasColumnType("date");
        builder.Property(x => x.Category).HasMaxLength(160);
        builder.Property(x => x.CreatedAtUtc).IsUtcInstant();
        builder.Property(x => x.UpdatedAtUtc).IsUtcInstant();
        builder.HasIndex(x => new { x.AccountId, x.ExternalId }).IsUnique();
        builder.HasIndex(x => new { x.AccountId, x.Date });
        builder.HasOne<FinancialAccount>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Restrict);
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

public sealed class CreditCardConfiguration : IEntityTypeConfiguration<CreditCard>
{
    public void Configure(EntityTypeBuilder<CreditCard> builder)
    {
        builder.ToTable("cards");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalId).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Institution).HasMaxLength(160);
        builder.Property(x => x.Brand).HasMaxLength(60);
        builder.Property(x => x.Number).HasMaxLength(40);
        builder.Property(x => x.CreditLimit).IsMoney();
        builder.Property(x => x.AvailableLimit).IsMoney();
        builder.Property(x => x.ReportedBalance).IsMoney();
        builder.Property(x => x.MinimumPayment).IsMoney();
        builder.Property(x => x.BalanceCloseDate).HasColumnType("date");
        builder.Property(x => x.BalanceDueDate).HasColumnType("date");
        builder.Property(x => x.CreatedAtUtc).IsUtcInstant();
        builder.Property(x => x.UpdatedAtUtc).IsUtcInstant();
        builder.HasIndex(x => new { x.HouseholdId, x.ExternalId }).IsUnique();
        builder.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CardBillConfiguration : IEntityTypeConfiguration<CardBill>
{
    public void Configure(EntityTypeBuilder<CardBill> builder)
    {
        builder.ToTable("card_bills");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalId).HasMaxLength(160).IsRequired();
        builder.Property(x => x.DueDate).HasColumnType("date");
        builder.Property(x => x.ClosingDate).HasColumnType("date");
        builder.Property(x => x.LastPaymentDate).HasColumnType("date");
        builder.Property(x => x.TotalAmount).IsMoney();
        builder.Property(x => x.MinimumPayment).IsMoney();
        builder.Property(x => x.PaidAmount).IsMoney();
        builder.Property(x => x.CreatedAtUtc).IsUtcInstant();
        builder.Property(x => x.UpdatedAtUtc).IsUtcInstant();
        builder.HasIndex(x => new { x.CardId, x.ExternalId }).IsUnique();
        builder.HasOne<CreditCard>().WithMany().HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CardTransactionConfiguration : IEntityTypeConfiguration<CardTransaction>
{
    public void Configure(EntityTypeBuilder<CardTransaction> builder)
    {
        builder.ToTable("card_transactions");
        builder.HasKey(x => x.Id);
        builder.Ignore(x => x.InvoiceEffect);
        builder.Property(x => x.ExternalId).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Amount).IsMoney();
        builder.Property(x => x.OriginalAmount).IsMoney();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Date).HasColumnType("date");
        builder.Property(x => x.PurchaseDate).HasColumnType("date");
        builder.Property(x => x.BillExternalId).HasMaxLength(160);
        builder.Property(x => x.Category).HasMaxLength(160);
        builder.Property(x => x.CurrencyCode).HasMaxLength(10);
        builder.Property(x => x.CreatedAtUtc).IsUtcInstant();
        builder.Property(x => x.UpdatedAtUtc).IsUtcInstant();
        builder.HasIndex(x => new { x.CardId, x.ExternalId }).IsUnique();
        builder.HasIndex(x => new { x.CardId, x.BillExternalId });
        builder.HasOne<CreditCard>().WithMany().HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Cascade);
    }
}
