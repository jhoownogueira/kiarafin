using System.Globalization;
using KiaraFin.Api.Common;
using KiaraFin.Api.Data;
using KiaraFin.Api.Features.Cards;
using Microsoft.EntityFrameworkCore;

namespace KiaraFin.Api.Features.Forecasts;

public sealed record BalanceForecastCard(
    Guid CardId,
    string Name,
    decimal CurrentInvoice,
    DateOnly? CurrentInvoiceDueDate,
    decimal OutstandingClosedInvoices,
    decimal Total);

public sealed record BalanceForecastEntry(Guid Id, string Name, ForecastEntryType Type, decimal Amount, int ExpectedDay);

public sealed record BalanceForecastResponse(
    string Period,
    decimal Salary,
    decimal ExtraIncome,
    decimal FixedExpenses,
    decimal CardInvoices,
    decimal Net,
    IReadOnlyList<BalanceForecastCard> Cards,
    IReadOnlyList<BalanceForecastEntry> Entries,
    IReadOnlyList<CategoryInvoiceGroup> CurrentInvoiceByCategory);

/// <summary>
/// Previsão do mês atual:
///   resultado = salários + rendas extras − contas fixas − fatura atual dos cartões.
/// A fatura atual é a fatura aberta calculada a partir do Open Finance (com as parcelas
/// projetadas), somada ao saldo de faturas fechadas que ainda não venceram nem foram pagas.
/// </summary>
public sealed class BalanceForecastService(AppDbContext dbContext, CardService cards)
{
    public async Task<BalanceForecastResponse> CalculateAsync(Guid householdId, CancellationToken cancellationToken)
    {
        var today = BusinessDate.Today();
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var entries = await dbContext.ForecastEntries.AsNoTracking()
            .Where(x => x.HouseholdId == householdId)
            .OrderBy(x => x.Type).ThenBy(x => x.ExpectedDay).ThenBy(x => x.Name)
            .Select(x => new BalanceForecastEntry(x.Id, x.Name, x.Type, x.Amount, x.ExpectedDay))
            .ToListAsync(cancellationToken);

        var activeCards = await cards.LoadActiveAsync(householdId, today, cancellationToken);
        var byCategory = await cards.CurrentInvoiceByCategoryAsync(householdId, activeCards, cancellationToken);
        var cardItems = activeCards.Select(x =>
        {
            var current = x.Invoices.Current.Total;
            var outstanding = x.Invoices.OutstandingClosedDueFrom(today);
            return new BalanceForecastCard(x.Card.Id, x.Card.Name, current, x.Invoices.Current.DueDate,
                outstanding, current + outstanding);
        }).ToList();

        var salary = entries.Where(x => x.Type == ForecastEntryType.Salary).Sum(x => x.Amount);
        var extra = entries.Where(x => x.Type == ForecastEntryType.ExtraIncome).Sum(x => x.Amount);
        var fixedExpenses = entries.Where(x => x.Type == ForecastEntryType.FixedExpense).Sum(x => x.Amount);
        var cardTotal = cardItems.Sum(x => x.Total);
        return new BalanceForecastResponse(
            monthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            salary, extra, fixedExpenses, cardTotal, salary + extra - fixedExpenses - cardTotal,
            cardItems, entries, byCategory);
    }
}
