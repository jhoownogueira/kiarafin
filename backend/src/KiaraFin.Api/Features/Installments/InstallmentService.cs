using System.Data;
using KiaraFin.Api.Common.Exceptions;
using KiaraFin.Api.Common.Security;
using KiaraFin.Api.Data;
using KiaraFin.Api.Features.Finance;
using KiaraFin.Api.Features.Identity;
using Microsoft.EntityFrameworkCore;

namespace KiaraFin.Api.Features.Installments;

public sealed class InstallmentService(AppDbContext dbContext, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<InstallmentPlanResponse>> ListAsync(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        await RequireMembershipAsync(householdId, cancellationToken);
        var plans = await dbContext.Set<InstallmentPlan>().AsNoTracking()
            .Where(x => x.HouseholdId == householdId)
            .OrderByDescending(x => x.PurchaseDate)
            .ToListAsync(cancellationToken);
        var planIds = plans.Select(x => x.Id).ToList();
        var entries = await dbContext.Set<FinancialTransaction>().AsNoTracking()
            .Where(x => x.InstallmentPlanId != null && planIds.Contains(x.InstallmentPlanId.Value))
            .OrderBy(x => x.DueDate)
            .ToListAsync(cancellationToken);
        return plans.Select(x => Map(x, entries.Where(e => e.InstallmentPlanId == x.Id))).ToList();
    }

    public async Task<InstallmentPlanResponse> GetAsync(
        Guid householdId,
        Guid id,
        CancellationToken cancellationToken)
    {
        await RequireMembershipAsync(householdId, cancellationToken);
        var plan = await dbContext.Set<InstallmentPlan>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.HouseholdId == householdId, cancellationToken)
            ?? throw new NotFoundException("Plano de parcelas não encontrado.");
        var entries = await dbContext.Set<FinancialTransaction>().AsNoTracking()
            .Where(x => x.InstallmentPlanId == id)
            .OrderBy(x => x.InstallmentNumber)
            .ToListAsync(cancellationToken);
        return Map(plan, entries);
    }

    public async Task<InstallmentPlanResponse> CreateAsync(
        Guid householdId,
        CreateInstallmentPlanRequest request,
        CancellationToken cancellationToken)
    {
        await RequireMembershipAsync(householdId, cancellationToken);
        if (request.IdempotencyKey == Guid.Empty)
            throw new AppValidationException("A chave de idempotência é obrigatória.");

        var existing = await dbContext.Set<InstallmentPlan>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.IdempotencyKey && x.HouseholdId == householdId, cancellationToken);
        if (existing is not null)
            return await GetAsync(householdId, existing.Id, cancellationToken);

        await ValidateCommonReferencesAsync(householdId, request, cancellationToken);
        IReadOnlyList<InstallmentAllocation> allocations;
        try
        {
            allocations = InstallmentAllocator.Split(request.TotalAmount, request.InstallmentCount);
        }
        catch (ArgumentException exception)
        {
            throw new AppValidationException(exception.Message);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var plan = new InstallmentPlan(
            householdId,
            request.Description,
            request.TotalAmount,
            request.InstallmentCount,
            request.PurchaseDate,
            request.FirstInstallmentDate,
            request.CategoryId,
            request.SourceType,
            request.SourceId,
            request.ResponsibleUserId,
            request.Scope,
            request.IdempotencyKey);
        dbContext.Set<InstallmentPlan>().Add(plan);

        var entries = request.SourceType == InstallmentSourceType.CreditCard
            ? await CreateCardEntriesAsync(plan, allocations, cancellationToken)
            : CreateAccountEntries(plan, allocations);
        dbContext.Set<FinancialTransaction>().AddRange(entries);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(plan, entries);
    }

    private async Task<IReadOnlyList<FinancialTransaction>> CreateCardEntriesAsync(
        InstallmentPlan plan,
        IReadOnlyList<InstallmentAllocation> allocations,
        CancellationToken cancellationToken)
    {
        var card = await dbContext.Set<CreditCard>().SingleAsync(x => x.Id == plan.SourceId, cancellationToken);
        if (card.InstitutionControlled)
            throw new ConflictException("As compras deste cartão são sincronizadas pela instituição financeira.");
        var activeInvoiceIds = await dbContext.Set<Invoice>().AsNoTracking()
            .Where(x => x.CreditCardId == card.Id &&
                        x.Status != InvoiceStatus.Paid && x.Status != InvoiceStatus.Canceled)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var committed = await dbContext.Set<FinancialTransaction>()
            .Where(x => x.HouseholdId == plan.HouseholdId &&
                        x.CreditCardId == card.Id &&
                        x.InvoiceId != null && activeInvoiceIds.Contains(x.InvoiceId.Value) &&
                        x.Status != FinancialTransactionStatus.Canceled)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        if (committed + plan.TotalAmount > card.TotalLimit!.Value)
            throw new ConflictException("A compra excede o limite disponível do cartão.");

        var entries = new List<FinancialTransaction>();
        var loadedInvoices = new Dictionary<DateOnly, Invoice>();
        foreach (var allocation in allocations)
        {
            var installmentDate = MonthlyDateCalculator.AddMonthsKeepingDesiredDay(
                plan.FirstInstallmentDate,
                allocation.Number - 1,
                plan.FirstInstallmentDate.Day);
            var cycle = InvoiceCycleCalculator.ForPurchase(installmentDate, card.ClosingDay!.Value, card.DueDay!.Value);
            if (!loadedInvoices.TryGetValue(cycle.ClosingDate, out var invoice))
            {
                invoice = await dbContext.Set<Invoice>().SingleOrDefaultAsync(
                    x => x.CreditCardId == card.Id && x.ClosingDate == cycle.ClosingDate,
                    cancellationToken) ?? new Invoice(
                        plan.HouseholdId, card.Id, cycle.StartDate, cycle.ClosingDate, cycle.DueDate);
                if (dbContext.Entry(invoice).State == EntityState.Detached)
                    dbContext.Set<Invoice>().Add(invoice);
                loadedInvoices.Add(cycle.ClosingDate, invoice);
            }

            invoice.ReplaceCalculatedTotal(invoice.TotalAmount + allocation.Amount, DateTime.UtcNow);
            var entry = FinancialTransaction.CreateCreditCardPurchase(
                plan.HouseholdId,
                $"{plan.Description} {allocation.Number}/{plan.InstallmentCount}",
                allocation.Amount,
                plan.CategoryId,
                card.Id,
                invoice.Id,
                installmentDate,
                plan.ResponsibleUserId,
                plan.Scope,
                dueDate: invoice.DueDate);
            entry.LinkInstallment(plan.Id, allocation.Number, plan.InstallmentCount);
            entries.Add(entry);
        }

        return entries;
    }

    private static IReadOnlyList<FinancialTransaction> CreateAccountEntries(
        InstallmentPlan plan,
        IReadOnlyList<InstallmentAllocation> allocations)
    {
        var entries = new List<FinancialTransaction>();
        foreach (var allocation in allocations)
        {
            var dueDate = MonthlyDateCalculator.AddMonthsKeepingDesiredDay(
                plan.FirstInstallmentDate,
                allocation.Number - 1,
                plan.FirstInstallmentDate.Day);
            var entry = new FinancialTransaction(
                plan.HouseholdId,
                $"{plan.Description} {allocation.Number}/{plan.InstallmentCount}",
                allocation.Amount,
                FinancialTransactionType.Expense,
                plan.CategoryId,
                plan.SourceId,
                dueDate,
                dueDate,
                plan.ResponsibleUserId,
                plan.Scope);
            entry.LinkInstallment(plan.Id, allocation.Number, plan.InstallmentCount);
            entries.Add(entry);
        }

        return entries;
    }

    private async Task ValidateCommonReferencesAsync(
        Guid householdId,
        CreateInstallmentPlanRequest request,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Set<Category>().AnyAsync(
            x => x.Id == request.CategoryId && x.HouseholdId == householdId && x.IsActive && x.Type == CategoryType.Expense,
            cancellationToken))
            throw new AppValidationException("Categoria de despesa inválida.");
        if (!await dbContext.Set<HouseholdMembership>().AnyAsync(
            x => x.HouseholdId == householdId && x.UserId == request.ResponsibleUserId,
            cancellationToken))
            throw new AppValidationException("Responsável não pertence ao núcleo financeiro.");

        var validSource = request.SourceType switch
        {
            InstallmentSourceType.FinancialAccount => await dbContext.Set<FinancialAccount>().AnyAsync(
                x => x.Id == request.SourceId && x.HouseholdId == householdId && x.IsActive,
                cancellationToken),
            InstallmentSourceType.CreditCard => await dbContext.Set<CreditCard>().AnyAsync(
                x => x.Id == request.SourceId && x.HouseholdId == householdId && x.Status == CreditCardStatus.Active,
                cancellationToken),
            _ => false
        };
        if (!validSource)
            throw new AppValidationException("Conta ou cartão de origem inválido.");
    }

    private async Task RequireMembershipAsync(Guid householdId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Set<HouseholdMembership>().AsNoTracking().AnyAsync(
            x => x.HouseholdId == householdId && x.UserId == currentUser.UserId,
            cancellationToken))
            throw new NotFoundException("Núcleo financeiro não encontrado.");
    }

    private static InstallmentPlanResponse Map(
        InstallmentPlan plan,
        IEnumerable<FinancialTransaction> transactions)
    {
        var items = transactions.OrderBy(x => x.InstallmentNumber).Select(x => new InstallmentItemResponse(
            x.Id,
            x.InstallmentNumber ?? 1,
            x.InstallmentCount ?? plan.InstallmentCount,
            x.Amount,
            x.DueDate,
            x.InvoiceId,
            x.Status)).ToList();
        var settled = items.Where(x => x.Status is FinancialTransactionStatus.Paid or FinancialTransactionStatus.Received)
            .Sum(x => x.Amount);
        return new InstallmentPlanResponse(
            plan.Id, plan.Description, plan.TotalAmount, plan.InstallmentCount,
            plan.TotalAmount - settled, plan.PurchaseDate, plan.SourceType, plan.SourceId, items);
    }
}
