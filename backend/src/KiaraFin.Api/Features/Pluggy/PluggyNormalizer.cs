using KiaraFin.Api.Features.Cards;

namespace KiaraFin.Api.Features.Pluggy;

public static class PluggyNormalizer
{
    private static readonly string[] PaymentTerms =
    [
        "PAGAMENTO", "PAGTO", "PGTO", "PAG FATURA", "PAG. FATURA", "PAYMENT", "CREDIT_CARD_PAYMENT",
        "CARD_PAYMENT", "PAGAMENTO_FATURA", "BILL_PAYMENT"
    ];

    private static readonly string[] CreditTerms = ["ESTORNO", "REEMBOLSO", "CANCELAMENTO", "DEVOLU", "REFUND", "CASHBACK"];

    /// <summary>
    /// Classifica um lançamento de cartão. A documentação da Pluggy define que, em cartões,
    /// o valor é positivo para compras e negativo para pagamentos/créditos. O campo "type"
    /// (DEBIT/CREDIT) não é confiável para cartões e é ignorado.
    /// </summary>
    public static CardTransactionKind ClassifyCardTransaction(PluggyTransaction transaction,
        IReadOnlyCollection<decimal> billTotals)
    {
        if (transaction.Amount > 0) return CardTransactionKind.Purchase;
        var text = $"{transaction.OperationType} {transaction.Category} {transaction.Description}".ToUpperInvariant();
        if (CreditTerms.Any(text.Contains)) return CardTransactionKind.Credit;
        if (PaymentTerms.Any(text.Contains)) return CardTransactionKind.Payment;
        // Um crédito com exatamente o valor de uma fatura fechada é o pagamento dela.
        var value = Math.Abs(transaction.Amount);
        if (billTotals.Any(total => total > 0 && Math.Abs(total - value) < 0.01m)) return CardTransactionKind.Payment;
        return CardTransactionKind.Credit;
    }
}
