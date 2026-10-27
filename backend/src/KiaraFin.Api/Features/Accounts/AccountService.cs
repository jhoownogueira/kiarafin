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
        Guid householdId,
        int page,
        int pageSize,
        bool? active,
        CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        (page, pageSize) = Pagination.Normalize(page, pageSize);
        var query = dbContext.FinancialAccounts.AsNoTracking().Where(x => x.HouseholdId == householdId);
        if (active is not null)
            query = query.Where(x => x.IsActive == active);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AccountResponse(
                x.Id, x.Name, x.Type, x.Institution, x.Color, x.Icon,
                x.InitialBalance, x.InitialBalanceDate,
                x.InitialBalance + (dbContext.AccountMovements.Where(m => m.AccountId == x.Id).Sum(m => (decimal?)m.SignedAmount) ?? 0),
                dbContext.AccountMovements.Where(m => m.AccountId == x.Id && m.SignedAmount > 0).Sum(m => (decimal?)m.SignedAmount) ?? 0,
                -(dbContext.AccountMovements.Where(m => m.AccountId == x.Id && m.SignedAmount < 0).Sum(m => (decimal?)m.SignedAmount) ?? 0),
                x.ResponsibleUserId, x.IsActive, x.SourceProvider, x.ExternalId, x.InstitutionControlled))
            .ToListAsync(cancellationToken);
        return new PagedResponse<AccountResponse>(items, page, pageSize, total);
    }

    public async Task<AccountResponse> GetAsync(Guid householdId, Guid accountId, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        var account = await dbContext.FinancialAccounts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == accountId && x.HouseholdId == householdId, cancellationToken)
            ?? throw new NotFoundException("Conta não encontrada.");
        return await MapAsync(account, cancellationToken);
    }

    public async Task<AccountResponse> CreateAsync(
        Guid householdId,
        CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        await access.RequireMemberUserAsync(householdId, request.ResponsibleUserId, cancellationToken);
        var account = new FinancialAccount(
            householdId, request.Name, request.Type, request.InitialBalance,
            request.InitialBalanceDate, request.ResponsibleUserId,
            request.Institution, request.Color, request.Icon);
        dbContext.FinancialAccounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapAsync(account, cancellationToken);
    }

    public async Task<AccountResponse> UpdateAsync(
        Guid householdId,
        Guid accountId,
        UpdateAccountRequest request,
        CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        await access.RequireMemberUserAsync(householdId, request.ResponsibleUserId, cancellationToken);
        var account = await FindAsync(householdId, accountId, cancellationToken);
        if (account.InstitutionControlled)
            throw new ConflictException("Os campos desta conta são controlados pela instituição financeira.");
        account.Update(request.Name, request.Type, request.ResponsibleUserId,
            request.Institution, request.Color, request.Icon, DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapAsync(account, cancellationToken);
    }

    public async Task SetActiveAsync(
        Guid householdId,
        Guid accountId,
        bool active,
        CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        var account = await FindAsync(householdId, accountId, cancellationToken);
        if (account.InstitutionControlled)
            throw new ConflictException("Esta conta é controlada pela instituição financeira.");
        account.SetActive(active, DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResponse<AccountMovementResponse>> HistoryAsync(
        Guid householdId,
        Guid accountId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        _ = await FindAsync(householdId, accountId, cancellationToken);
        (page, pageSize) = Pagination.Normalize(page, pageSize);
        var query = dbContext.AccountMovements.AsNoTracking()
            .Where(x => x.HouseholdId == householdId && x.AccountId == accountId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.MovementDate).ThenByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Join(dbContext.FinancialTransactions.AsNoTracking(), movement => movement.TransactionId,
                transaction => transaction.Id,
                (movement, transaction) => new AccountMovementResponse(
                    movement.Id, movement.TransactionId, transaction.Description,
                    movement.SignedAmount, movement.Kind, movement.MovementDate, movement.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return new PagedResponse<AccountMovementResponse>(items, page, pageSize, total);
    }

    private async Task<FinancialAccount> FindAsync(Guid householdId, Guid accountId, CancellationToken cancellationToken) =>
        await dbContext.FinancialAccounts.SingleOrDefaultAsync(
            x => x.Id == accountId && x.HouseholdId == householdId,
            cancellationToken) ?? throw new NotFoundException("Conta não encontrada.");

    private async Task<AccountResponse> MapAsync(FinancialAccount account, CancellationToken cancellationToken)
    {
        var movements = dbContext.AccountMovements.AsNoTracking().Where(x => x.AccountId == account.Id);
        var movementBalance = await movements.SumAsync(x => (decimal?)x.SignedAmount, cancellationToken) ?? 0;
        var income = await movements.Where(x => x.SignedAmount > 0).SumAsync(x => (decimal?)x.SignedAmount, cancellationToken) ?? 0;
        var expenses = -(await movements.Where(x => x.SignedAmount < 0).SumAsync(x => (decimal?)x.SignedAmount, cancellationToken) ?? 0);
        return new AccountResponse(
            account.Id, account.Name, account.Type, account.Institution, account.Color, account.Icon,
            account.InitialBalance, account.InitialBalanceDate, account.InitialBalance + movementBalance,
            income, expenses, account.ResponsibleUserId, account.IsActive,
            account.SourceProvider, account.ExternalId, account.InstitutionControlled);
    }
}
