using KiaraFin.Api.Common;
using KiaraFin.Api.Common.Exceptions;
using KiaraFin.Api.Data;
using KiaraFin.Api.Features.Finance;
using KiaraFin.Api.Features.Households;
using Microsoft.EntityFrameworkCore;

namespace KiaraFin.Api.Features.Accounts;

public sealed class AccountService(AppDbContext dbContext, HouseholdAccessService access)
{
    public async Task<PagedResponse<AccountResponse>> ListAsync(
        Guid householdId, int page, int pageSize, bool? active, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        (page, pageSize) = Pagination.Normalize(page, pageSize);
        var query = dbContext.FinancialAccounts.AsNoTracking().Where(x => x.HouseholdId == householdId);
        if (active is not null)
            query = query.Where(x => x.IsActive == active);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Name).Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResponse<AccountResponse>(items.Select(Map).ToList(), page, pageSize, total);
    }

    public async Task<AccountResponse> GetAsync(Guid householdId, Guid accountId, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        return Map(await FindAsync(householdId, accountId, cancellationToken));
    }

    public async Task<AccountResponse> CreateAsync(
        Guid householdId, SaveAccountRequest request, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        var account = new FinancialAccount(householdId, request.Name, request.Type,
            decimal.Round(request.Balance, 2), request.Institution, request.Color, request.Icon);
        dbContext.FinancialAccounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(account);
    }

    public async Task<AccountResponse> UpdateAsync(
        Guid householdId, Guid accountId, SaveAccountRequest request, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        var account = await FindAsync(householdId, accountId, cancellationToken);
        if (account.InstitutionControlled)
            throw new ConflictException("Os dados desta conta são controlados pela instituição financeira.");
        account.Update(request.Name, request.Type, request.Institution, request.Color, request.Icon,
            decimal.Round(request.Balance, 2), DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(account);
    }

    public async Task SetActiveAsync(Guid householdId, Guid accountId, bool active, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        var account = await FindAsync(householdId, accountId, cancellationToken);
        if (account.InstitutionControlled)
            throw new ConflictException("Esta conta é controlada pela instituição financeira.");
        account.SetActive(active, DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResponse<AccountTransactionResponse>> HistoryAsync(
        Guid householdId, Guid accountId, int page, int pageSize, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        _ = await FindAsync(householdId, accountId, cancellationToken);
        (page, pageSize) = Pagination.Normalize(page, pageSize);
        var query = dbContext.BankTransactions.AsNoTracking().Where(x => x.AccountId == accountId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.Date).ThenBy(x => x.Description)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AccountTransactionResponse(x.Id, x.Description, x.Amount, x.Date, x.IsPending, x.Category))
            .ToListAsync(cancellationToken);
        return new PagedResponse<AccountTransactionResponse>(items, page, pageSize, total);
    }

    private async Task<FinancialAccount> FindAsync(Guid householdId, Guid accountId, CancellationToken cancellationToken) =>
        await dbContext.FinancialAccounts.SingleOrDefaultAsync(
            x => x.Id == accountId && x.HouseholdId == householdId, cancellationToken)
        ?? throw new NotFoundException("Conta não encontrada.");

    private static AccountResponse Map(FinancialAccount x) => new(
        x.Id, x.Name, x.Type, x.Institution, x.Color, x.Icon, x.Balance, x.IsActive,
        x.SourceProvider, x.InstitutionControlled, x.UpdatedAtUtc);
}
