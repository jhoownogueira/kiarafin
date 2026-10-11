using System.Data;
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
        await ValidateReferencesAsync(
            householdId, request.ResponsibleUserId, request.DefaultPaymentAccountId, cancellationToken);
        var current = await MapCardAsync(card, cancellationToken);
        if (request.TotalLimit < current.UsedLimit)
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
        await FindCardAsync(householdId, cardId, cancellationToken);
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
        var purchases = await dbContext.Set<FinancialTransaction>().AsNoTracking()
            .Where(x => x.InvoiceId == invoice.Id && x.Type == FinancialTransactionType.Expense)
            .OrderBy(x => x.CompetenceDate)
            .Select(x => new InvoicePurchaseResponse(
                x.Id, x.Description, x.Amount, x.CompetenceDate, x.InstallmentNumber, x.InstallmentCount))
            .ToListAsync(cancellationToken);
        return new InvoiceDetailResponse(
            invoice.Id, invoice.CreditCardId, invoice.CycleStartDate, invoice.ClosingDate,
            invoice.DueDate, invoice.TotalAmount, invoice.Status, invoice.PaymentTransactionId, purchases);
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
        var activeInvoiceIds = invoices
            .Where(x => x.Status is not InvoiceStatus.Paid and not InvoiceStatus.Canceled)
            .Select(x => x.Id)
            .ToList();
        var usedLimit = await dbContext.Set<FinancialTransaction>().AsNoTracking()
            .Where(x => x.CreditCardId == card.Id &&
                        x.InvoiceId != null && activeInvoiceIds.Contains(x.InvoiceId.Value) &&
                        x.Status != FinancialTransactionStatus.Canceled)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        return new CreditCardResponse(
            card.Id, card.Name, card.Institution, card.Brand, card.Color,
            card.TotalLimit, usedLimit, card.TotalLimit - usedLimit,
            card.ClosingDay, card.DueDay, card.ResponsibleUserId,
            card.DefaultPaymentAccountId, card.Status, invoices);
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
