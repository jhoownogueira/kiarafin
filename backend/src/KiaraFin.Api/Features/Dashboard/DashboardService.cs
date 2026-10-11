using System.Globalization;
using KiaraFin.Api.Common;
using KiaraFin.Api.Common.Exceptions;
using KiaraFin.Api.Data;
using KiaraFin.Api.Features.Finance;
using KiaraFin.Api.Features.Households;
using Microsoft.EntityFrameworkCore;

namespace KiaraFin.Api.Features.Dashboard;

public sealed class DashboardService(AppDbContext dbContext, HouseholdAccessService access)
{
    public async Task<DashboardResponse> GetAsync(
        Guid householdId, string? month, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        var monthStart = ParseMonth(month);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var today = BusinessDate.Today();

        var activeAccountIds = dbContext.FinancialAccounts.AsNoTracking()
            .Where(x => x.HouseholdId == householdId && x.IsActive).Select(x => x.Id);
        var initialBalance = await dbContext.FinancialAccounts.AsNoTracking()
            .Where(x => x.HouseholdId == householdId && x.IsActive)
            .SumAsync(x => (decimal?)x.InitialBalance, cancellationToken) ?? 0;
        var movementBalance = await dbContext.AccountMovements.AsNoTracking()
            .Where(x => x.HouseholdId == householdId && activeAccountIds.Contains(x.AccountId))
            .SumAsync(x => (decimal?)x.SignedAmount, cancellationToken) ?? 0;
        var availableBalance = initialBalance + movementBalance;

        var consumption = dbContext.FinancialTransactions.AsNoTracking().Where(x =>
            x.HouseholdId == householdId &&
            x.CompetenceDate >= monthStart && x.CompetenceDate <= monthEnd &&
            x.Status != FinancialTransactionStatus.Canceled &&
            x.Type != FinancialTransactionType.Transfer &&
            x.Type != FinancialTransactionType.InvoicePayment);
        var monthIncome = await consumption.Where(x => x.Type == FinancialTransactionType.Income)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;
        var monthExpenses = await consumption.Where(x => x.Type == FinancialTransactionType.Expense)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;

        var projectionStart = monthStart > today ? monthStart : today;
        var pending = await dbContext.FinancialTransactions.AsNoTracking().Where(x =>
                x.HouseholdId == householdId &&
                x.Status == FinancialTransactionStatus.Pending &&
                x.DueDate >= projectionStart && x.DueDate <= monthEnd &&
                (x.Type == FinancialTransactionType.Income ||
                 (x.Type == FinancialTransactionType.Expense && x.AccountId != null && x.CreditCardId == null)))
            .Select(x => new { x.Amount, x.Type, x.DueDate, x.RecurringTransactionId })
            .ToListAsync(cancellationToken);
        var invoicesForProjection = await dbContext.Invoices.AsNoTracking().Where(x =>
                x.HouseholdId == householdId && x.DueDate >= projectionStart && x.DueDate <= monthEnd &&
                x.Status != InvoiceStatus.Paid && x.Status != InvoiceStatus.Canceled)
            .Select(x => new { x.TotalAmount, x.DueDate })
            .ToListAsync(cancellationToken);

        var projectionEntries = pending.Select(x => new ProjectionEntry(
                x.Amount,
                x.Type == FinancialTransactionType.Income
                    ? ProjectionEntryKind.ExpectedIncome
                    : ProjectionEntryKind.PendingExpense,
                x.DueDate))
            .Concat(invoicesForProjection.Select(x =>
                new ProjectionEntry(x.TotalAmount, ProjectionEntryKind.InvoiceDue, x.DueDate)))
            .ToList();
        await AddMissingRecurrencesAsync(
            householdId, projectionStart, monthEnd,
            pending.Where(x => x.RecurringTransactionId != null).Select(x => x.RecurringTransactionId!.Value).ToHashSet(),
            projectionEntries, cancellationToken);
        var projection = FinancialProjectionCalculator.Calculate(availableBalance, monthEnd, projectionEntries);

        var upcomingBills = await dbContext.FinancialTransactions.AsNoTracking().CountAsync(x =>
            x.HouseholdId == householdId && x.Status == FinancialTransactionStatus.Pending &&
            x.Type == FinancialTransactionType.Expense && x.CreditCardId == null &&
            x.DueDate >= today && x.DueDate <= today.AddDays(30), cancellationToken);
        var openInvoices = await dbContext.Invoices.AsNoTracking().Where(x =>
                x.HouseholdId == householdId && x.Status != InvoiceStatus.Paid && x.Status != InvoiceStatus.Canceled)
            .SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0;
        var committedInstallments = await dbContext.FinancialTransactions.AsNoTracking().Where(x =>
                x.HouseholdId == householdId && x.InstallmentPlanId != null &&
                x.Status != FinancialTransactionStatus.Paid &&
                x.Status != FinancialTransactionStatus.Received &&
                x.Status != FinancialTransactionStatus.Canceled)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;

        var indicators = new DashboardIndicators(
            availableBalance, monthIncome, monthExpenses, projection.ProjectedBalance,
            upcomingBills, openInvoices, committedInstallments);
        var incomeExpenses = await IncomeExpenseSeriesAsync(householdId, monthStart, cancellationToken);
        var categories = await CategorySeriesAsync(consumption, cancellationToken);
        var balances = await BalanceSeriesAsync(householdId, monthStart, cancellationToken);
        var forecast = await ForecastAsync(householdId, monthStart, cancellationToken);
        var upcoming = await UpcomingAsync(householdId, today, cancellationToken);
        var latest = await LatestAsync(householdId, today, cancellationToken);
        var invoiceItems = await InvoiceItemsAsync(householdId, today, cancellationToken);
        var cards = await CardItemsAsync(householdId, cancellationToken);

        return new DashboardResponse(
            monthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture), indicators,
            incomeExpenses, categories, balances, forecast, upcoming, latest, invoiceItems, cards);
    }

    private async Task AddMissingRecurrencesAsync(
        Guid householdId, DateOnly from, DateOnly to, HashSet<Guid> generatedIds,
        List<ProjectionEntry> entries, CancellationToken cancellationToken)
    {
        var rules = await dbContext.RecurringTransactions.AsNoTracking().Where(x =>
                x.HouseholdId == householdId && x.Status == RecurringTransactionStatus.Active &&
                x.StartDate <= to && (x.EndDate == null || x.EndDate >= from))
            .Select(x => new { x.Id, x.Type, x.ExpectedAmount, x.DueDay })
            .ToListAsync(cancellationToken);
        foreach (var rule in rules.Where(x => !generatedIds.Contains(x.Id)))
        {
            var dueDate = MonthlyDateCalculator.Resolve(to.Year, to.Month, rule.DueDay);
            if (dueDate < from || dueDate > to)
                continue;
            entries.Add(new ProjectionEntry(
                rule.ExpectedAmount,
                rule.Type == FinancialTransactionType.Income
                    ? ProjectionEntryKind.ExpectedIncome
                    : ProjectionEntryKind.PendingExpense,
                dueDate));
        }
    }

    private async Task<IReadOnlyList<IncomeExpensePoint>> IncomeExpenseSeriesAsync(
        Guid householdId, DateOnly selectedMonth, CancellationToken cancellationToken)
    {
        var from = selectedMonth.AddMonths(-5);
        var through = selectedMonth.AddMonths(1).AddDays(-1);
        var values = await dbContext.FinancialTransactions.AsNoTracking().Where(x =>
                x.HouseholdId == householdId && x.CompetenceDate >= from && x.CompetenceDate <= through &&
                x.Status != FinancialTransactionStatus.Canceled &&
                (x.Type == FinancialTransactionType.Income || x.Type == FinancialTransactionType.Expense))
            .GroupBy(x => new { x.CompetenceDate.Year, x.CompetenceDate.Month })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                Income = group.Where(x => x.Type == FinancialTransactionType.Income).Sum(x => x.Amount),
                Expenses = group.Where(x => x.Type == FinancialTransactionType.Expense).Sum(x => x.Amount)
            }).ToListAsync(cancellationToken);
        return Enumerable.Range(0, 6).Select(index => from.AddMonths(index)).Select(period =>
        {
            var value = values.SingleOrDefault(x => x.Year == period.Year && x.Month == period.Month);
            return new IncomeExpensePoint(period.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                value?.Income ?? 0, value?.Expenses ?? 0);
        }).ToList();
    }

    private async Task<IReadOnlyList<CategoryExpensePoint>> CategorySeriesAsync(
        IQueryable<FinancialTransaction> consumption, CancellationToken cancellationToken)
    {
        var values = await consumption
            .Where(x => x.Type == FinancialTransactionType.Expense && x.CategoryId != null)
            .Join(dbContext.Categories.AsNoTracking(), transaction => transaction.CategoryId,
                category => category.Id, (transaction, category) => new { transaction, category })
            .GroupBy(x => new { x.category.Id, x.category.Name, x.category.Color, x.category.Icon })
            .Select(group => new
            {
                group.Key.Id,
                group.Key.Name,
                group.Key.Color,
                group.Key.Icon,
                Amount = group.Sum(x => x.transaction.Amount)
            })
            .OrderByDescending(x => x.Amount)
            .ToListAsync(cancellationToken);

        return values.Select(x => new CategoryExpensePoint(
            x.Id, x.Name, x.Color, x.Icon, x.Amount)).ToList();
    }

    private async Task<IReadOnlyList<BalancePoint>> BalanceSeriesAsync(
        Guid householdId, DateOnly selectedMonth, CancellationToken cancellationToken)
    {
        var from = selectedMonth.AddMonths(-5);
        var through = selectedMonth.AddMonths(1).AddDays(-1);
        var accounts = await dbContext.FinancialAccounts.AsNoTracking()
            .Where(x => x.HouseholdId == householdId && x.IsActive)
            .Select(x => new { x.Id, x.InitialBalance, x.InitialBalanceDate }).ToListAsync(cancellationToken);
        var accountIds = accounts.Select(x => x.Id).ToList();
        var movements = await dbContext.AccountMovements.AsNoTracking()
            .Where(x => x.HouseholdId == householdId && accountIds.Contains(x.AccountId) && x.MovementDate <= through)
            .Select(x => new { x.SignedAmount, x.MovementDate }).ToListAsync(cancellationToken);
        return Enumerable.Range(0, 6).Select(index => from.AddMonths(index)).Select(period =>
        {
            var end = period.AddMonths(1).AddDays(-1);
            var balance = accounts.Where(x => x.InitialBalanceDate <= end).Sum(x => x.InitialBalance) +
                          movements.Where(x => x.MovementDate <= end).Sum(x => x.SignedAmount);
            return new BalancePoint(period.ToString("yyyy-MM", CultureInfo.InvariantCulture), balance);
        }).ToList();
    }

    private async Task<IReadOnlyList<ForecastPoint>> ForecastAsync(
        Guid householdId, DateOnly selectedMonth, CancellationToken cancellationToken)
    {
        var through = selectedMonth.AddMonths(4).AddDays(-1);
        var pending = await dbContext.FinancialTransactions.AsNoTracking().Where(x =>
                x.HouseholdId == householdId && x.Status == FinancialTransactionStatus.Pending &&
                x.DueDate >= selectedMonth && x.DueDate <= through &&
                (x.Type == FinancialTransactionType.Income ||
                 (x.Type == FinancialTransactionType.Expense && x.CreditCardId == null)))
            .Select(x => new { x.DueDate, x.Type, x.Amount }).ToListAsync(cancellationToken);
        var invoices = await dbContext.Invoices.AsNoTracking().Where(x =>
                x.HouseholdId == householdId && x.DueDate >= selectedMonth && x.DueDate <= through &&
                x.Status != InvoiceStatus.Paid && x.Status != InvoiceStatus.Canceled)
            .Select(x => new { x.DueDate, x.TotalAmount }).ToListAsync(cancellationToken);
        return Enumerable.Range(0, 4).Select(index => selectedMonth.AddMonths(index)).Select(period =>
        {
            var income = pending.Where(x => x.DueDate.Year == period.Year && x.DueDate.Month == period.Month &&
                                            x.Type == FinancialTransactionType.Income).Sum(x => x.Amount);
            var expenses = pending.Where(x => x.DueDate.Year == period.Year && x.DueDate.Month == period.Month &&
                                              x.Type == FinancialTransactionType.Expense).Sum(x => x.Amount);
            var invoiceTotal = invoices.Where(x => x.DueDate.Year == period.Year && x.DueDate.Month == period.Month)
                .Sum(x => x.TotalAmount);
            return new ForecastPoint(period.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                income, expenses, invoiceTotal, income - expenses - invoiceTotal);
        }).ToList();
    }

    private async Task<IReadOnlyList<UpcomingDueItem>> UpcomingAsync(
        Guid householdId, DateOnly today, CancellationToken cancellationToken) =>
        await dbContext.FinancialTransactions.AsNoTracking().Where(x =>
                x.HouseholdId == householdId && x.Status == FinancialTransactionStatus.Pending &&
                x.Type == FinancialTransactionType.Expense && x.CreditCardId == null && x.DueDate >= today)
            .OrderBy(x => x.DueDate).Take(5)
            .Select(x => new UpcomingDueItem(x.Id, "transaction", x.Description, x.Amount, x.DueDate))
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<LatestTransactionItem>> LatestAsync(
        Guid householdId, DateOnly today, CancellationToken cancellationToken)
    {
        var items = await dbContext.FinancialTransactions.AsNoTracking().Where(x => x.HouseholdId == householdId)
            .OrderByDescending(x => x.CreatedAtUtc).Take(8)
            .Select(x => new { x.Id, x.Description, x.Amount, x.Type, x.Status, x.DueDate, x.CompetenceDate })
            .ToListAsync(cancellationToken);
        return items.Select(x => new LatestTransactionItem(
            x.Id, x.Description, x.Amount, x.Type,
            x.Status == FinancialTransactionStatus.Pending && x.DueDate < today
                ? FinancialTransactionStatus.Overdue : x.Status,
            x.CompetenceDate)).ToList();
    }

    private async Task<IReadOnlyList<DashboardInvoiceItem>> InvoiceItemsAsync(
        Guid householdId, DateOnly today, CancellationToken cancellationToken) =>
        await dbContext.Invoices.AsNoTracking().Where(x =>
                x.HouseholdId == householdId && x.DueDate >= today &&
                x.Status != InvoiceStatus.Paid && x.Status != InvoiceStatus.Canceled)
            .Join(dbContext.CreditCards.AsNoTracking(), invoice => invoice.CreditCardId,
                card => card.Id, (invoice, card) => new { invoice, card })
            .OrderBy(x => x.invoice.DueDate).Take(5)
            .Select(x => new DashboardInvoiceItem(
                x.invoice.Id, x.card.Id, x.card.Name, x.invoice.TotalAmount,
                x.invoice.DueDate, x.invoice.Status)).ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<DashboardCardItem>> CardItemsAsync(
        Guid householdId, CancellationToken cancellationToken)
    {
        var cards = await dbContext.CreditCards.AsNoTracking()
            .Where(x => x.HouseholdId == householdId && x.Status == CreditCardStatus.Active)
            .Select(x => new { x.Id, x.Name, x.Color, x.TotalLimit }).ToListAsync(cancellationToken);
        var cardIds = cards.Select(x => x.Id).ToList();
        var used = await dbContext.FinancialTransactions.AsNoTracking().Where(x =>
                x.HouseholdId == householdId && x.CreditCardId != null && cardIds.Contains(x.CreditCardId.Value) &&
                x.Status != FinancialTransactionStatus.Canceled)
            .Join(dbContext.Invoices.AsNoTracking().Where(x => x.Status != InvoiceStatus.Paid && x.Status != InvoiceStatus.Canceled),
                transaction => transaction.InvoiceId, invoice => invoice.Id, (transaction, invoice) => transaction)
            .GroupBy(x => x.CreditCardId!.Value)
            .Select(group => new { CardId = group.Key, Amount = group.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.CardId, x => x.Amount, cancellationToken);
        return cards.Select(x =>
        {
            var usedLimit = used.GetValueOrDefault(x.Id);
            return new DashboardCardItem(x.Id, x.Name, x.Color, x.TotalLimit, usedLimit, x.TotalLimit - usedLimit);
        }).ToList();
    }

    private static DateOnly ParseMonth(string? month)
    {
        if (string.IsNullOrWhiteSpace(month))
        {
            var today = BusinessDate.Today();
            return new DateOnly(today.Year, today.Month, 1);
        }
        if (!DateOnly.TryParseExact($"{month}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var result))
            throw new AppValidationException("O mês deve usar o formato YYYY-MM.");
        return result;
    }
}
