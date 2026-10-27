using KiaraFin.Api.Features.Finance;

namespace KiaraFin.Api.Features.Pluggy;

public sealed record NormalizedPluggyTransaction(ExternalTransactionKind Kind,
    FinancialTransactionType? FinancialType, decimal Amount, bool IsCanceled, bool IsPending,
    bool CreatesLedgerEntry, bool IsRefund = false);

public static class PluggyNormalizer
{
    private static readonly string[] ExplicitInvoicePaymentTypes = ["CREDIT_CARD_PAYMENT", "CARD_PAYMENT", "PAGAMENTO_FATURA"];
    private static readonly string[] CreditCardPaymentTerms = [.. ExplicitInvoicePaymentTypes, "BILL_PAYMENT", "PAGAMENTO DE FATURA"];
    private static readonly string[] TransferTerms = ["TRANSFER", "TED", "DOC", "PIX_TRANSFER", "PIX TRANSFER"];

    public static NormalizedPluggyTransaction Normalize(PluggyAccount account, PluggyTransaction transaction)
    {
        var status = transaction.Status?.Trim().ToUpperInvariant();
        var canceled = status is "CANCELED" or "CANCELLED" or "VOID" or "REVERSED";
        var pending = status is "PENDING" or "PROCESSING" or "SCHEDULED";
        var operationType = transaction.OperationType?.Trim().ToUpperInvariant();
        var operation = $"{transaction.OperationType} {transaction.Category} {transaction.Description}".ToUpperInvariant();
        var explicitInvoicePayment = operationType is not null && ExplicitInvoicePaymentTypes.Contains(operationType);
        var creditCardPayment = account.Type.Equals("CREDIT", StringComparison.OrdinalIgnoreCase) && CreditCardPaymentTerms.Any(operation.Contains);
        if (explicitInvoicePayment || creditCardPayment)
            return new(ExternalTransactionKind.InvoicePayment, null, Math.Abs(transaction.Amount), canceled, pending, false);
        if (operationType is not null && TransferTerms.Contains(operationType)) return new(ExternalTransactionKind.Transfer, null, Math.Abs(transaction.Amount), canceled, pending, false);
        if (operation.Contains("TRANSFERENCIA", StringComparison.Ordinal) || operation.Contains("TRANSFERÊNCIA", StringComparison.Ordinal))
            return new(ExternalTransactionKind.Transfer, null, Math.Abs(transaction.Amount), canceled, pending, false);

        // Pluggy's explicit debit/credit semantic is authoritative. The amount sign is retained
        // only as monetary magnitude and never used by itself to decide income versus expense.
        var direction = transaction.Type?.Trim().ToUpperInvariant();
        if (direction is "DEBIT" or "OUTFLOW")
            return new(ExternalTransactionKind.Expense, FinancialTransactionType.Expense, Math.Abs(transaction.Amount), canceled, pending, transaction.Amount != 0);
        if (direction is "CREDIT" or "INFLOW")
        {
            var refund = account.Type.Equals("CREDIT", StringComparison.OrdinalIgnoreCase) || operation.Contains("REFUND") || operation.Contains("ESTORNO");
            if (refund) return new(ExternalTransactionKind.Refund, FinancialTransactionType.Expense,
                Math.Abs(transaction.Amount), canceled, pending, transaction.Amount != 0, true);
            return new(ExternalTransactionKind.Income, FinancialTransactionType.Income, Math.Abs(transaction.Amount), canceled, pending, transaction.Amount != 0);
        }
        return new(ExternalTransactionKind.Unknown, null, Math.Abs(transaction.Amount), canceled, pending, false);
    }
}
