using System.Data;
using KiaraFin.Api.Common;
using KiaraFin.Api.Common.Exceptions;
using KiaraFin.Api.Common.Security;
using KiaraFin.Api.Data;
using KiaraFin.Api.Features.Finance;
using KiaraFin.Api.Features.Identity;
using KiaraFin.Api.Features.Installments;
using Microsoft.EntityFrameworkCore;

namespace KiaraFin.Api.Features.CreditCards;

public sealed class CreditCardService(
    AppDbContext dbContext,
    ICurrentUser currentUser,
    InstallmentService installmentService)
{
    public async Task<IReadOnlyList<CreditCardResponse>> ListAsync(Guid householdId, CancellationToken cancellationToken)
    {
        await RequireMembershipAsync(householdId, cancellationToken);
        var cards = await dbContext.Set<CreditCard>().AsNoTracking()
            .Where(x => x.HouseholdId == householdId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        var result = new List<CreditCardResponse>();
        foreach (var card in cards)
            result.Add(await MapCardAsync(card, cancellationToken));
        return result;
    }

    public async Task<CreditCardResponse> GetAsync(Guid householdId, Guid id, CancellationToken cancellationToken)
    {
        await RequireMembershipAsync(householdId, cancellationToken);
        var card = await FindCardAsync(householdId, id, cancellationToken);
        return await MapCardAsync(card, cancellationToken);
    }

    public async Task<CreditCardResponse> CreateAsync(
        Guid householdId,
        CreateCreditCardRequest request,
        CancellationToken cancellationToken)
    {
        await RequireMembershipAsync(householdId, cancellationToken);
        await ValidateReferencesAsync(
            householdId, request.ResponsibleUserId, request.DefaultPaymentAccountId, cancellationToken);
        CreditCard card;
        try
        {
            card = new CreditCard(
                householdId, request.Name, request.TotalLimit, request.ClosingDay, request.DueDay,
                request.ResponsibleUserId, request.DefaultPaymentAccountId,
                request.Institution, request.Brand, request.Color);
        }
        catch (ArgumentException exception)
        {
            throw new AppValidationException(exception.Message);
        }

        dbContext.Set<CreditCard>().Add(card);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapCardAsync(card, cancellationToken);
    }

    public async Task<CreditCardResponse> UpdateAsync(
        Guid householdId,
        Guid id,
        UpdateCreditCardRequest request,
        CancellationToken cancellationToken)
    {
        await RequireMembershipAsync(householdId, cancellationToken);
        var card = await FindCardAsync(householdId, id, cancellationToken);
        if (card.InstitutionControlled)
            throw new ConflictException("Os campos deste cartão são controlados pela instituição financeira.");
        await ValidateReferencesAsync(
            householdId, request.ResponsibleUserId, request.DefaultPaymentAccountId, cancellationToken);
        var current = await MapCardAsync(card, cancellationToken);
        if (current.UsedLimit is decimal usedLimit && request.TotalLimit < usedLimit)
            throw new ConflictException("O limite total não pode ser menor que o valor já comprometido.");
        try
        {
            card.Update(
                request.Name, request.TotalLimit, request.ClosingDay, request.DueDay,
                request.ResponsibleUserId, request.DefaultPaymentAccountId,
                request.Institution, request.Brand, request.Color, DateTime.UtcNow);
            card.SetActive(request.IsActive, DateTime.UtcNow);
        }
        catch (ArgumentException exception)
        {
            throw new AppValidationException(exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapCardAsync(card, cancellationToken);
    }

    public async Task DeactivateAsync(Guid householdId, Guid id, CancellationToken cancellationToken)
    {
        await RequireMembershipAsync(householdId, cancellationToken);
        var card = await FindCardAsync(householdId, id, cancellationToken);
        if (card.InstitutionControlled)
            throw new ConflictException("Este cartão é controlado pela instituição financeira.");
        card.SetActive(false, DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<InstallmentPlanResponse> CreatePurchaseAsync(
        Guid householdId,
        Guid cardId,
        CreditCardPurchaseRequest request,
        CancellationToken cancellationToken)
    {
        await RequireMembershipAsync(householdId, cancellationToken);
        var card = await FindCardAsync(householdId, cardId, cancellationToken);
        if (card.InstitutionControlled)
            throw new ConflictException("As compras deste cartão são sincronizadas pela instituição financeira.");
        var firstDate = request.FirstInstallmentDate ?? request.PurchaseDate;
        return await installmentService.CreateAsync(
            householdId,
            new CreateInstallmentPlanRequest(
                request.IdempotencyKey,
                request.Description,
                request.TotalAmount,
                request.InstallmentCount,
                request.PurchaseDate,
                firstDate,
                request.CategoryId,
                InstallmentSourceType.CreditCard,
                cardId,
                request.ResponsibleUserId,
                request.Scope),
            cancellationToken);
    }

    public async Task UpdatePurchaseAsync(
        Guid householdId,
        Guid cardId,
        Guid purchaseId,
        UpdateCreditCardPurchaseRequest request,
        CancellationToken cancellationToken)
    {
        await RequireMembershipAsync(householdId, cancellationToken);
        var card = await FindCardAsync(householdId, cardId, cancellationToken);
        if (card.InstitutionControlled)
            throw new ConflictException("As compras deste cartão são sincronizadas pela instituição financeira.");
        await ValidatePurchaseReferencesAsync(
            householdId, request.CategoryId, request.ResponsibleUserId, cancellationToken);

        await using var databaseTransaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var (plan, entries, invoices) = await LoadEditablePurchaseAsync(
            householdId, cardId, purchaseId, cancellationToken);
        var allocations = InstallmentAllocator.Split(request.TotalAmount, plan.InstallmentCount)
            .ToDictionary(x => x.Number);
        var activeInvoiceIds = await dbContext.Set<Invoice>()
            .Where(x => x.CreditCardId == cardId &&
                        x.Status != InvoiceStatus.Paid && x.Status != InvoiceStatus.Canceled)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var committedByOtherPurchases = await dbContext.Set<FinancialTransaction>()
            .Where(x => x.CreditCardId == cardId && x.InvoiceId != null &&
                        activeInvoiceIds.Contains(x.InvoiceId.Value) &&
                        x.InstallmentPlanId != plan.Id &&
                        x.Status != FinancialTransactionStatus.Canceled)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        if (committedByOtherPurchases + request.TotalAmount > card.TotalLimit!.Value)
            throw new ConflictException("A alteração excede o limite disponível do cartão.");

        var oldTotals = entries.GroupBy(x => x.InvoiceId!.Value)
            .ToDictionary(x => x.Key, x => x.Sum(item => item.Amount));
        var now = DateTime.UtcNow;
        foreach (var entry in entries)
        {
            var allocation = allocations[entry.InstallmentNumber ?? 1];
            entry.UpdatePendingCreditCardPurchase(
                $"{request.Description} {allocation.Number}/{plan.InstallmentCount}",
                allocation.Amount,
                request.CategoryId,
                request.ResponsibleUserId,
                request.Scope,
                now);
        }
        plan.UpdatePendingCardPurchase(
            request.Description, request.TotalAmount, request.CategoryId,
            request.ResponsibleUserId, request.Scope, now);
        foreach (var invoice in invoices)
        {
            var updatedPlanTotal = entries.Where(x => x.InvoiceId == invoice.Id).Sum(x => x.Amount);
            invoice.ReplaceCalculatedTotal(
                invoice.TotalAmount - oldTotals.GetValueOrDefault(invoice.Id) + updatedPlanTotal, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await databaseTransaction.CommitAsync(cancellationToken);
    }

    public async Task DeletePurchaseAsync(
        Guid householdId,
        Guid cardId,
        Guid purchaseId,
        CancellationToken cancellationToken)
    {
        await RequireMembershipAsync(householdId, cancellationToken);
        var card = await FindCardAsync(householdId, cardId, cancellationToken);
        if (card.InstitutionControlled)
            throw new ConflictException("As compras deste cartão são sincronizadas pela instituição financeira.");
        await using var databaseTransaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var (plan, entries, invoices) = await LoadEditablePurchaseAsync(
            householdId, cardId, purchaseId, cancellationToken);
        var totals = entries.GroupBy(x => x.InvoiceId!.Value)
            .ToDictionary(x => x.Key, x => x.Sum(item => item.Amount));
        var now = DateTime.UtcNow;
        foreach (var invoice in invoices)
            invoice.ReplaceCalculatedTotal(invoice.TotalAmount - totals.GetValueOrDefault(invoice.Id), now);

        dbContext.Set<FinancialTransaction>().RemoveRange(entries);
        dbContext.Set<InstallmentPlan>().Remove(plan);
        await dbContext.SaveChangesAsync(cancellationToken);
        await databaseTransaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InvoiceSummaryResponse>> ListInvoicesAsync(
        Guid householdId,
        Guid cardId,
        CancellationToken cancellationToken)
    {
        await RequireMembershipAsync(householdId, cancellationToken);
        await FindCardAsync(householdId, cardId, cancellationToken);
        return await dbContext.Set<Invoice>().AsNoTracking()
            .Where(x => x.HouseholdId == householdId && x.CreditCardId == cardId)
            .OrderByDescending(x => x.ClosingDate)
            .Select(x => new InvoiceSummaryResponse(
                x.Id, x.ClosingDate, x.DueDate, x.TotalAmount, x.Status, x.PaidDate))
            .ToListAsync(cancellationToken);
    }

    public async Task<InvoiceDetailResponse> GetInvoiceAsync(
        Guid householdId,
        Guid cardId,
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        await RequireMembershipAsync(householdId, cancellationToken);
        var invoice = await dbContext.Set<Invoice>().AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == invoiceId && x.CreditCardId == cardId && x.HouseholdId == householdId,
            cancellationToken) ?? throw new NotFoundException("Fatura não encontrada.");
        var purchaseEntities = await dbContext.Set<FinancialTransaction>().AsNoTracking()
            .Where(x => x.InvoiceId == invoice.Id && x.Type == FinancialTransactionType.Expense &&
                        x.Status != FinancialTransactionStatus.Canceled)
            .OrderBy(x => x.CompetenceDate)
            .ToListAsync(cancellationToken);
        var planIds = purchaseEntities.Where(x => x.InstallmentPlanId != null)
            .Select(x => x.InstallmentPlanId!.Value).Distinct().ToList();
        var plans = await dbContext.Set<InstallmentPlan>().AsNoTracking()
            .Where(x => planIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var purchases = purchaseEntities.Select(x =>
        {
            var plan = x.InstallmentPlanId is Guid planId ? plans.GetValueOrDefault(planId) : null;
            return new InvoicePurchaseResponse(
                x.Id, x.InstallmentPlanId, x.CardSubscriptionId,
                plan?.Description ?? x.Description, x.Amount,
                plan?.TotalAmount ?? x.Amount, x.CompetenceDate, x.CategoryId!.Value,
                x.ResponsibleUserId, x.Scope, x.InstallmentNumber, x.InstallmentCount, x.IsRefund);
        }).ToList();
        return new InvoiceDetailResponse(
            invoice.Id, invoice.CreditCardId, invoice.CycleStartDate, invoice.ClosingDate,
            invoice.DueDate, invoice.TotalAmount, invoice.Status, invoice.PaidDate,
            invoice.PaymentTransactionId, purchases);
    }

    public async Task<InvoiceDetailResponse> CloseInvoiceAsync(
        Guid householdId,
        Guid cardId,
        Guid invoiceId,
        CloseInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        await RequireMembershipAsync(householdId, cancellationToken);
        await using var databaseTransaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var invoice = await dbContext.Set<Invoice>().SingleOrDefaultAsync(
            x => x.Id == invoiceId && x.CreditCardId == cardId && x.HouseholdId == householdId,
            cancellationToken) ?? throw new NotFoundException("Fatura não encontrada.");
        if (invoice.InstitutionControlled)
            throw new ConflictException("Esta fatura é controlada pela instituição financeira.");
        if (invoice.Status != InvoiceStatus.Closed)
        {
            var now = DateTime.UtcNow;
            invoice.Close(request.DueDate, now);
            var purchases = await dbContext.Set<FinancialTransaction>()
                .Where(x => x.InvoiceId == invoice.Id && x.Type == FinancialTransactionType.Expense &&
                            x.Status == FinancialTransactionStatus.Pending)
                .ToListAsync(cancellationToken);
            foreach (var purchase in purchases)
                purchase.UpdateInvoiceDueDate(request.DueDate, now);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        await databaseTransaction.CommitAsync(cancellationToken);
        return await GetInvoiceAsync(householdId, cardId, invoiceId, cancellationToken);
    }

    public async Task<InvoicePaymentResponse> PayInvoiceAsync(
        Guid householdId,
        Guid cardId,
        Guid invoiceId,
        PayInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        await RequireMembershipAsync(householdId, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var invoice = await dbContext.Set<Invoice>().SingleOrDefaultAsync(
            x => x.Id == invoiceId && x.CreditCardId == cardId && x.HouseholdId == householdId,
            cancellationToken) ?? throw new NotFoundException("Fatura não encontrada.");
        if (invoice.InstitutionControlled)
            throw new ConflictException("Esta fatura é controlada pela instituição financeira.");
        if (invoice.PaymentTransactionId is Guid previousPaymentId)
            return new InvoicePaymentResponse(invoice.Id, previousPaymentId, invoice.Status);
        if (!await dbContext.Set<FinancialAccount>().AnyAsync(
            x => x.Id == request.AccountId && x.HouseholdId == householdId && x.IsActive,
            cancellationToken))
            throw new AppValidationException("Conta de pagamento inválida.");

        var payment = FinancialTransaction.CreateInvoicePayment(
            householdId,
            request.AccountId,
            invoice.Id,
            invoice.TotalAmount,
            request.PaidDate,
            currentUser.UserId);
        var movement = new AccountMovement(
            householdId,
            request.AccountId,
            payment.Id,
            -invoice.TotalAmount,
            AccountMovementKind.InvoicePayment,
            request.PaidDate,
            1);
        var purchases = await dbContext.Set<FinancialTransaction>()
            .Where(x => x.InvoiceId == invoice.Id &&
                        x.Type == FinancialTransactionType.Expense &&
                        x.Status != FinancialTransactionStatus.Canceled)
            .ToListAsync(cancellationToken);
        foreach (var purchase in purchases.Where(x =>
                     x.Status is FinancialTransactionStatus.Pending or FinancialTransactionStatus.Overdue))
            purchase.Settle(request.PaidDate, DateTime.UtcNow);
        invoice.PayInFull(payment.Id, request.PaidDate, invoice.TotalAmount, DateTime.UtcNow);
        dbContext.Set<FinancialTransaction>().Add(payment);
        dbContext.Set<AccountMovement>().Add(movement);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new InvoicePaymentResponse(invoice.Id, payment.Id, invoice.Status);
    }

    private async Task<CreditCardResponse> MapCardAsync(CreditCard card, CancellationToken cancellationToken)
    {
        var invoices = await dbContext.Set<Invoice>().AsNoTracking()
            .Where(x => x.CreditCardId == card.Id)
            .OrderByDescending(x => x.ClosingDate)
            .Select(x => new InvoiceSummaryResponse(
                x.Id, x.ClosingDate, x.DueDate, x.TotalAmount, x.Status, x.PaidDate))
            .ToListAsync(cancellationToken);
        var formingInvoices = new List<FormingInvoiceResponse>();
        if (card.InstitutionControlled && card.DueDay is int dueDay)
        {
            var pendingPurchases = await dbContext.Set<FinancialTransaction>().AsNoTracking()
                .Where(x => x.CreditCardId == card.Id && x.InvoiceId == null &&
                            x.InstitutionControlled && x.Status == FinancialTransactionStatus.Pending)
                .OrderBy(x => x.CompetenceDate)
                .ThenBy(x => x.CreatedAtUtc)
                .ToListAsync(cancellationToken);
            formingInvoices = pendingPurchases
                .GroupBy(x => EstimateDueDate(x.CompetenceDate, dueDay, BusinessDate.Today()))
                .OrderBy(x => x.Key)
                .Select(group => new FormingInvoiceResponse(
                    group.Key,
                    group.Sum(x => x.IsRefund ? -x.Amount : x.Amount),
                    group.Select(MapPurchase).ToList()))
                .ToList();
        }
        var activeInvoiceIds = invoices
            .Where(x => x.Status is not InvoiceStatus.Paid and not InvoiceStatus.Canceled)
            .Select(x => x.Id)
            .ToList();
        decimal? usedLimit = card.InstitutionControlled
            ? card.CurrentBalance
            : await dbContext.Set<FinancialTransaction>().AsNoTracking()
                .Where(x => x.CreditCardId == card.Id &&
                            x.InvoiceId != null && activeInvoiceIds.Contains(x.InvoiceId.Value) &&
                            x.Status != FinancialTransactionStatus.Canceled)
                .SumAsync(x => (decimal?)(x.IsRefund ? -x.Amount : x.Amount), cancellationToken) ?? 0m;
        var availableLimit = card.AvailableLimit ??
            (card.TotalLimit is decimal total && usedLimit is decimal used ? total - used : null);
        return new CreditCardResponse(
            card.Id, card.Name, card.Institution, card.Brand, card.Color,
            card.TotalLimit, usedLimit, availableLimit,
            card.ClosingDay, card.DueDay, card.ResponsibleUserId,
            card.DefaultPaymentAccountId, card.Status, invoices,
            formingInvoices,
            card.SourceProvider, card.ExternalId, card.InstitutionControlled);
    }

    private static InvoicePurchaseResponse MapPurchase(FinancialTransaction purchase) => new(
        purchase.Id, purchase.InstallmentPlanId, purchase.CardSubscriptionId,
        purchase.Description, purchase.Amount, purchase.Amount, purchase.CompetenceDate,
        purchase.CategoryId!.Value, purchase.ResponsibleUserId, purchase.Scope,
        purchase.InstallmentNumber, purchase.InstallmentCount, purchase.IsRefund);

    private static DateOnly EstimateDueDate(DateOnly competenceDate, int dueDay, DateOnly today)
    {
        var reference = competenceDate > today ? competenceDate : today;
        var dueDate = new DateOnly(reference.Year, reference.Month,
            Math.Min(dueDay, DateTime.DaysInMonth(reference.Year, reference.Month)));
        if (dueDate >= reference) return dueDate;
        var nextMonth = new DateOnly(reference.Year, reference.Month, 1).AddMonths(1);
        return new DateOnly(nextMonth.Year, nextMonth.Month,
            Math.Min(dueDay, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month)));
    }

    private async Task ValidateReferencesAsync(
        Guid householdId,
        Guid responsibleUserId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Set<HouseholdMembership>().AnyAsync(
            x => x.HouseholdId == householdId && x.UserId == responsibleUserId,
            cancellationToken))
            throw new AppValidationException("Responsável não pertence ao núcleo financeiro.");
        if (!await dbContext.Set<FinancialAccount>().AnyAsync(
            x => x.Id == accountId && x.HouseholdId == householdId && x.IsActive,
            cancellationToken))
            throw new AppValidationException("Conta padrão de pagamento inválida.");
    }

    private async Task ValidatePurchaseReferencesAsync(
        Guid householdId,
        Guid categoryId,
        Guid responsibleUserId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Set<Category>().AnyAsync(
            x => x.Id == categoryId && x.HouseholdId == householdId && x.IsActive &&
                 x.Type == CategoryType.Expense, cancellationToken))
            throw new AppValidationException("Categoria de despesa inválida.");
        if (!await dbContext.Set<HouseholdMembership>().AnyAsync(
            x => x.HouseholdId == householdId && x.UserId == responsibleUserId,
            cancellationToken))
            throw new AppValidationException("Responsável não pertence ao núcleo financeiro.");
    }

    private async Task<(InstallmentPlan Plan, List<FinancialTransaction> Entries, List<Invoice> Invoices)>
        LoadEditablePurchaseAsync(
            Guid householdId,
            Guid cardId,
            Guid purchaseId,
            CancellationToken cancellationToken)
    {
        var selectedEntry = await dbContext.Set<FinancialTransaction>().SingleOrDefaultAsync(
            x => x.Id == purchaseId && x.HouseholdId == householdId && x.CreditCardId == cardId &&
                 x.InstallmentPlanId != null,
            cancellationToken) ?? throw new NotFoundException("Compra não encontrada.");
        var plan = await dbContext.Set<InstallmentPlan>().SingleAsync(
            x => x.Id == selectedEntry.InstallmentPlanId!.Value && x.HouseholdId == householdId,
            cancellationToken);
        var entries = await dbContext.Set<FinancialTransaction>()
            .Where(x => x.InstallmentPlanId == plan.Id && x.CreditCardId == cardId)
            .ToListAsync(cancellationToken);
        var invoiceIds = entries.Where(x => x.InvoiceId != null).Select(x => x.InvoiceId!.Value).Distinct().ToList();
        var invoices = await dbContext.Set<Invoice>()
            .Where(x => invoiceIds.Contains(x.Id) && x.HouseholdId == householdId)
            .ToListAsync(cancellationToken);
        if (entries.Any(x => x.Status != FinancialTransactionStatus.Pending) ||
            invoices.Any(x => x.Status is InvoiceStatus.Paid or InvoiceStatus.Canceled))
            throw new ConflictException("A compra não pode ser alterada porque uma de suas faturas já foi paga ou cancelada.");
        return (plan, entries, invoices);
    }

    private async Task RequireMembershipAsync(Guid householdId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Set<HouseholdMembership>().AsNoTracking().AnyAsync(
            x => x.HouseholdId == householdId && x.UserId == currentUser.UserId,
            cancellationToken))
            throw new NotFoundException("Núcleo financeiro não encontrado.");
    }

    private async Task<CreditCard> FindCardAsync(Guid householdId, Guid id, CancellationToken cancellationToken) =>
        await dbContext.Set<CreditCard>().SingleOrDefaultAsync(
            x => x.Id == id && x.HouseholdId == householdId,
            cancellationToken) ?? throw new NotFoundException("Cartão não encontrado.");
}
