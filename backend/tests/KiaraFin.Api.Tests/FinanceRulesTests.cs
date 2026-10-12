using KiaraFin.Api.Common.Exceptions;
using KiaraFin.Api.Features.Finance;
using Xunit;

namespace KiaraFin.Api.Tests;

public sealed class FinanceRulesTests
{
    [Fact]
    public void Installments_distribute_remainder_without_losing_cents()
    {
        var allocations = InstallmentAllocator.Split(100m, 3);

        Assert.Equal([33.34m, 33.33m, 33.33m], allocations.Select(x => x.Amount));
        Assert.Equal(100m, allocations.Sum(x => x.Amount));
    }

    [Theory]
    [InlineData(2025, 2, 31, 28)]
    [InlineData(2024, 2, 31, 29)]
    [InlineData(2025, 4, 31, 30)]
    [InlineData(2025, 1, 28, 28)]
    public void Monthly_dates_clamp_to_the_last_valid_day(
        int year,
        int month,
        int desiredDay,
        int expectedDay)
    {
        Assert.Equal(expectedDay, MonthlyDateCalculator.Resolve(year, month, desiredDay).Day);
    }

    [Fact]
    public void Purchase_on_closing_day_stays_in_current_cycle_and_due_date_is_later()
    {
        var cycle = InvoiceCycleCalculator.ForPurchase(new DateOnly(2026, 1, 20), 20, 5);

        Assert.Equal(new DateOnly(2026, 1, 20), cycle.ClosingDate);
        Assert.Equal(new DateOnly(2026, 2, 5), cycle.DueDate);
        Assert.True(cycle.StartDate <= new DateOnly(2026, 1, 20));
    }

    [Fact]
    public void Purchase_after_closing_moves_to_next_invoice_across_year_boundary()
    {
        var cycle = InvoiceCycleCalculator.ForPurchase(new DateOnly(2025, 12, 21), 20, 5);

        Assert.Equal(new DateOnly(2026, 1, 20), cycle.ClosingDate);
        Assert.Equal(new DateOnly(2026, 2, 5), cycle.DueDate);
    }

    [Fact]
    public void Account_balance_ignores_pending_canceled_and_credit_card_consumption()
    {
        CashLedgerEntry[] entries =
        [
            new(500m, CashEntryKind.Income, true),
            new(100m, CashEntryKind.Expense, true),
            new(75m, CashEntryKind.Expense, false),
            new(50m, CashEntryKind.Expense, true, true),
            new(200m, CashEntryKind.CreditCardPurchase, true),
            new(200m, CashEntryKind.InvoicePayment, true)
        ];

        Assert.Equal(1_200m, AccountBalanceCalculator.Calculate(1_000m, entries));
    }

    [Fact]
    public void Projection_does_not_count_card_purchase_and_its_invoice_twice()
    {
        var through = new DateOnly(2026, 2, 28);
        ProjectionEntry[] entries =
        [
            new(1_000m, ProjectionEntryKind.ExpectedIncome, through),
            new(300m, ProjectionEntryKind.PendingExpense, through),
            new(500m, ProjectionEntryKind.CreditCardPurchase, through),
            new(500m, ProjectionEntryKind.InvoiceDue, through)
        ];

        var result = FinancialProjectionCalculator.Calculate(8_000m, through, entries);

        Assert.Equal(8_200m, result.ProjectedBalance);
        Assert.Equal(500m, result.ExpectedInvoices);
    }

    [Fact]
    public void Invoice_payment_is_a_distinct_cash_transaction_without_consumption_category()
    {
        var accountId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var payment = FinancialTransaction.CreateInvoicePayment(
            Guid.NewGuid(), accountId, invoiceId, 250m, new DateOnly(2026, 1, 10), Guid.NewGuid());

        Assert.Equal(FinancialTransactionType.InvoicePayment, payment.Type);
        Assert.Equal(FinancialTransactionStatus.Paid, payment.Status);
        Assert.Equal(accountId, payment.AccountId);
        Assert.Equal(invoiceId, payment.InvoiceId);
        Assert.Null(payment.CategoryId);
        Assert.Null(payment.CreditCardId);
    }

    [Fact]
    public void Regular_constructor_rejects_invoice_payment_type()
    {
        Assert.Throws<ArgumentException>(() => new FinancialTransaction(
            Guid.NewGuid(),
            "Pagamento indevido",
            10m,
            FinancialTransactionType.InvoicePayment,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 1),
            Guid.NewGuid(),
            ExpenseScope.Shared));
    }

    [Fact]
    public void Invoice_must_be_closed_manually_before_payment()
    {
        var invoice = new Invoice(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 6),
            new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 12));
        invoice.ReplaceCalculatedTotal(340m, DateTime.UtcNow);

        Assert.Throws<ConflictException>(() => invoice.PayInFull(
            Guid.NewGuid(), new DateOnly(2026, 11, 12), 340m, DateTime.UtcNow));

        invoice.Close(new DateOnly(2026, 11, 18), DateTime.UtcNow);
        invoice.PayInFull(Guid.NewGuid(), new DateOnly(2026, 11, 18), 340m, DateTime.UtcNow);

        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(new DateOnly(2026, 11, 18), invoice.DueDate);
    }

    [Fact]
    public void Paid_invoice_cannot_be_recalculated()
    {
        var invoice = new Invoice(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 6),
            new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 12));
        invoice.ReplaceCalculatedTotal(100m, DateTime.UtcNow);
        invoice.Close(new DateOnly(2026, 11, 15), DateTime.UtcNow);
        invoice.ReplaceCalculatedTotal(90m, DateTime.UtcNow);
        invoice.PayInFull(Guid.NewGuid(), new DateOnly(2026, 11, 15), 90m, DateTime.UtcNow);

        Assert.Throws<ConflictException>(() => invoice.ReplaceCalculatedTotal(80m, DateTime.UtcNow));
    }
}
