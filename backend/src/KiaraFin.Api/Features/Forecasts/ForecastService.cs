using KiaraFin.Api.Common.Exceptions;
using KiaraFin.Api.Data;
using KiaraFin.Api.Features.Dashboard;
using KiaraFin.Api.Features.Households;
using KiaraFin.Api.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace KiaraFin.Api.Features.Forecasts;

public sealed class ForecastService(
    AppDbContext dbContext,
    HouseholdAccessService access,
    DashboardService? dashboard = null,
    IAccountNotificationService? notifications = null,
    ILogger<ForecastService>? logger = null)
{
    public async Task<IReadOnlyList<ForecastEntryResponse>> ListAsync(
        Guid householdId, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        var entries = await dbContext.ForecastEntries.AsNoTracking()
            .Where(x => x.HouseholdId == householdId)
            .OrderBy(x => x.Type)
            .ThenBy(x => x.ExpectedDay)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return entries.Select(Map).ToList();
    }

    public async Task<ForecastEntryResponse> GetAsync(
        Guid householdId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        return Map(await FindAsync(householdId, id, cancellationToken));
    }

    public async Task<ForecastEntryResponse> CreateAsync(
        Guid householdId, SaveForecastEntryRequest request, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        ForecastEntry entry;
        try
        {
            entry = new ForecastEntry(householdId, request.Name, request.Type, request.Amount, request.ExpectedDay);
        }
        catch (ArgumentException exception)
        {
            throw new AppValidationException(exception.Message);
        }

        var previous = await GetCurrentMonthResultAsync(householdId, cancellationToken);
        dbContext.ForecastEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        await NotifyForecastChangeAsync(householdId, previous, cancellationToken);
        return Map(entry);
    }

    public async Task<ForecastEntryResponse> UpdateAsync(
        Guid householdId, Guid id, SaveForecastEntryRequest request, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        var entry = await FindAsync(householdId, id, cancellationToken);
        var previous = await GetCurrentMonthResultAsync(householdId, cancellationToken);
        try
        {
            entry.Update(request.Name, request.Type, request.Amount, request.ExpectedDay, DateTime.UtcNow);
        }
        catch (ArgumentException exception)
        {
            throw new AppValidationException(exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await NotifyForecastChangeAsync(householdId, previous, cancellationToken);
        return Map(entry);
    }

    public async Task DeleteAsync(Guid householdId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        var previous = await GetCurrentMonthResultAsync(householdId, cancellationToken);
        dbContext.ForecastEntries.Remove(await FindAsync(householdId, id, cancellationToken));
        await dbContext.SaveChangesAsync(cancellationToken);
        await NotifyForecastChangeAsync(householdId, previous, cancellationToken);
    }

    private async Task<ForecastEntry> FindAsync(Guid householdId, Guid id, CancellationToken cancellationToken) =>
        await dbContext.ForecastEntries.SingleOrDefaultAsync(
            x => x.Id == id && x.HouseholdId == householdId, cancellationToken)
        ?? throw new NotFoundException("Previsão não encontrada.");

    private static ForecastEntryResponse Map(ForecastEntry entry) => new(
        entry.Id, entry.Name, entry.Type, entry.Amount, entry.ExpectedDay,
        entry.CreatedAtUtc, entry.UpdatedAtUtc);

    private async Task<decimal?> GetCurrentMonthResultAsync(
        Guid householdId, CancellationToken cancellationToken)
    {
        if (dashboard is null || notifications is null) return null;
        var analysis = await dashboard.GetBalanceForecastAsync(householdId, cancellationToken);
        return analysis.Months.FirstOrDefault()?.Net;
    }

    private async Task NotifyForecastChangeAsync(
        Guid householdId, decimal? previousResult, CancellationToken cancellationToken)
    {
        if (dashboard is null || notifications is null) return;
        try
        {
            var email = await dbContext.Users.AsNoTracking()
                .Where(x => x.Id == access.UserId)
                .Select(x => x.Email)
                .SingleAsync(cancellationToken);
            var analysis = await dashboard.GetBalanceForecastAsync(householdId, cancellationToken);
            var current = analysis.Months[0];
            await notifications.NotifyForecastAsync(new BalanceForecastNotification(
                email, BalanceForecastNotificationReason.ForecastEdited,
                current.Salary, current.ExtraIncome, current.FixedExpenses,
                current.CardInvoices + current.EstimatedCardInvoices,
                current.Net, previousResult, DateTime.UtcNow), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger?.LogWarning("Forecast notification failed after a successful forecast change");
        }
    }
}
