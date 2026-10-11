using KiaraFin.Api.Common.Exceptions;
using KiaraFin.Api.Common.Security;
using KiaraFin.Api.Data;
using KiaraFin.Api.Features.Finance;
using KiaraFin.Api.Features.Identity;
using Microsoft.EntityFrameworkCore;

namespace KiaraFin.Api.Features.RecurringTransactions;

public sealed class RecurringTransactionService(AppDbContext dbContext, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<RecurringTransactionResponse>> ListAsync(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        await RequireMembershipAsync(householdId, cancellationToken);
        var items = await dbContext.Set<RecurringTransaction>()
            .AsNoTracking()
            .Where(x => x.HouseholdId == householdId)
            .OrderBy(x => x.DueDay)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<RecurringTransactionResponse> GetAsync(
        Guid householdId,
        Guid id,
        CancellationToken cancellationToken)
    {
        await RequireMembershipAsync(householdId, cancellationToken);
        var item = await FindAsync(householdId, id, cancellationToken);
        return Map(item);
    }

    public async Task<RecurringTransactionResponse> CreateAsync(
        Guid householdId,
        CreateRecurringTransactionRequest request,
        CancellationToken cancellationToken)
    {
        await RequireMembershipAsync(householdId, cancellationToken);
        await ValidateReferencesAsync(
            householdId, request.CategoryId, request.AccountId, request.ResponsibleUserId, request.Type, cancellationToken);

        RecurringTransaction item;
        try
        {
            item = new RecurringTransaction(
                householdId,
                request.Name,
                request.Type,
                request.CategoryId,
                request.ExpectedAmount,
                request.DueDay,
                request.AccountId,
                request.StartDate,
                request.ResponsibleUserId,
                request.Scope,
                request.EndDate);
        }
        catch (ArgumentException exception)
        {
            throw new AppValidationException(exception.Message);
        }

        dbContext.Set<RecurringTransaction>().Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(item);
    }

    public async Task<RecurringTransactionResponse> UpdateAsync(
        Guid householdId,
        Guid id,
        UpdateRecurringTransactionRequest request,
        CancellationToken cancellationToken)
    {
        await RequireMembershipAsync(householdId, cancellationToken);
        var item = await FindAsync(householdId, id, cancellationToken);
        await ValidateReferencesAsync(
            householdId, request.CategoryId, request.AccountId, request.ResponsibleUserId, item.Type, cancellationToken);
        try
        {
            item.Update(
                request.Name,
                request.ExpectedAmount,
                request.DueDay,
                request.CategoryId,
                request.AccountId,
                request.ResponsibleUserId,
                request.Scope,
                request.EndDate,
                DateTime.UtcNow);
        }
        catch (ArgumentException exception)
        {
            throw new AppValidationException(exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(item);
    }

    public Task<RecurringTransactionResponse> PauseAsync(Guid householdId, Guid id, CancellationToken cancellationToken) =>
        ChangeStatusAsync(householdId, id, static (item, now) => item.Pause(now), cancellationToken);

    public Task<RecurringTransactionResponse> ResumeAsync(Guid householdId, Guid id, CancellationToken cancellationToken) =>
        ChangeStatusAsync(householdId, id, static (item, now) => item.Resume(now), cancellationToken);

    public async Task<RecurringTransactionResponse> EndAsync(
        Guid householdId,
        Guid id,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        await RequireMembershipAsync(householdId, cancellationToken);
        var item = await FindAsync(householdId, id, cancellationToken);
        try
        {
            item.End(endDate, DateTime.UtcNow);
        }
        catch (ArgumentException exception)
        {
            throw new AppValidationException(exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(item);
    }

    public async Task DeleteAsync(Guid householdId, Guid id, CancellationToken cancellationToken)
    {
        await RequireMembershipAsync(householdId, cancellationToken);
        var item = await FindAsync(householdId, id, cancellationToken);
        if (await dbContext.Set<FinancialTransaction>()
            .AnyAsync(x => x.RecurringTransactionId == id, cancellationToken))
            throw new ConflictException("A recorrência possui histórico e deve ser encerrada em vez de excluída.");

        dbContext.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<RecurrenceGenerationResponse> GenerateMonthAsync(
        Guid householdId,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        await RequireMembershipAsync(householdId, cancellationToken);
        if (year is < 2000 or > 2200 || month is < 1 or > 12)
            throw new AppValidationException("Período inválido.");

        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var recurring = await dbContext.Set<RecurringTransaction>()
            .Where(x => x.HouseholdId == householdId &&
                        x.Status == RecurringTransactionStatus.Active &&
                        x.StartDate <= monthEnd &&
                        (x.EndDate == null || x.EndDate >= monthStart))
            .ToListAsync(cancellationToken);
        var existingIds = await dbContext.Set<FinancialTransaction>()
            .Where(x => x.HouseholdId == householdId &&
                        x.RecurringTransactionId != null &&
                        x.DueDate >= monthStart && x.DueDate <= monthEnd)
            .Select(x => x.RecurringTransactionId!.Value)
            .ToHashSetAsync(cancellationToken);

        var created = new List<FinancialTransaction>();
        foreach (var rule in recurring.Where(x => !existingIds.Contains(x.Id)))
        {
            var dueDate = MonthlyDateCalculator.Resolve(year, month, rule.DueDay);
            var occurrence = new FinancialTransaction(
                householdId,
                rule.Name,
                rule.ExpectedAmount,
                rule.Type,
                rule.CategoryId,
                rule.AccountId,
                dueDate,
                dueDate,
                rule.ResponsibleUserId,
                rule.Scope);
            occurrence.LinkRecurrence(rule.Id);
            created.Add(occurrence);
        }

        dbContext.Set<FinancialTransaction>().AddRange(created);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new RecurrenceGenerationResponse(created.Count, created.Select(x => x.Id).ToList());
    }

    private async Task<RecurringTransactionResponse> ChangeStatusAsync(
        Guid householdId,
        Guid id,
        Action<RecurringTransaction, DateTime> change,
        CancellationToken cancellationToken)
    {
        await RequireMembershipAsync(householdId, cancellationToken);
        var item = await FindAsync(householdId, id, cancellationToken);
        change(item, DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(item);
    }

    private async Task ValidateReferencesAsync(
        Guid householdId,
        Guid categoryId,
        Guid accountId,
        Guid responsibleUserId,
        FinancialTransactionType type,
        CancellationToken cancellationToken)
    {
        var expectedCategoryType = type == FinancialTransactionType.Income ? CategoryType.Income : CategoryType.Expense;
        if (!await dbContext.Set<Category>().AnyAsync(
            x => x.Id == categoryId && x.HouseholdId == householdId && x.IsActive && x.Type == expectedCategoryType,
            cancellationToken))
            throw new AppValidationException("Categoria inválida para o núcleo e tipo informados.");
        if (!await dbContext.Set<FinancialAccount>().AnyAsync(
            x => x.Id == accountId && x.HouseholdId == householdId && x.IsActive,
            cancellationToken))
            throw new AppValidationException("Conta financeira inválida.");
        if (!await dbContext.Set<HouseholdMembership>().AnyAsync(
            x => x.HouseholdId == householdId && x.UserId == responsibleUserId,
            cancellationToken))
            throw new AppValidationException("Responsável não pertence ao núcleo financeiro.");
    }

    private async Task RequireMembershipAsync(Guid householdId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Set<HouseholdMembership>().AsNoTracking().AnyAsync(
            x => x.HouseholdId == householdId && x.UserId == currentUser.UserId,
            cancellationToken))
            throw new NotFoundException("Núcleo financeiro não encontrado.");
    }

    private async Task<RecurringTransaction> FindAsync(Guid householdId, Guid id, CancellationToken cancellationToken) =>
        await dbContext.Set<RecurringTransaction>().SingleOrDefaultAsync(
            x => x.Id == id && x.HouseholdId == householdId,
            cancellationToken) ?? throw new NotFoundException("Recorrência não encontrada.");

    private static RecurringTransactionResponse Map(RecurringTransaction item) => new(
        item.Id, item.Name, item.Type, item.CategoryId, item.ExpectedAmount, item.DueDay,
        item.AccountId, item.StartDate, item.EndDate, item.Status, item.ResponsibleUserId, item.Scope);
}
