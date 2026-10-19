using System.Data;
using KiaraFin.Api.Common;
using KiaraFin.Api.Common.Exceptions;
using KiaraFin.Api.Data;
using KiaraFin.Api.Features.Finance;
using KiaraFin.Api.Features.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KiaraFin.Api.Features.Transactions;

public sealed class TransactionService(AppDbContext dbContext, HouseholdAccessService access)
{
    public async Task<PagedResponse<TransactionResponse>> ListAsync(
        Guid householdId, int page, int pageSize, DateOnly? from, DateOnly? to,
        Guid? accountId, Guid? categoryId, Guid? responsibleUserId,
        FinancialTransactionStatus? status, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        if (from > to)
            throw new AppValidationException("A data inicial deve ser anterior à data final.");
        (page, pageSize) = Pagination.Normalize(page, pageSize);
        var today = BusinessDate.Today();
        var query = dbContext.FinancialTransactions.AsNoTracking().Where(x => x.HouseholdId == householdId);
        if (from is not null) query = query.Where(x => x.CompetenceDate >= from);
        if (to is not null) query = query.Where(x => x.CompetenceDate <= to);
        if (accountId is not null) query = query.Where(x => x.AccountId == accountId || x.DestinationAccountId == accountId);
        if (categoryId is not null) query = query.Where(x => x.CategoryId == categoryId);
        if (responsibleUserId is not null) query = query.Where(x => x.ResponsibleUserId == responsibleUserId);
        if (status == FinancialTransactionStatus.Overdue)
            query = query.Where(x => x.Status == FinancialTransactionStatus.Pending && x.DueDate < today &&
                !(x.InstitutionControlled && x.CreditCardId != null && x.InvoiceId == null));
        else if (status == FinancialTransactionStatus.Pending)
            query = query.Where(x => x.Status == FinancialTransactionStatus.Pending &&
                (x.DueDate >= today || x.InstitutionControlled && x.CreditCardId != null && x.InvoiceId == null));
        else if (status is not null)
            query = query.Where(x => x.Status == status);
        var total = await query.CountAsync(cancellationToken);
        var entities = await query.OrderByDescending(x => x.CompetenceDate).ThenByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResponse<TransactionResponse>(entities.Select(x => Map(x, today)).ToList(), page, pageSize, total);
    }

    public async Task<TransactionResponse> GetAsync(
        Guid householdId, Guid transactionId, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        var transaction = await FindAsync(householdId, transactionId, false, cancellationToken);
        return Map(transaction, BusinessDate.Today());
    }

    public async Task<TransactionResponse> CreateAsync(
        Guid householdId, CreateTransactionRequest request, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        if (request.Type == FinancialTransactionType.Transfer)
            throw new AppValidationException("Use o endpoint de transferências.");
        await ValidateReferencesAsync(householdId, request.AccountId, request.CategoryId,
            request.Type, request.ResponsibleUserId, cancellationToken);
        var transaction = new FinancialTransaction(
            householdId, request.Description, request.Amount, request.Type, request.CategoryId,
            request.AccountId, request.CompetenceDate, request.DueDate, request.ResponsibleUserId,
            request.Scope, request.Notes);
        dbContext.FinancialTransactions.Add(transaction);
        if (request.SettledDate is not null)
        {
            transaction.Settle(request.SettledDate.Value, DateTime.UtcNow);
            dbContext.AccountMovements.Add(CreateSettlementMovement(transaction));
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(transaction, BusinessDate.Today());
    }

    public async Task<TransactionResponse> UpdateAsync(
        Guid householdId, Guid transactionId, UpdateTransactionRequest request,
        CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        var transaction = await FindAsync(householdId, transactionId, true, cancellationToken);
        if (transaction.InstitutionControlled)
        {
            var expectedCategoryType = transaction.Type == FinancialTransactionType.Income
                ? CategoryType.Income : CategoryType.Expense;
            if (!await dbContext.Categories.AnyAsync(x => x.Id == request.CategoryId &&
                    x.HouseholdId == householdId && x.IsActive && x.Type == expectedCategoryType,
                    cancellationToken))
                throw new AppValidationException("A categoria deve estar ativa, ter o tipo correto e pertencer ao núcleo financeiro.");
            transaction.UpdateInstitutionMetadata(request.CategoryId, request.Notes, DateTime.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Map(transaction, BusinessDate.Today());
        }
        await ValidateReferencesAsync(householdId, request.AccountId, request.CategoryId,
            transaction.Type, request.ResponsibleUserId, cancellationToken);
        transaction.UpdatePending(request.Description, request.Amount, request.CategoryId,
            request.AccountId, request.CompetenceDate, request.DueDate, request.ResponsibleUserId,
            request.Scope, request.Notes, DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(transaction, BusinessDate.Today());
    }

    public async Task<TransactionResponse> SettleAsync(
        Guid householdId, Guid transactionId, SettleTransactionRequest request,
        CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        var transaction = await FindAsync(householdId, transactionId, true, cancellationToken);
        if (transaction.InstitutionControlled)
            throw new ConflictException("A liquidação deste lançamento é controlada pela instituição.");
        transaction.Settle(request.SettledDate, DateTime.UtcNow);
        dbContext.AccountMovements.Add(CreateSettlementMovement(transaction));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(transaction, BusinessDate.Today());
    }

    public async Task<TransactionResponse> UpdateMetadataAsync(Guid householdId, Guid transactionId,
        UpdateTransactionMetadataRequest request, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        var transaction = await FindAsync(householdId, transactionId, true, cancellationToken);
        if (!transaction.InstitutionControlled)
            throw new ConflictException("Este endpoint é exclusivo para metadados de lançamentos sincronizados.");
        var expectedType = transaction.Type == FinancialTransactionType.Income ? CategoryType.Income : CategoryType.Expense;
        if (!await dbContext.Categories.AnyAsync(x => x.Id == request.CategoryId && x.HouseholdId == householdId &&
                x.IsActive && x.Type == expectedType, cancellationToken))
            throw new AppValidationException("A categoria deve estar ativa, ter o tipo correto e pertencer ao núcleo financeiro.");
        transaction.UpdateInstitutionMetadata(request.CategoryId, request.Notes, DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(transaction, BusinessDate.Today());
    }

    public async Task<TransactionResponse> CancelAsync(
        Guid householdId, Guid transactionId, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        var transaction = await FindAsync(householdId, transactionId, true, cancellationToken);
        if (transaction.InstitutionControlled)
            throw new ConflictException("O cancelamento deste lançamento é controlado pela instituição.");
        if (transaction.Status == FinancialTransactionStatus.Canceled)
            return Map(transaction, BusinessDate.Today());
        var movement = await dbContext.AccountMovements.SingleOrDefaultAsync(
            x => x.TransactionId == transaction.Id && x.Kind != AccountMovementKind.Reversal,
            cancellationToken);
        transaction.Cancel(DateTime.UtcNow);
        if (movement is not null)
        {
            dbContext.AccountMovements.Add(new AccountMovement(
                householdId, movement.AccountId, transaction.Id, -movement.SignedAmount,
                AccountMovementKind.Reversal, BusinessDate.Today(), movement.Sequence + 10));
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(transaction, BusinessDate.Today());
    }

    public async Task<TransactionResponse> TransferAsync(
        Guid householdId, CreateTransferRequest request, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        await access.RequireMemberUserAsync(householdId, request.ResponsibleUserId, cancellationToken);
        var requestId = request.ClientRequestId.Trim();
        var existing = await dbContext.FinancialTransactions.AsNoTracking().SingleOrDefaultAsync(
            x => x.HouseholdId == householdId && x.ClientRequestId == requestId,
            cancellationToken);
        if (existing is not null)
            return Map(existing, BusinessDate.Today());

        var accounts = await dbContext.FinancialAccounts.Where(x =>
                x.HouseholdId == householdId && x.IsActive &&
                (x.Id == request.SourceAccountId || x.Id == request.DestinationAccountId))
            .Select(x => x.Id).ToListAsync(cancellationToken);
        if (accounts.Count != 2 || request.SourceAccountId == request.DestinationAccountId)
            throw new AppValidationException("As contas de origem e destino devem ser distintas, ativas e pertencer ao núcleo.");

        await using var transactionScope = await BeginTransactionAsync(cancellationToken);
        var transaction = FinancialTransaction.CreateTransfer(
            householdId, request.Description, request.Amount, request.SourceAccountId,
            request.DestinationAccountId, request.TransferDate, request.ResponsibleUserId,
            requestId, request.Notes);
        dbContext.FinancialTransactions.Add(transaction);
        dbContext.AccountMovements.AddRange(
            new AccountMovement(householdId, request.SourceAccountId, transaction.Id,
                -request.Amount, AccountMovementKind.TransferOut, request.TransferDate, 1),
            new AccountMovement(householdId, request.DestinationAccountId, transaction.Id,
                request.Amount, AccountMovementKind.TransferIn, request.TransferDate, 2));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transactionScope is not null)
                await transactionScope.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (transactionScope is not null)
                await transactionScope.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            var concurrent = await dbContext.FinancialTransactions.AsNoTracking().SingleOrDefaultAsync(
                x => x.HouseholdId == householdId && x.ClientRequestId == requestId,
                cancellationToken);
            if (concurrent is not null)
                return Map(concurrent, BusinessDate.Today());
            throw;
        }
        return Map(transaction, BusinessDate.Today());
    }

    private async Task ValidateReferencesAsync(
        Guid householdId, Guid accountId, Guid categoryId, FinancialTransactionType type,
        Guid responsibleUserId, CancellationToken cancellationToken)
    {
        await access.RequireMemberUserAsync(householdId, responsibleUserId, cancellationToken);
        if (!await dbContext.FinancialAccounts.AnyAsync(
                x => x.Id == accountId && x.HouseholdId == householdId && x.IsActive,
                cancellationToken))
            throw new AppValidationException("A conta deve estar ativa e pertencer ao núcleo financeiro.");
        var expectedCategoryType = type == FinancialTransactionType.Income ? CategoryType.Income : CategoryType.Expense;
        if (!await dbContext.Categories.AnyAsync(
                x => x.Id == categoryId && x.HouseholdId == householdId && x.IsActive && x.Type == expectedCategoryType,
                cancellationToken))
            throw new AppValidationException("A categoria deve estar ativa, ter o tipo correto e pertencer ao núcleo financeiro.");
    }

    private async Task<FinancialTransaction> FindAsync(
        Guid householdId, Guid transactionId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? dbContext.FinancialTransactions : dbContext.FinancialTransactions.AsNoTracking();
        return await query.SingleOrDefaultAsync(
            x => x.Id == transactionId && x.HouseholdId == householdId,
            cancellationToken) ?? throw new NotFoundException("Lançamento não encontrado.");
    }

    private static AccountMovement CreateSettlementMovement(FinancialTransaction transaction)
    {
        var signedAmount = transaction.Type == FinancialTransactionType.Income
            ? transaction.Amount
            : -transaction.Amount;
        var kind = transaction.Type == FinancialTransactionType.Income
            ? AccountMovementKind.Income
            : AccountMovementKind.Expense;
        return new AccountMovement(transaction.HouseholdId, transaction.AccountId!.Value,
            transaction.Id, signedAmount, kind, transaction.SettledDate!.Value, 1);
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken) =>
        dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

    private static TransactionResponse Map(FinancialTransaction x, DateOnly today)
    {
        var unbilledInstitutionCardPurchase = x.InstitutionControlled && x.CreditCardId is not null && x.InvoiceId is null;
        var status = x.Status == FinancialTransactionStatus.Pending && x.DueDate < today && !unbilledInstitutionCardPurchase
            ? FinancialTransactionStatus.Overdue
            : x.Status;
        return new TransactionResponse(
            x.Id, x.Description, x.Amount, x.Type, status, x.CategoryId, x.AccountId,
            x.DestinationAccountId, x.CompetenceDate, x.DueDate, x.SettledDate, x.Notes,
            x.ResponsibleUserId, x.Scope, x.RecurringTransactionId, x.CardSubscriptionId,
            x.ClientRequestId, x.CreatedAtUtc, x.SourceProvider, x.ExternalId, x.InstitutionControlled,
            x.IsRefund);
    }
}
