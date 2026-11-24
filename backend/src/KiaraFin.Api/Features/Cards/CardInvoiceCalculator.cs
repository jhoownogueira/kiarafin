using System.Text.RegularExpressions;

namespace KiaraFin.Api.Features.Cards;

public sealed record InvoiceLine(CardTransaction Transaction, decimal Effect);

public sealed record OpenInvoice(
    decimal Total,
    DateOnly? ClosingDate,
    DateOnly? DueDate,
    IReadOnlyList<InvoiceLine> Lines);

public sealed record ClosedInvoice(
    CardBill Bill,
    decimal PaidAmount,
    decimal Outstanding,
    bool IsPaid,
    IReadOnlyList<InvoiceLine> Lines);

public sealed record CardInvoices(
    OpenInvoice Current,
    IReadOnlyList<InvoiceLine> Upcoming,
    IReadOnlyList<ClosedInvoice> Closed,
    IReadOnlyList<CardTransaction> Payments)
{
    /// <summary>Saldo ainda devido de faturas fechadas que vencem a partir do mês informado.</summary>
    public decimal OutstandingClosedFrom(DateOnly monthStart) =>
        Closed.Where(x => !x.IsPaid && x.Bill.DueDate >= monthStart).Sum(x => x.Outstanding);
}

/// <summary>
/// Monta as faturas de um cartão a partir dos dados brutos da Pluggy.
///
/// Regras:
/// - Fatura fechada = cada item de /bills; seu total é o totalAmount oficial do banco.
/// - Fatura aberta (atual) = lançamentos sem billId de uma fatura fechada conhecida,
///   excluindo pagamentos de fatura e parcelas futuras.
/// - Parcela futura = lançamento com data posterior a hoje, ou parcela de número maior
///   que a menor parcela ainda em aberto da mesma compra (bancos que publicam todas
///   as parcelas de uma vez).
/// </summary>
public static partial class CardInvoiceCalculator
{
    public static CardInvoices Calculate(CreditCard card, IReadOnlyList<CardBill> bills,
        IReadOnlyList<CardTransaction> transactions, DateOnly today)
    {
        var billsById = bills.ToDictionary(x => x.ExternalId, StringComparer.Ordinal);
        var payments = transactions.Where(x => x.Kind == CardTransactionKind.Payment)
            .OrderBy(x => x.Date).ToList();
        var charges = transactions.Where(x => x.Kind != CardTransactionKind.Payment).ToList();

        var billed = charges.Where(x => x.BillExternalId is not null && billsById.ContainsKey(x.BillExternalId))
            .ToLookup(x => x.BillExternalId!, StringComparer.Ordinal);
        var unbilled = charges.Where(x => x.BillExternalId is null || !billsById.ContainsKey(x.BillExternalId)).ToList();

        var upcoming = new List<CardTransaction>();
        var current = new List<CardTransaction>();
        foreach (var item in unbilled)
            (item.Date > today ? upcoming : current).Add(item);

        // Bancos que publicam todas as parcelas de uma vez: só a menor parcela em aberto
        // de cada compra pertence à fatura atual.
        foreach (var group in current.Where(x => x.InstallmentNumber is not null && x.TotalInstallments is > 1)
                     .GroupBy(InstallmentKey).ToList())
        {
            var first = group.Min(x => x.InstallmentNumber!.Value);
            foreach (var later in group.Where(x => x.InstallmentNumber > first))
            {
                current.Remove(later);
                upcoming.Add(later);
            }
        }

        var orderedBills = bills.OrderByDescending(x => x.DueDate).ToList();
        var closed = orderedBills.Select((bill, index) =>
        {
            var lines = billed[bill.ExternalId].OrderByDescending(x => x.Date).Select(Line).ToList();
            var paid = PaidAmount(bill, index > 0 ? orderedBills[index - 1] : null, payments);
            var outstanding = Math.Max(0, bill.TotalAmount - paid);
            return new ClosedInvoice(bill, paid, outstanding, bill.TotalAmount <= 0 || outstanding <= 0.01m, lines);
        }).ToList();

        var (closing, due) = EstimateCurrentDates(card, orderedBills.FirstOrDefault());
        var currentLines = current.OrderByDescending(x => x.Date).Select(Line).ToList();
        return new CardInvoices(
            new OpenInvoice(currentLines.Sum(x => x.Effect), closing, due, currentLines),
            upcoming.OrderBy(x => x.Date).ThenBy(x => x.Description).Select(Line).ToList(),
            closed,
            payments);
    }

    private static InvoiceLine Line(CardTransaction x) => new(x, x.InvoiceEffect);

    private static decimal PaidAmount(CardBill bill, CardBill? newer, IReadOnlyList<CardTransaction> payments)
    {
        // Pagamentos feitos depois do fechamento desta fatura e até o fechamento da fatura
        // seguinte pertencem a ela. Sem data de fechamento, usamos uma janela antes do vencimento.
        var from = bill.ClosingDate ?? bill.DueDate.AddDays(-15);
        var to = newer is null ? DateOnly.MaxValue : newer.ClosingDate ?? newer.DueDate.AddDays(-15);
        var byTransactions = payments.Where(x => x.Date > from && x.Date <= to).Sum(x => x.Amount);
        // bill.payments e os lançamentos de pagamento descrevem os mesmos pagamentos: usa o maior.
        return Math.Max(bill.PaidAmount, byTransactions);
    }

    private static (DateOnly? Closing, DateOnly? Due) EstimateCurrentDates(CreditCard card, CardBill? lastBill)
    {
        // Datas informadas pelo banco valem quando são posteriores à última fatura fechada.
        if (lastBill is null || card.BalanceDueDate > lastBill.DueDate)
            return (card.BalanceCloseDate, card.BalanceDueDate);
        return (lastBill.ClosingDate?.AddMonths(1), lastBill.DueDate.AddMonths(1));
    }

    private static (string Description, int Total, decimal Amount) InstallmentKey(CardTransaction x) =>
        (NormalizeDescription(x.Description), x.TotalInstallments!.Value, x.Amount);

    internal static string NormalizeDescription(string value)
    {
        var upper = value.Trim().ToUpperInvariant();
        upper = InstallmentSuffix().Replace(upper, " ");
        return MultipleSpaces().Replace(upper, " ").Trim();
    }

    [GeneratedRegex(@"\b(PARC(ELA)?\.?\s*)?\d{1,2}\s*(/|DE)\s*\d{1,2}\b")]
    private static partial Regex InstallmentSuffix();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultipleSpaces();
}
