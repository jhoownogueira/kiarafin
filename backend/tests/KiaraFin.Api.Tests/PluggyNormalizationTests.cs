using KiaraFin.Api.Features.Finance;
using KiaraFin.Api.Features.Pluggy;
using Xunit;

namespace KiaraFin.Api.Tests;

public sealed class PluggyNormalizationTests
{
    [Fact]
    public void ExplicitCreditWinsOverNegativeAmountForBankAccount()
    {
        var result = PluggyNormalizer.Normalize(Account("BANK"), Transaction(-125.45m, type: "CREDIT"));

        Assert.Equal(ExternalTransactionKind.Income, result.Kind);
        Assert.Equal(FinancialTransactionType.Income, result.FinancialType);
        Assert.Equal(125.45m, result.Amount);
        Assert.True(result.CreatesLedgerEntry);
    }

    [Fact]
    public void ExplicitDebitWinsOverPositiveAmountForBankAccount()
    {
        var result = PluggyNormalizer.Normalize(Account("BANK"), Transaction(79.90m, type: "DEBIT"));

        Assert.Equal(ExternalTransactionKind.Expense, result.Kind);
        Assert.Equal(FinancialTransactionType.Expense, result.FinancialType);
        Assert.Equal(79.90m, result.Amount);
        Assert.True(result.CreatesLedgerEntry);
    }

    [Fact]
    public void CreditCardInvoicePaymentDoesNotCreateConsumptionExpense()
    {
        var result = PluggyNormalizer.Normalize(
            Account("BANK"),
            Transaction(-900m, type: "DEBIT", operationType: "CREDIT_CARD_PAYMENT"));

        Assert.Equal(ExternalTransactionKind.InvoicePayment, result.Kind);
        Assert.Null(result.FinancialType);
        Assert.False(result.CreatesLedgerEntry);
    }

    [Fact]
    public void Generic_bill_payment_on_bank_account_remains_an_expense()
    {
        var result = PluggyNormalizer.Normalize(
            Account("BANK"),
            Transaction(-120m, type: "DEBIT", operationType: "BILL_PAYMENT", description: "Pagamento de energia"));

        Assert.Equal(ExternalTransactionKind.Expense, result.Kind);
        Assert.True(result.CreatesLedgerEntry);
    }

    [Fact]
    public void UnknownDirectionIsNotInferredFromNegativeSign()
    {
        var result = PluggyNormalizer.Normalize(Account("BANK"), Transaction(-42m));

        Assert.Equal(ExternalTransactionKind.Unknown, result.Kind);
        Assert.Null(result.FinancialType);
        Assert.False(result.CreatesLedgerEntry);
    }

    [Fact]
    public void CompletedStatusDoesNotAccidentallyMatchTedTransferToken()
    {
        var result = PluggyNormalizer.Normalize(
            Account("BANK"),
            Transaction(42m, type: "CREDIT", status: "COMPLETED", description: "Pagamento recebido"));

        Assert.Equal(ExternalTransactionKind.Income, result.Kind);
        Assert.True(result.CreatesLedgerEntry);
    }

    [Theory]
    [InlineData("CANCELED")]
    [InlineData("CANCELLED")]
    [InlineData("VOID")]
    [InlineData("REVERSED")]
    public void CancellationStatusesArePreserved(string status)
    {
        var result = PluggyNormalizer.Normalize(Account("BANK"), Transaction(10m, type: "DEBIT", status: status));

        Assert.True(result.IsCanceled);
    }

    [Fact]
    public void CreditOnCreditCardIsClassifiedAsRefund()
    {
        var result = PluggyNormalizer.Normalize(
            Account("CREDIT"),
            Transaction(35m, type: "CREDIT", operationType: "REFUND"));

        Assert.Equal(ExternalTransactionKind.Refund, result.Kind);
        Assert.Equal(35m, result.Amount);
    }

    private static PluggyAccount Account(string type) =>
        new("account-1", type, "Conta", "Instituição", 0m, null, "account-hash");

    private static PluggyTransaction Transaction(
        decimal amount,
        string? type = null,
        string? operationType = null,
        string? status = null,
        string description = "Transação") =>
        new(
            "transaction-1",
            "account-1",
            description,
            amount,
            new DateOnly(2026, 10, 19),
            type,
            operationType,
            status,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            "transaction-hash");
}
