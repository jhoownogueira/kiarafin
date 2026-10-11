using KiaraFin.Api.Common;
using KiaraFin.Api.Common.Exceptions;
using KiaraFin.Api.Data;
using KiaraFin.Api.Features.Finance;
using KiaraFin.Api.Features.Households;
using Microsoft.EntityFrameworkCore;

namespace KiaraFin.Api.Features.Reports;

public sealed class ReportService(AppDbContext dbContext, HouseholdAccessService access)
{
    public async Task<PagedResponse<MonthlyReportItem>> MonthlyAsync(
        Guid householdId, ReportFilter filter, CancellationToken cancellationToken)
    {
        var query = await FilteredQueryAsync(householdId, filter, cancellationToken);
        var grouped = query.GroupBy(x => new { x.CompetenceDate.Year, x.CompetenceDate.Month })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                Income = group.Sum(x => x.Type == FinancialTransactionType.Income ? x.Amount : 0m),
                Expenses = group.Sum(x => x.Type == FinancialTransactionType.Expense ? x.Amount : 0m)
            })
            .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month);
        var page = await PageValuesAsync(grouped, filter, cancellationToken);
        return new PagedResponse<MonthlyReportItem>(
            page.Items.Select(x => new MonthlyReportItem(x.Year, x.Month, x.Income, x.Expenses, x.Income - x.Expenses)).ToList(),
            page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<PagedResponse<CategoryReportItem>> ByCategoryAsync(
        Guid householdId, ReportFilter filter, CancellationToken cancellationToken)
    {
        var query = (await FilteredQueryAsync(householdId, filter, cancellationToken))
            .Where(x => x.Type == FinancialTransactionType.Expense && x.CategoryId != null);
        var totalAmount = await query.SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;
        var grouped = query.Join(dbContext.Categories.AsNoTracking(), transaction => transaction.CategoryId,
                category => category.Id, (transaction, category) => new { transaction, category })
            .GroupBy(x => new { x.category.Id, x.category.Name, x.category.Color, x.category.Icon })
            .Select(group => new
            {
                group.Key.Id,
                group.Key.Name,
                group.Key.Color,
                group.Key.Icon,
                Amount = group.Sum(x => x.transaction.Amount)
            }).OrderByDescending(x => x.Amount);
        var page = await PageValuesAsync(grouped, filter, cancellationToken);
        return new PagedResponse<CategoryReportItem>(
            page.Items.Select(x => new CategoryReportItem(
                x.Id, x.Name, x.Color, x.Icon, x.Amount, totalAmount == 0 ? 0 : x.Amount / totalAmount * 100)).ToList(),
            page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<PagedResponse<ResponsibleReportItem>> ByResponsibleAsync(
        Guid householdId, ReportFilter filter, CancellationToken cancellationToken)
    {
        var query = await FilteredQueryAsync(householdId, filter, cancellationToken);
        var grouped = query.Join(dbContext.Users.AsNoTracking(), transaction => transaction.ResponsibleUserId,
                user => user.Id, (transaction, user) => new { transaction, user })
            .GroupBy(x => new { x.user.Id, x.user.Name })
            .Select(group => new
            {
                group.Key.Id,
                group.Key.Name,
                Income = group.Sum(x => x.transaction.Type == FinancialTransactionType.Income ? x.transaction.Amount : 0m),
                Expenses = group.Sum(x => x.transaction.Type == FinancialTransactionType.Expense ? x.transaction.Amount : 0m)
            }).OrderByDescending(x => x.Expenses);
        var page = await PageValuesAsync(grouped, filter, cancellationToken);
        return new PagedResponse<ResponsibleReportItem>(
            page.Items.Select(x => new ResponsibleReportItem(
                x.Id, x.Name, x.Income, x.Expenses, x.Income - x.Expenses)).ToList(),
            page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<PagedResponse<ScopeReportItem>> SharedVsIndividualAsync(
        Guid householdId, ReportFilter filter, CancellationToken cancellationToken)
    {
        var query = (await FilteredQueryAsync(householdId, filter, cancellationToken))
            .Where(x => x.Type == FinancialTransactionType.Expense);
        var totalAmount = await query.SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;
        var grouped = query.GroupBy(x => x.Scope)
            .Select(group => new { Scope = group.Key, Amount = group.Sum(x => x.Amount) })
            .OrderByDescending(x => x.Amount);
        var page = await PageValuesAsync(grouped, filter, cancellationToken);
        return new PagedResponse<ScopeReportItem>(
            page.Items.Select(x => new ScopeReportItem(
                x.Scope, x.Amount, totalAmount == 0 ? 0 : x.Amount / totalAmount * 100)).ToList(),
            page.Page, page.PageSize, page.TotalCount);
    }

    private async Task<IQueryable<FinancialTransaction>> FilteredQueryAsync(
        Guid householdId, ReportFilter filter, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        if (filter.DateFrom > filter.DateTo)
            throw new AppValidationException("A data inicial deve ser anterior à data final.");
        var query = dbContext.FinancialTransactions.AsNoTracking().Where(x =>
            x.HouseholdId == householdId &&
            x.Type != FinancialTransactionType.Transfer &&
            x.Type != FinancialTransactionType.InvoicePayment &&
            x.Status != FinancialTransactionStatus.Canceled);
        if (filter.DateFrom is not null) query = query.Where(x => x.CompetenceDate >= filter.DateFrom);
        if (filter.DateTo is not null) query = query.Where(x => x.CompetenceDate <= filter.DateTo);
        if (filter.AccountId is not null) query = query.Where(x => x.AccountId == filter.AccountId);
        if (filter.CardId is not null) query = query.Where(x => x.CreditCardId == filter.CardId);
        if (filter.CategoryId is not null) query = query.Where(x => x.CategoryId == filter.CategoryId);
        if (filter.ResponsibleUserId is not null) query = query.Where(x => x.ResponsibleUserId == filter.ResponsibleUserId);
        var today = BusinessDate.Today();
        if (filter.Status == FinancialTransactionStatus.Overdue)
            query = query.Where(x => x.Status == FinancialTransactionStatus.Pending && x.DueDate < today);
        else if (filter.Status == FinancialTransactionStatus.Pending)
            query = query.Where(x => x.Status == FinancialTransactionStatus.Pending && x.DueDate >= today);
        else if (filter.Status is not null)
            query = query.Where(x => x.Status == filter.Status);
        return query;
    }

    private static async Task<PagedResponse<T>> PageValuesAsync<T>(
        IOrderedQueryable<T> query, ReportFilter filter, CancellationToken cancellationToken)
    {
        var (page, pageSize) = Pagination.Normalize(filter.Page, filter.PageSize);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResponse<T>(items, page, pageSize, total);
    }
}
