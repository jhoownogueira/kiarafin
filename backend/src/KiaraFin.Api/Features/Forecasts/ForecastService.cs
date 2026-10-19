using KiaraFin.Api.Common.Exceptions;
using KiaraFin.Api.Data;
using KiaraFin.Api.Features.Households;
using Microsoft.EntityFrameworkCore;

namespace KiaraFin.Api.Features.Forecasts;

public sealed class ForecastService(AppDbContext dbContext, HouseholdAccessService access)
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

        dbContext.ForecastEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(entry);
    }

    public async Task<ForecastEntryResponse> UpdateAsync(
        Guid householdId, Guid id, SaveForecastEntryRequest request, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        var entry = await FindAsync(householdId, id, cancellationToken);
        try
        {
            entry.Update(request.Name, request.Type, request.Amount, request.ExpectedDay, DateTime.UtcNow);
        }
        catch (ArgumentException exception)
        {
            throw new AppValidationException(exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(entry);
    }

    public async Task DeleteAsync(Guid householdId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        dbContext.ForecastEntries.Remove(await FindAsync(householdId, id, cancellationToken));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<ForecastEntry> FindAsync(Guid householdId, Guid id, CancellationToken cancellationToken) =>
        await dbContext.ForecastEntries.SingleOrDefaultAsync(
            x => x.Id == id && x.HouseholdId == householdId, cancellationToken)
        ?? throw new NotFoundException("Previsão não encontrada.");

    private static ForecastEntryResponse Map(ForecastEntry entry) => new(
        entry.Id, entry.Name, entry.Type, entry.Amount, entry.ExpectedDay,
        entry.CreatedAtUtc, entry.UpdatedAtUtc);
}
