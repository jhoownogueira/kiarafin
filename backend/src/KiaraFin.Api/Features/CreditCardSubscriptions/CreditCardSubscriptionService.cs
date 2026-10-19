using System.Data;
using KiaraFin.Api.Common.Exceptions;
using KiaraFin.Api.Common.Security;
using KiaraFin.Api.Data;
using KiaraFin.Api.Features.Finance;
using KiaraFin.Api.Features.Identity;
using Microsoft.EntityFrameworkCore;

namespace KiaraFin.Api.Features.CreditCardSubscriptions;

public sealed class CreditCardSubscriptionService(AppDbContext dbContext, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<CreditCardSubscriptionResponse>> ListAsync(
        Guid householdId, Guid cardId, CancellationToken cancellationToken)
    {
        await RequireCardAsync(householdId, cardId, cancellationToken);
        var items = await dbContext.CreditCardSubscriptions.AsNoTracking()
            .Where(x => x.HouseholdId == householdId && x.CreditCardId == cardId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<CreditCardSubscriptionResponse> CreateAsync(
        Guid householdId, Guid cardId, CreateCreditCardSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var card = await RequireCardAsync(householdId, cardId, cancellationToken);
        if (card.InstitutionControlled)
            throw new ConflictException("As assinaturas deste cartão são sincronizadas pela instituição financeira.");
        if (card.Status != CreditCardStatus.Active)
            throw new ConflictException("Não é possível criar uma assinatura em um cartão inativo.");
        await ValidateReferencesAsync(householdId, request.CategoryId, request.ResponsibleUserId, cancellationToken);
        CreditCardSubscription subscription;
        try
        {
            subscription = new CreditCardSubscription(
                householdId, cardId, request.Name, request.Amount, request.ChargeDay,
                request.StartDate, request.CategoryId, request.ResponsibleUserId,
                request.Scope, request.EndDate);
        }
        catch (ArgumentException exception)
        {
            throw new AppValidationException(exception.Message);
        }
        dbContext.CreditCardSubscriptions.Add(subscription);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(subscription);
    }

    public async Task<CreditCardSubscriptionResponse> UpdateAsync(
        Guid householdId, Guid cardId, Guid id, UpdateCreditCardSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        await RequireCardAsync(householdId, cardId, cancellationToken);
        await ValidateReferencesAsync(householdId, request.CategoryId, request.ResponsibleUserId, cancellationToken);
        var subscription = await FindAsync(householdId, cardId, id, cancellationToken);
        try
        {
            subscription.Update(
                request.Name, request.Amount, request.ChargeDay, request.CategoryId,
                request.ResponsibleUserId, request.Scope, request.EndDate, DateTime.UtcNow);
        }
        catch (ArgumentException exception)
        {
            throw new AppValidationException(exception.Message);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(subscription);
    }

    public Task<CreditCardSubscriptionResponse> PauseAsync(
        Guid householdId, Guid cardId, Guid id, CancellationToken cancellationToken) =>
        ChangeStatusAsync(householdId, cardId, id, x => x.Pause(DateTime.UtcNow), cancellationToken);

    public Task<CreditCardSubscriptionResponse> ResumeAsync(
        Guid householdId, Guid cardId, Guid id, CancellationToken cancellationToken) =>
        ChangeStatusAsync(householdId, cardId, id, x => x.Resume(DateTime.UtcNow), cancellationToken);

    public async Task<CreditCardSubscriptionResponse> EndAsync(
        Guid householdId, Guid cardId, Guid id, DateOnly endDate, CancellationToken cancellationToken)
    {
        await RequireCardAsync(householdId, cardId, cancellationToken);
        var subscription = await FindAsync(householdId, cardId, id, cancellationToken);
        try
        {
            subscription.End(endDate, DateTime.UtcNow);
        }
        catch (ArgumentException exception)
        {
            throw new AppValidationException(exception.Message);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(subscription);
    }

    public async Task<CreditCardSubscriptionGenerationResponse> GenerateMonthAsync(
        Guid householdId, Guid cardId, int year, int month, CancellationToken cancellationToken)
    {
        var card = await RequireCardAsync(householdId, cardId, cancellationToken);
        if (card.InstitutionControlled)
            throw new ConflictException("As assinaturas deste cartão são sincronizadas pela instituição financeira.");
        if (card.Status != CreditCardStatus.Active)
            throw new ConflictException("Não é possível gerar assinaturas para um cartão inativo.");
        if (year is < 2000 or > 2200 || month is < 1 or > 12)
            throw new AppValidationException("Período inválido.");

        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        var subscriptions = await dbContext.CreditCardSubscriptions
            .Where(x => x.HouseholdId == householdId && x.CreditCardId == cardId &&
                        x.Status == RecurringTransactionStatus.Active &&
                        x.StartDate <= monthEnd && (x.EndDate == null || x.EndDate >= monthStart))
            .ToListAsync(cancellationToken);
        var existingIds = await dbContext.FinancialTransactions.AsNoTracking()
            .Where(x => x.HouseholdId == householdId && x.CreditCardId == cardId &&
                        x.CardSubscriptionId != null && x.CompetenceDate >= monthStart &&
                        x.CompetenceDate <= monthEnd)
            .Select(x => x.CardSubscriptionId!.Value)
            .ToHashSetAsync(cancellationToken);
        var toGenerate = subscriptions.Where(x => !existingIds.Contains(x.Id))
            .Select(x => new { Subscription = x, ChargeDate = MonthlyDateCalculator.Resolve(year, month, x.ChargeDay) })
            .Where(x => x.ChargeDate >= x.Subscription.StartDate &&
                        (x.Subscription.EndDate == null || x.ChargeDate <= x.Subscription.EndDate))
            .ToList();

        var activeInvoiceIds = await dbContext.Invoices.AsNoTracking()
            .Where(x => x.CreditCardId == cardId && x.Status != InvoiceStatus.Paid && x.Status != InvoiceStatus.Canceled)
            .Select(x => x.Id).ToListAsync(cancellationToken);
        var committed = await dbContext.FinancialTransactions.AsNoTracking()
            .Where(x => x.CreditCardId == cardId && x.InvoiceId != null &&
                        activeInvoiceIds.Contains(x.InvoiceId.Value) &&
                        x.Status != FinancialTransactionStatus.Canceled)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        if (committed + toGenerate.Sum(x => x.Subscription.Amount) > card.TotalLimit!.Value)
            throw new ConflictException("As assinaturas do mês excedem o limite disponível do cartão.");

        var loadedInvoices = new Dictionary<DateOnly, Invoice>();
        var created = 0;
        foreach (var occurrence in toGenerate)
        {
            var cycle = InvoiceCycleCalculator.ForPurchase(
                occurrence.ChargeDate, card.ClosingDay!.Value, card.DueDay!.Value);
            if (!loadedInvoices.TryGetValue(cycle.ClosingDate, out var invoice))
            {
                invoice = await dbContext.Invoices.SingleOrDefaultAsync(
                    x => x.CreditCardId == cardId && x.ClosingDate == cycle.ClosingDate,
                    cancellationToken) ?? new Invoice(
                        householdId, cardId, cycle.StartDate, cycle.ClosingDate, cycle.DueDate);
                if (dbContext.Entry(invoice).State == EntityState.Detached)
                    dbContext.Invoices.Add(invoice);
                loadedInvoices.Add(cycle.ClosingDate, invoice);
            }

            invoice.ReplaceCalculatedTotal(invoice.TotalAmount + occurrence.Subscription.Amount, DateTime.UtcNow);
            var entry = FinancialTransaction.CreateCreditCardPurchase(
                householdId, occurrence.Subscription.Name, occurrence.Subscription.Amount,
                occurrence.Subscription.CategoryId, cardId, invoice.Id, occurrence.ChargeDate,
                occurrence.Subscription.ResponsibleUserId, occurrence.Subscription.Scope,
                dueDate: invoice.DueDate);
            entry.LinkCardSubscription(occurrence.Subscription.Id);
            dbContext.FinancialTransactions.Add(entry);
            created++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        return new CreditCardSubscriptionGenerationResponse(created);
    }

    private async Task<CreditCardSubscriptionResponse> ChangeStatusAsync(
        Guid householdId, Guid cardId, Guid id, Action<CreditCardSubscription> change,
        CancellationToken cancellationToken)
    {
        await RequireCardAsync(householdId, cardId, cancellationToken);
        var subscription = await FindAsync(householdId, cardId, id, cancellationToken);
        change(subscription);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(subscription);
    }

    private async Task<CreditCard> RequireCardAsync(
        Guid householdId, Guid cardId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Set<HouseholdMembership>().AsNoTracking().AnyAsync(
            x => x.HouseholdId == householdId && x.UserId == currentUser.UserId, cancellationToken))
            throw new NotFoundException("Núcleo financeiro não encontrado.");
        return await dbContext.CreditCards.SingleOrDefaultAsync(
            x => x.Id == cardId && x.HouseholdId == householdId,
            cancellationToken) ?? throw new NotFoundException("Cartão não encontrado.");
    }

    private async Task ValidateReferencesAsync(
        Guid householdId, Guid categoryId, Guid responsibleUserId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Categories.AnyAsync(
            x => x.Id == categoryId && x.HouseholdId == householdId && x.IsActive &&
                 x.Type == CategoryType.Expense, cancellationToken))
            throw new AppValidationException("Categoria de despesa inválida.");
        if (!await dbContext.Set<HouseholdMembership>().AnyAsync(
            x => x.HouseholdId == householdId && x.UserId == responsibleUserId, cancellationToken))
            throw new AppValidationException("Responsável não pertence ao núcleo financeiro.");
    }

    private async Task<CreditCardSubscription> FindAsync(
        Guid householdId, Guid cardId, Guid id, CancellationToken cancellationToken) =>
        await dbContext.CreditCardSubscriptions.SingleOrDefaultAsync(
            x => x.Id == id && x.HouseholdId == householdId && x.CreditCardId == cardId,
            cancellationToken) ?? throw new NotFoundException("Assinatura não encontrada.");

    private static CreditCardSubscriptionResponse Map(CreditCardSubscription item) => new(
        item.Id, item.CreditCardId, item.Name, item.Amount, item.ChargeDay,
        item.StartDate, item.EndDate, item.CategoryId, item.ResponsibleUserId,
        item.Scope, item.Status);
}
