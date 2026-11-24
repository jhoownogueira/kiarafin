using System.Text.RegularExpressions;

namespace KiaraFin.Api.Features.Cards;

/// <param name="Projected">Parcela ainda não publicada pela Pluggy, projetada a partir da parcela anterior.</param>
public sealed record InvoiceLine(CardTransaction Transaction, decimal Effect, bool Projected = false);

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
    /// <summary>Saldo devido de faturas fechadas que ainda não venceram.</summary>
    public decimal OutstandingClosedDueFrom(DateOnly today) =>
        Closed.Where(x => !x.IsPaid && x.Bill.DueDate >= today).Sum(x => x.Outstanding);
}

/// <summary>
/// Monta as faturas de um cartão a partir dos dados brutos da Pluggy.
///
/// Regras:
/// - Fatura fechada = cada item de /bills; seu total é o totalAmount oficial do banco.
/// - Fatura aberta (atual) = lançamentos sem billId de uma fatura fechada conhecida (exceto
///   pagamentos) + parcelas projetadas.
/// - O Open Finance só publica cada parcela quando ela é faturada. Por isso, quando a última
///   parcela conhecida de uma compra está na última fatura fechada, a parcela seguinte é
///   projetada na fatura atual e as demais nas próximas faturas.
/// - Bancos que publicam todas as parcelas de uma vez: só a menor parcela em aberto de cada
///   compra pertence à fatura atual; lançamentos com data futura também vão para as próximas.
/// - Os pagamentos listados em uma fatura (bill.payments) quitam a fatura anterior.
/// </summary>
public static partial class CardInvoiceCalculator
{
    public static CardInvoices Calculate(CreditCard card, IReadOnlyList<CardBill> bills,
        IReadOnlyList<CardTransaction> transactions, DateOnly today)
    {
        var billsById = bills.ToDictionary(x => x.ExternalId, StringComparer.Ordinal);
        var orderedBills = bills.OrderByDescending(x => x.DueDate).ToList();
        var latestBill = orderedBills.FirstOrDefault();
        var (closing, due) = EstimateCurrentDates(card, latestBill);

        var payments = transactions.Where(x => x.Kind == CardTransactionKind.Payment).OrderBy(x => x.Date).ToList();
        var charges = transactions.Where(x => x.Kind != CardTransactionKind.Payment).ToList();
        bool IsBilled(CardTransaction x) => x.BillExternalId is not null && billsById.ContainsKey(x.BillExternalId);

        var billed = charges.Where(IsBilled).ToLookup(x => x.BillExternalId!, StringComparer.Ordinal);
        var current = new List<InvoiceLine>();
        var upcoming = new List<InvoiceLine>();
        foreach (var item in charges.Where(x => !IsBilled(x)))
            (item.Date > today ? upcoming : current).Add(Line(item));

        foreach (var group in current.Where(x => IsInstallment(x.Transaction)).GroupBy(x => PurchaseKey(x.Transaction)).ToList())
        {
            var first = group.Min(x => x.Transaction.InstallmentNumber!.Value);
            foreach (var later in group.Where(x => x.Transaction.InstallmentNumber > first).ToList())
            {
                current.Remove(later);
                upcoming.Add(later);
            }
        }

        // Projeção das parcelas que o banco ainda não publicou.
        foreach (var group in charges.Where(x => x.Kind == CardTransactionKind.Purchase && IsInstallment(x)).GroupBy(PurchaseKey))
        {
            var lastNumber = group.Max(x => x.InstallmentNumber!.Value);
            var total = group.First().TotalInstallments!.Value;
            if (lastNumber >= total) continue;
            var lastItems = group.Where(x => x.InstallmentNumber == lastNumber).ToList();
            var lastBillId = lastItems[0].BillExternalId;
            // Deslocamento da próxima parcela em relação à fatura atual (0 = fatura atual).
            int firstOffset;
            if (!IsBilled(lastItems[0])) firstOffset = 1;
            else if (lastBillId == latestBill?.ExternalId) firstOffset = 0;
            else continue; // parcela seguinte deveria estar numa fatura já fechada: compra antecipada/quitada.

            foreach (var item in lastItems)
                for (var number = lastNumber + 1; number <= total; number++)
                {
                    var offset = firstOffset + (number - lastNumber - 1);
                    var line = Project(item, number, total, due?.AddMonths(offset) ?? today.AddMonths(offset));
                    (offset == 0 ? current : upcoming).Add(line);
                }
        }

        var closed = orderedBills.Select((bill, index) =>
        {
            var lines = billed[bill.ExternalId].OrderByDescending(x => x.Date).Select(Line).ToList();
            var paid = PaidAmount(bill, index > 0 ? orderedBills[index - 1] : null, payments);
            var outstanding = Math.Max(0, bill.TotalAmount - paid);
            // Tolerância de 1%: estornos/créditos aplicados podem reduzir o valor pago em centavos.
            var isPaid = bill.TotalAmount <= 0 || outstanding <= Math.Max(0.01m, bill.TotalAmount * 0.01m);
            return new ClosedInvoice(bill, paid, isPaid ? 0 : outstanding, isPaid, lines);
        }).ToList();

        var currentLines = current.OrderByDescending(x => x.Transaction.Date).ToList();
        return new CardInvoices(
            new OpenInvoice(currentLines.Sum(x => x.Effect), closing, due, currentLines),
            upcoming.OrderBy(x => x.Transaction.Date).ThenBy(x => x.Transaction.Description).ToList(),
            closed,
            payments);
    }

    private static InvoiceLine Line(CardTransaction x) => new(x, x.InvoiceEffect);

    private static bool IsInstallment(CardTransaction x) =>
        x.InstallmentNumber is not null && x.TotalInstallments is > 1;

    private static InvoiceLine Project(CardTransaction source, int number, int total, DateOnly date)
    {
        var projected = new CardTransaction(source.CardId, $"projected:{source.ExternalId}:{number}");
        projected.Apply(source.Description, source.Amount, CardTransactionKind.Purchase, date, source.PurchaseDate,
            true, null, number, total, source.Category, source.CurrencyCode, source.OriginalAmount, DateTime.UtcNow);
        return new InvoiceLine(projected, projected.InvoiceEffect, Projected: true);
    }

    private static decimal PaidAmount(CardBill bill, CardBill? newer, IReadOnlyList<CardTransaction> payments)
    {
        // A Pluggy lista em cada fatura os pagamentos feitos durante o seu ciclo, ou seja, o
        // pagamento da fatura anterior. Por isso o pagamento desta fatura está na fatura seguinte.
        var fromNewerBill = newer?.PaidAmount ?? 0;
        // Lançamentos de pagamento feitos entre o fechamento desta fatura e o da seguinte.
        var from = bill.ClosingDate ?? bill.DueDate.AddDays(-15);
        var to = newer is null ? DateOnly.MaxValue : newer.ClosingDate ?? newer.DueDate.AddDays(-15);
        var byTransactions = payments.Where(x => x.Date > from && x.Date <= to).Sum(x => x.Amount);
        return Math.Max(fromNewerBill, byTransactions);
    }

    private static (DateOnly? Closing, DateOnly? Due) EstimateCurrentDates(CreditCard card, CardBill? lastBill)
    {
        // Datas informadas pelo banco valem quando são posteriores à última fatura fechada.
        if (lastBill is null || card.BalanceDueDate > lastBill.DueDate)
            return (card.BalanceCloseDate, card.BalanceDueDate);
        return (lastBill.ClosingDate?.AddMonths(1), lastBill.DueDate.AddMonths(1));
    }

    /// <summary>
    /// Identifica a compra de uma parcela. Todas as parcelas compartilham descrição, data da compra
    /// e quantidade; o valor pode variar em centavos (arredondamento) e por isso só é usado quando
    /// a data da compra não é informada.
    /// </summary>
    private static string PurchaseKey(CardTransaction x) => x.PurchaseDate is DateOnly purchase
        ? $"{NormalizeDescription(x.Description)}|{purchase:yyyy-MM-dd}|{x.TotalInstallments}"
        : $"{NormalizeDescription(x.Description)}|{x.TotalInstallments}|{x.Amount}";

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
