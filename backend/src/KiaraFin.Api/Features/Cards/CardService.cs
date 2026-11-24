using KiaraFin.Api.Common;
using KiaraFin.Api.Common.Exceptions;
using KiaraFin.Api.Data;
using KiaraFin.Api.Features.Households;
using Microsoft.EntityFrameworkCore;

namespace KiaraFin.Api.Features.Cards;

public sealed record CardWithInvoices(CreditCard Card, CardInvoices Invoices);

public sealed class CardService(AppDbContext dbContext, HouseholdAccessService access)
{
    public async Task<IReadOnlyList<CardSummaryResponse>> ListAsync(Guid householdId, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        var cards = await LoadAsync(householdId, null, BusinessDate.Today(), cancellationToken);
        return cards.Select(x => Summary(x, BusinessDate.Today())).ToList();
    }

    public async Task<CardDetailResponse> GetAsync(Guid householdId, Guid cardId, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        var today = BusinessDate.Today();
        var item = (await LoadAsync(householdId, cardId, today, cancellationToken)).SingleOrDefault()
            ?? throw new NotFoundException("Cartão não encontrado.");
        return new CardDetailResponse(
            Summary(item, today),
            Open(item.Invoices.Current, includeItems: true),
            item.Invoices.Upcoming.Select(Item).ToList(),
            item.Invoices.Closed.Select(x => Closed(x, includeItems: true)).ToList(),
            item.Invoices.Payments.OrderByDescending(x => x.Date).Select(x => Item(new InvoiceLine(x, 0))).ToList());
    }

    /// <summary>Faturas calculadas de todos os cartões ativos (usado pela previsão de saldo).</summary>
    public Task<IReadOnlyList<CardWithInvoices>> LoadActiveAsync(Guid householdId, DateOnly today,
        CancellationToken cancellationToken) => LoadAsync(householdId, null, today, cancellationToken, activeOnly: true);

    private async Task<IReadOnlyList<CardWithInvoices>> LoadAsync(Guid householdId, Guid? cardId, DateOnly today,
        CancellationToken cancellationToken, bool activeOnly = false)
    {
        var cardsQuery = dbContext.CreditCards.AsNoTracking().Where(x => x.HouseholdId == householdId);
        if (cardId is Guid id) cardsQuery = cardsQuery.Where(x => x.Id == id);
        if (activeOnly) cardsQuery = cardsQuery.Where(x => x.IsActive);
        var cards = await cardsQuery.OrderByDescending(x => x.IsActive).ThenBy(x => x.Name).ToListAsync(cancellationToken);
        var ids = cards.Select(x => x.Id).ToList();
        var bills = (await dbContext.CardBills.AsNoTracking().Where(x => ids.Contains(x.CardId)).ToListAsync(cancellationToken))
            .ToLookup(x => x.CardId);
        var transactions = (await dbContext.CardTransactions.AsNoTracking().Where(x => ids.Contains(x.CardId)).ToListAsync(cancellationToken))
            .ToLookup(x => x.CardId);
        return cards.Select(card => new CardWithInvoices(card,
            CardInvoiceCalculator.Calculate(card, bills[card.Id].ToList(), transactions[card.Id].ToList(), today))).ToList();
    }

    private static CardSummaryResponse Summary(CardWithInvoices item, DateOnly today)
    {
        var card = item.Card;
        var last = item.Invoices.Closed.FirstOrDefault();
        return new CardSummaryResponse(card.Id, card.Name, card.Institution, card.Brand, card.Number,
            card.CreditLimit, card.AvailableLimit, card.ReportedBalance, card.IsActive, card.UpdatedAtUtc,
            Open(item.Invoices.Current, includeItems: false),
            item.Invoices.OutstandingClosedDueFrom(today),
            last is null ? null : Closed(last, includeItems: false));
    }

    private static OpenInvoiceResponse Open(OpenInvoice invoice, bool includeItems) => new(
        invoice.Total, invoice.ClosingDate, invoice.DueDate, invoice.Lines.Count,
        includeItems ? invoice.Lines.Select(Item).ToList() : null);

    private static ClosedInvoiceResponse Closed(ClosedInvoice invoice, bool includeItems) => new(
        invoice.Bill.Id, invoice.Bill.DueDate, invoice.Bill.ClosingDate, invoice.Bill.TotalAmount,
        invoice.Bill.MinimumPayment, invoice.PaidAmount, invoice.Outstanding, invoice.IsPaid,
        invoice.Lines.Sum(x => x.Effect), includeItems ? invoice.Lines.Select(Item).ToList() : null);

    private static CardItemResponse Item(InvoiceLine line)
    {
        var x = line.Transaction;
        return new CardItemResponse(x.Id, x.Description, x.Amount, line.Effect, x.Kind, x.Date, x.PurchaseDate,
            x.IsPending, x.InstallmentNumber, x.TotalInstallments, x.Category, x.CurrencyCode, x.OriginalAmount,
            line.Projected);
    }
}
