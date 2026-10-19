using System.Data;
using System.Text.Json;
using KiaraFin.Api.Common.Exceptions;
using KiaraFin.Api.Common.Security;
using KiaraFin.Api.Data;
using KiaraFin.Api.Features.Finance;
using KiaraFin.Api.Features.Identity;
using KiaraFin.Api.Features.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace KiaraFin.Api.Features.Pluggy;

public sealed record PluggyStatusResponse(FinancialIntegrationStatus Status, string? ItemId,
    DateTime? ActivatedAtUtc, DateTime? LastSuccessfulSyncAtUtc, DateTime? LastAttemptAtUtc,
    int AccountCount, int TransactionCount, DateTime? ConsentExpiresAtUtc, string? LastError,
    bool InitialImportCompleted, IReadOnlyList<string> Institutions, DateTime? NextScheduledSyncAtUtc);
public sealed record PluggyPreviewResponse(int ExistingAccounts, int ExistingCreditCards,
    int ExistingTransactions, int PluggyAccounts, int PluggyTransactions, DateTime? ConsentExpiresAtUtc);
public sealed record ActivatePluggyRequest(bool Confirmed);
public sealed record PluggySyncResult(int Inserted, int Updated, int Deleted, int AccountsChanged = 0);

public sealed class PluggyIntegrationService(AppDbContext db, ICurrentUser currentUser,
    IPluggyClient client, IOptions<PluggyOptions> options, ILogger<PluggyIntegrationService> logger,
    IAccountNotificationService? notifications = null)
{
    private const string Provider = "PLUGGY";
    private readonly PluggyOptions _options = options.Value;

    public async Task<PluggyStatusResponse> GetStatusAsync(CancellationToken ct)
    {
        var user = await RequireAuthorizedUserAsync(ct);
        var integration = await db.FinancialIntegrations.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == user.Id && x.Provider == Provider, ct);
        if (integration is null) return new(FinancialIntegrationStatus.Disabled, null, null, null, null, 0, 0, null, null, false, [], null);
        var institutions = await db.ExternalFinancialAccounts.AsNoTracking().Where(x => x.IntegrationId == integration.Id && x.InstitutionName != null)
            .Select(x => x.InstitutionName!).Distinct().OrderBy(x => x).ToListAsync(ct);
        DateTime? next = integration.Status == FinancialIntegrationStatus.Connected && integration.LastSuccessfulSyncAtUtc is DateTime last
            ? last.AddHours(_options.SyncIntervalHours) : null;
        return new(integration.Status, integration.ItemId, integration.ActivatedAtUtc, integration.LastSuccessfulSyncAtUtc,
            integration.LastAttemptAtUtc, integration.AccountCount, integration.TransactionCount, integration.ConsentExpiresAtUtc,
            integration.LastError, integration.InitialImportCompleted, institutions, next);
    }

    public async Task<PluggyPreviewResponse> PreviewAsync(CancellationToken ct)
    {
        var user = await RequireAuthorizedUserAsync(ct); var householdId = await RequireOwnedHouseholdAsync(user.Id, ct);
        var snapshot = await client.ReadCompleteSnapshotAsync(_options.ItemId, ct);
        return new(
            await db.FinancialAccounts.CountAsync(x => x.HouseholdId == householdId && x.ResponsibleUserId == user.Id, ct),
            await db.CreditCards.CountAsync(x => x.HouseholdId == householdId && x.ResponsibleUserId == user.Id, ct),
            await db.FinancialTransactions.CountAsync(x => x.HouseholdId == householdId && x.ResponsibleUserId == user.Id, ct),
            snapshot.Accounts.Count, snapshot.Transactions.Count, snapshot.Item.ConsentExpiresAtUtc);
    }

    public async Task<PluggySyncResult> ActivateAsync(bool confirmed, CancellationToken ct)
    {
        if (!confirmed) throw new AppValidationException("A confirmação explícita é obrigatória.");
        var user = await RequireAuthorizedUserAsync(ct); var householdId = await RequireOwnedHouseholdAsync(user.Id, ct);
        // Every remote validation and every page is read before opening the destructive transaction.
        var snapshot = await client.ReadCompleteSnapshotAsync(_options.ItemId, ct);
        await using var tx = await BeginSerializableAsync(ct);
        var integration = await db.FinancialIntegrations.SingleOrDefaultAsync(x => x.UserId == user.Id && x.Provider == Provider, ct);
        if (integration?.InitialImportCompleted == true) throw new ConflictException("A importação inicial já foi concluída.");
        integration ??= new FinancialIntegration(user.Id, householdId, Provider, snapshot.Item.Id);
        if (db.Entry(integration).State == EntityState.Detached) db.FinancialIntegrations.Add(integration);
        integration.BeginSync(true, DateTime.UtcNow); await db.SaveChangesAsync(ct);
        await DeleteOwnerFinancialDataAsync(user.Id, householdId, ct);
        var result = await PersistSnapshotAsync(integration, user.Id, householdId, snapshot, new HashSet<string>(StringComparer.Ordinal), ct);
        integration.Complete(snapshot.Accounts.Count, snapshot.Transactions.Count, snapshot.Item.ConsentExpiresAtUtc, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        await NotifyChangesAsync(user.Email, result, snapshot, true, ct);
        return result;
    }

    public async Task<PluggySyncResult> SynchronizeAsync(Guid? integrationId, string trigger, CancellationToken ct, string? payload = null)
    {
        var integration = integrationId is Guid id
            ? await db.FinancialIntegrations.SingleOrDefaultAsync(x => x.Id == id, ct)
            : await FindAuthorizedIntegrationAsync(ct);
        if (integration is null || !integration.InitialImportCompleted) throw new ConflictException("A integração ainda não foi ativada.");
        var targetItemId = !string.IsNullOrWhiteSpace(_options.ItemId) &&
                           !string.Equals(_options.ItemId, integration.ItemId, StringComparison.Ordinal)
            ? _options.ItemId : integration.ItemId;
        PluggySnapshot snapshot;
        try { snapshot = await client.ReadCompleteSnapshotAsync(targetItemId, ct); }
        catch (Exception ex)
        {
            await RecordFailureAsync(integration.Id, trigger, ex, ct);
            throw;
        }
        await using var tx = await BeginSerializableAsync(ct);
        try
        {
            if (db.Database.IsRelational())
            {
                var acquired = await db.Database.SqlQueryRaw<bool>("SELECT pg_try_advisory_xact_lock(hashtextextended({0}, 0)) AS \"Value\"", integration.Id.ToString()).SingleAsync(ct);
                if (!acquired) throw new ConflictException("Já existe uma sincronização em execução.");
            }
            if (!string.Equals(integration.ItemId, targetItemId, StringComparison.Ordinal))
            {
                var knownAccountIds = await db.ExternalFinancialAccounts
                    .Where(x => x.IntegrationId == integration.Id)
                    .Select(x => x.ExternalAccountId)
                    .ToListAsync(ct);
                if (!knownAccountIds.ToHashSet(StringComparer.Ordinal)
                        .SetEquals(snapshot.Accounts.Select(x => x.Id)))
                    throw new PluggyIncompleteResponseException(
                        "O Item renovado retornou identificadores de contas diferentes; a vinculação automática foi interrompida para evitar duplicações.");
            }
            if (!string.Equals(integration.ItemId, targetItemId, StringComparison.Ordinal))
                integration.ReplaceRenewedItem(targetItemId, DateTime.UtcNow);
            integration.BeginSync(false, DateTime.UtcNow); var sync = new FinancialIntegrationSync(integration.Id, trigger); db.FinancialIntegrationSyncs.Add(sync);
            var deletedIds = trigger.Equals("transactions/deleted", StringComparison.OrdinalIgnoreCase)
                ? ExtractDeletedTransactionIds(payload) : [];
            if (trigger.Equals("transactions/deleted", StringComparison.OrdinalIgnoreCase) && deletedIds.Count == 0)
                throw new PluggyIncompleteResponseException("Webhook de exclusão sem identificador de transação reconhecido.");
            var result = await PersistSnapshotAsync(integration, integration.UserId, integration.HouseholdId, snapshot, deletedIds, ct);
            sync.Complete(result.Inserted, result.Updated, result.Deleted, DateTime.UtcNow);
            integration.Complete(snapshot.Accounts.Count, snapshot.Transactions.Count, snapshot.Item.ConsentExpiresAtUtc, DateTime.UtcNow);
            await db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
            await NotifyChangesAsync(_options.AuthorizedEmail, result, snapshot, false, ct);
            return result;
        }
        catch (ConflictException)
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            throw;
        }
        catch (Exception ex)
        {
            if (tx is not null) await tx.RollbackAsync(ct); db.ChangeTracker.Clear();
            await RecordFailureAsync(integration.Id, trigger, ex, ct);
            logger.LogWarning(ex, "Pluggy sync failed for integration {IntegrationId}", integration.Id); throw;
        }
    }

    private async Task<PluggySyncResult> PersistSnapshotAsync(FinancialIntegration integration, Guid userId, Guid householdId,
        PluggySnapshot snapshot, IReadOnlySet<string> explicitlyDeletedIds, CancellationToken ct)
    {
        ValidateSnapshot(snapshot);
        var now = DateTime.UtcNow; var inserted = 0; var updated = 0; var deleted = 0; var accountsChanged = 0;
        var externalAccounts = await db.ExternalFinancialAccounts.Where(x => x.IntegrationId == integration.Id)
            .ToDictionaryAsync(x => x.ExternalAccountId, ct);
        var accountRows = new Dictionary<string, (ExternalFinancialAccount Mapping, FinancialAccount? Account, CreditCard? Card)>(StringComparer.Ordinal);
        foreach (var source in snapshot.Accounts)
        {
            var institution = source.InstitutionName ?? snapshot.Item.ConnectorName;
            externalAccounts.TryGetValue(source.Id, out var mapping);
            if (mapping is null || !string.Equals(mapping.ContentHash, source.ContentHash, StringComparison.Ordinal))
                accountsChanged++;
            FinancialAccount? account = null; CreditCard? card = null;
            if (source.Type.Equals("CREDIT", StringComparison.OrdinalIgnoreCase))
            {
                if (mapping is null)
                {
                    card = CreditCard.CreateInstitutionControlled(householdId, source.Name, userId, Provider, source.Id,
                        institution, source.Brand, source.CreditLimit, source.Balance, source.AvailableCreditLimit,
                        source.BalanceCloseDate, source.BalanceDueDate, IsActiveCard(source.Status));
                    db.CreditCards.Add(card);
                    mapping = new ExternalFinancialAccount(integration.Id, null, card.Id, source.Id, source.Type, institution, source.ContentHash);
                    db.ExternalFinancialAccounts.Add(mapping);
                }
                else
                {
                    if (mapping.CreditCardId is not Guid cardId)
                        throw new PluggyIncompleteResponseException("O tipo de uma conta externa mudou de BANK para CREDIT.");
                    card = await db.CreditCards.SingleAsync(x => x.Id == cardId, ct);
                    card.SynchronizeFromInstitution(source.Name, institution, source.Brand, source.CreditLimit,
                        source.Balance, source.AvailableCreditLimit, source.BalanceCloseDate,
                        source.BalanceDueDate, IsActiveCard(source.Status), now);
                    mapping.Refresh(source.Type, institution, source.ContentHash, now);
                }
            }
            else
            {
                if (mapping is null)
                {
                    account = new FinancialAccount(householdId, source.Name, MapAccountType(source), source.Balance,
                        DateOnly.FromDateTime(now), userId, institution);
                    account.SynchronizeFromInstitution(Provider, source.Id, source.Name, MapAccountType(source),
                        source.Balance, DateOnly.FromDateTime(now), institution, now);
                    db.FinancialAccounts.Add(account);
                    mapping = new ExternalFinancialAccount(integration.Id, account.Id, null, source.Id, source.Type, institution, source.ContentHash);
                    db.ExternalFinancialAccounts.Add(mapping);
                }
                else
                {
                    if (mapping.FinancialAccountId is not Guid accountId)
                        throw new PluggyIncompleteResponseException("O tipo de uma conta externa mudou de CREDIT para BANK.");
                    account = await db.FinancialAccounts.SingleAsync(x => x.Id == accountId, ct);
                    mapping.Refresh(source.Type, institution, source.ContentHash, now);
                }
            }
            accountRows[source.Id] = (mapping, account, card);
        }
        await db.SaveChangesAsync(ct);

        var invoiceRows = new Dictionary<string, Invoice>(StringComparer.Ordinal);
        var existingInvoices = await db.Invoices.Where(x => x.SourceProvider == Provider && x.ExternalId != null)
            .ToDictionaryAsync(x => x.ExternalId!, ct);
        foreach (var source in snapshot.CreditCardBills)
        {
            var pair = accountRows[source.AccountId];
            if (pair.Card is null) throw new PluggyIncompleteResponseException("Fatura associada a uma conta que não é cartão.");
            if (!existingInvoices.TryGetValue(source.Id, out var invoice))
            {
                invoice = Invoice.CreateInstitutionControlled(householdId, pair.Card.Id, Provider, source.Id,
                    source.ClosingDate, source.DueDate, source.TotalAmount, source.PaidDate, now);
                db.Invoices.Add(invoice);
            }
            else
                invoice.SynchronizeFromInstitution(source.ClosingDate, source.DueDate, source.TotalAmount, source.PaidDate, now);
            invoiceRows[source.Id] = invoice;
        }
        await db.SaveChangesAsync(ct);

        var mappings = await db.ExternalTransactionMappings.Where(x => x.IntegrationId == integration.Id)
            .ToDictionaryAsync(x => x.ExternalTransactionId, ct);
        var categories = await EnsureOtherCategoriesAsync(householdId, ct);
        foreach (var source in snapshot.Transactions)
        {
            var accountPair = accountRows[source.AccountId];
            var sourceAccount = snapshot.Accounts.Single(x => x.Id == source.AccountId);
            var normalized = PluggyNormalizer.Normalize(sourceAccount, source);
            mappings.TryGetValue(source.Id, out var mapping);
            FinancialTransaction? transaction = mapping?.FinancialTransactionId is Guid transactionId
                ? await db.FinancialTransactions.SingleOrDefaultAsync(x => x.Id == transactionId, ct) : null;
            if (!normalized.CreatesLedgerEntry)
            {
                if (transaction is not null && transaction.Status != FinancialTransactionStatus.Canceled)
                { await CancelExternalTransactionAsync(transaction, now, ct); deleted++; }
                if (mapping is null)
                { mapping = NewMapping(integration.Id, accountPair.Mapping.Id, source, null, normalized.Kind); db.ExternalTransactionMappings.Add(mapping); inserted++; }
                else
                {
                    mapping.MoveToAccount(accountPair.Mapping.Id);
                    // Preserve the link to a canceled ledger row so a later source correction can
                    // reactivate the same internal ID without violating the external identity key.
                    mapping.Refresh(transaction?.Id, normalized.Kind, source.ContentHash, source.UpdatedAtUtc,
                        now, source.ProviderId, source.ProviderCode, source.TransactionDateTimeUtc);
                    updated++;
                }
                continue;
            }

            var financialType = normalized.FinancialType!.Value;
            invoiceRows.TryGetValue(source.BillId ?? string.Empty, out var invoice);
            var movementReconciled = false;
            if (transaction is null)
            {
                var categoryId = await ResolveCategoryAsync(integration.Id, householdId, source,
                    financialType, categories[financialType], ct);
                transaction = accountPair.Card is not null
                    ? FinancialTransaction.CreateInstitutionCreditCardEntry(householdId, source.Description,
                        normalized.Amount, categoryId, accountPair.Card.Id,
                        invoice?.Id, source.Date, userId, !normalized.IsPending, normalized.IsRefund)
                    : new FinancialTransaction(householdId, source.Description, normalized.Amount, financialType,
                        categoryId, accountPair.Account!.Id, source.Date, source.Date, userId, ExpenseScope.Individual);
                transaction.MarkInstitutionControlled(Provider, source.Id);
                transaction.SetInstitutionInstallment(source.InstallmentNumber, source.InstallmentCount, now);
                if (accountPair.Account is not null)
                    transaction.SynchronizeInstitutionFields(source.Description, normalized.Amount, financialType,
                        accountPair.Account.Id, null, null, source.Date, !normalized.IsPending, normalized.IsRefund, now);
                db.FinancialTransactions.Add(transaction);
                if (mapping is null)
                { mapping = NewMapping(integration.Id, accountPair.Mapping.Id, source, transaction.Id, normalized.Kind); db.ExternalTransactionMappings.Add(mapping); }
                else
                { mapping.MoveToAccount(accountPair.Mapping.Id); mapping.Refresh(transaction.Id, normalized.Kind, source.ContentHash, source.UpdatedAtUtc, now, source.ProviderId, source.ProviderCode, source.TransactionDateTimeUtc); }
                inserted++;
            }
            else
            {
                var mustReconcile = mapping!.ContentHash != source.ContentHash || mapping.DeletedAtUtc is not null ||
                    transaction.Status == FinancialTransactionStatus.Canceled || transaction.AccountId != accountPair.Account?.Id ||
                    transaction.CreditCardId != accountPair.Card?.Id || transaction.InvoiceId != invoice?.Id;
                if (mustReconcile)
                {
                    transaction.SynchronizeInstitutionFields(source.Description, normalized.Amount, financialType,
                        accountPair.Account?.Id, accountPair.Card?.Id, invoice?.Id, source.Date,
                        !normalized.IsPending, normalized.IsRefund, now);
                    transaction.SetInstitutionInstallment(source.InstallmentNumber, source.InstallmentCount, now);
                    await ReplaceInstitutionMovementAsync(transaction, accountPair.Account is not null && !normalized.IsPending, ct);
                    movementReconciled = true;
                    updated++;
                }
                mapping.MoveToAccount(accountPair.Mapping.Id);
                mapping.Refresh(transaction.Id, normalized.Kind, source.ContentHash, source.UpdatedAtUtc, now,
                    source.ProviderId, source.ProviderCode, source.TransactionDateTimeUtc);
            }
            if (normalized.IsCanceled && transaction.Status != FinancialTransactionStatus.Canceled)
            { await CancelExternalTransactionAsync(transaction, now, ct); deleted++; }
            else if (!movementReconciled && !normalized.IsCanceled && accountPair.Account is not null && !normalized.IsPending &&
                     !await db.AccountMovements.AnyAsync(x => x.TransactionId == transaction.Id, ct))
                db.AccountMovements.Add(CreateMovement(transaction));
        }

        foreach (var externalId in explicitlyDeletedIds)
        {
            if (!mappings.TryGetValue(externalId, out var stale) || stale.DeletedAtUtc is not null) continue;
            if (stale.FinancialTransactionId is Guid id &&
                await db.FinancialTransactions.SingleOrDefaultAsync(x => x.Id == id, ct) is { } transaction &&
                transaction.Status != FinancialTransactionStatus.Canceled)
                await CancelExternalTransactionAsync(transaction, now, ct);
            stale.MarkDeleted(now); deleted++;
        }
        await db.SaveChangesAsync(ct);
        foreach (var source in snapshot.Accounts.Where(x => !x.Type.Equals("CREDIT", StringComparison.OrdinalIgnoreCase)))
        {
            var account = accountRows[source.Id].Account!;
            var net = await db.AccountMovements.Where(x => x.AccountId == account.Id).SumAsync(x => (decimal?)x.SignedAmount, ct) ?? 0m;
            account.SynchronizeFromInstitution(Provider, source.Id, source.Name, MapAccountType(source), source.Balance - net,
                DateOnly.FromDateTime(now), source.InstitutionName ?? snapshot.Item.ConnectorName, now);
        }
        return new(inserted, updated, deleted, accountsChanged);
    }

    private async Task NotifyChangesAsync(string email, PluggySyncResult result, PluggySnapshot snapshot,
        bool initialImport, CancellationToken ct)
    {
        if (notifications is null) return;
        var institutions = snapshot.Accounts
            .Select(x => x.InstitutionName ?? snapshot.Item.ConnectorName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
        try
        {
            await notifications.NotifyAsync(new AccountUpdateNotification(
                email, initialImport, result.Inserted, result.Updated, result.Deleted,
                result.AccountsChanged, institutions, DateTime.UtcNow), ct);
        }
        catch (Exception)
        {
            logger.LogWarning("Account update notification failed after a successful Pluggy synchronization");
        }
    }

    private static ExternalTransactionMapping NewMapping(Guid integrationId, Guid accountMappingId, PluggyTransaction source, Guid? transactionId, ExternalTransactionKind kind) =>
        new(integrationId, accountMappingId, source.Id, transactionId, kind, source.ContentHash, source.UpdatedAtUtc, source.ProviderId, source.ProviderCode, source.TransactionDateTimeUtc);
    private static bool IsActiveCard(string? status) => !string.Equals(status, "BLOCKED", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(status, "CANCELLED", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(status, "CANCELED", StringComparison.OrdinalIgnoreCase);
    private static FinancialAccountType MapAccountType(PluggyAccount account) => account.Type.ToUpperInvariant() switch { "BANK" when account.Subtype?.Contains("SAV", StringComparison.OrdinalIgnoreCase) == true => FinancialAccountType.Savings, "BANK" => FinancialAccountType.Checking, _ => FinancialAccountType.Other };
    private static AccountMovement CreateMovement(FinancialTransaction tx) => new(tx.HouseholdId, tx.AccountId!.Value, tx.Id,
        tx.Type == FinancialTransactionType.Income || tx.IsRefund ? tx.Amount : -tx.Amount,
        tx.IsRefund ? AccountMovementKind.Reversal : tx.Type == FinancialTransactionType.Income ? AccountMovementKind.Income : AccountMovementKind.Expense,
        tx.SettledDate!.Value, 1);
    private async Task ReplaceInstitutionMovementAsync(FinancialTransaction tx, bool shouldExist, CancellationToken ct)
    {
        var existing = await db.AccountMovements.Where(x => x.TransactionId == tx.Id).ToListAsync(ct);
        if (existing.Count > 0) db.AccountMovements.RemoveRange(existing);
        if (shouldExist) db.AccountMovements.Add(CreateMovement(tx));
    }
    private async Task CancelExternalTransactionAsync(FinancialTransaction tx, DateTime now, CancellationToken ct)
    {
        var movements = await db.AccountMovements.Where(x => x.TransactionId == tx.Id).ToListAsync(ct);
        if (movements.Count > 0) db.AccountMovements.RemoveRange(movements);
        tx.Cancel(now);
    }

    private async Task RecordFailureAsync(Guid integrationId, string trigger, Exception exception, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var failed = await db.FinancialIntegrations.SingleAsync(x => x.Id == integrationId, ct);
        var message = exception is PluggyConsentExpiredException
            ? "O consentimento com a instituição expirou."
            : "Falha temporária ao sincronizar com a instituição.";
        failed.Fail(message, exception is PluggyConsentExpiredException, DateTime.UtcNow);
        var sync = new FinancialIntegrationSync(integrationId, trigger);
        sync.Fail(message, DateTime.UtcNow);
        db.FinancialIntegrationSyncs.Add(sync);
        await db.SaveChangesAsync(ct);
    }

    private static HashSet<string> ExtractDeletedTransactionIds(string? payload)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(payload)) return ids;
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            Visit(root, null, false);
        }
        catch (JsonException) { }
        return ids;

        void Visit(JsonElement element, string? propertyName, bool allowDirectId)
        {
            if (element.ValueKind == JsonValueKind.Object)
                foreach (var property in element.EnumerateObject())
                    Visit(property.Value, property.Name,
                        property.Name.Equals("data", StringComparison.OrdinalIgnoreCase) ||
                        (allowDirectId && property.Name.Equals("id", StringComparison.OrdinalIgnoreCase)));
            else if (element.ValueKind == JsonValueKind.Array)
                foreach (var item in element.EnumerateArray()) Visit(item, propertyName, allowDirectId);
            else if (element.ValueKind == JsonValueKind.String &&
                     propertyName is not null &&
                     (propertyName.Equals("transactionId", StringComparison.OrdinalIgnoreCase) ||
                      propertyName.Equals("transactionIds", StringComparison.OrdinalIgnoreCase) ||
                      (allowDirectId && propertyName.Equals("id", StringComparison.OrdinalIgnoreCase))))
            {
                var value = element.GetString(); if (!string.IsNullOrWhiteSpace(value)) ids.Add(value);
            }
        }
    }

    private static void ValidateSnapshot(PluggySnapshot snapshot)
    {
        var accountIds = snapshot.Accounts.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        if (accountIds.Count != snapshot.Accounts.Count)
            throw new PluggyIncompleteResponseException("A Pluggy retornou contas duplicadas.");
        if (snapshot.Transactions.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != snapshot.Transactions.Count)
            throw new PluggyIncompleteResponseException("A Pluggy retornou transações duplicadas.");
        if (snapshot.CreditCardBills.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != snapshot.CreditCardBills.Count)
            throw new PluggyIncompleteResponseException("A Pluggy retornou faturas duplicadas.");
        if (snapshot.Transactions.Any(x => !accountIds.Contains(x.AccountId)) ||
            snapshot.CreditCardBills.Any(x => !accountIds.Contains(x.AccountId)))
            throw new PluggyIncompleteResponseException("A Pluggy retornou dados associados a uma conta ausente.");
    }

    private async Task<Dictionary<FinancialTransactionType,Guid>> EnsureOtherCategoriesAsync(Guid householdId, CancellationToken ct)
    {
        var result = new Dictionary<FinancialTransactionType,Guid>();
        foreach (var pair in new[] { (FinancialTransactionType.Income, CategoryType.Income), (FinancialTransactionType.Expense, CategoryType.Expense) })
        {
            var category = await db.Categories.FirstOrDefaultAsync(x => x.HouseholdId == householdId && x.Type == pair.Item2 && x.Name == "Outros", ct);
            if (category is null) { category = new Category(householdId, "Outros", pair.Item2, "#64748b", "circle-ellipsis", true); db.Categories.Add(category); }
            result[pair.Item1] = category.Id;
        }
        await db.SaveChangesAsync(ct); return result;
    }

    private async Task<Guid> ResolveCategoryAsync(Guid integrationId, Guid householdId,
        PluggyTransaction source, FinancialTransactionType type, Guid fallback, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(source.ProviderId) || !string.IsNullOrWhiteSpace(source.ProviderCode))
        {
            var known = await db.ExternalTransactionMappings.AsNoTracking()
                .Where(x => x.IntegrationId == integrationId && x.FinancialTransactionId != null &&
                    ((!string.IsNullOrWhiteSpace(source.ProviderId) && x.ProviderId == source.ProviderId) ||
                     (!string.IsNullOrWhiteSpace(source.ProviderCode) && x.ProviderCode == source.ProviderCode)))
                .Join(db.FinancialTransactions.AsNoTracking(), x => x.FinancialTransactionId, x => x.Id,
                    (_, transaction) => transaction.CategoryId)
                .FirstOrDefaultAsync(ct);
            if (known is Guid knownId) return knownId;
        }
        var expected = type == FinancialTransactionType.Income ? CategoryType.Income : CategoryType.Expense;
        var normalizedDescription = source.Description.Trim().ToUpper();
        var learned = await db.FinancialTransactions.AsNoTracking()
            .Where(x => x.HouseholdId == householdId && x.CategoryId != null &&
                        x.Description.ToUpper() == normalizedDescription)
            .Join(db.Categories.AsNoTracking().Where(x => x.Type == expected && x.Name != "Outros"),
                transaction => transaction.CategoryId, category => category.Id, (_, category) => (Guid?)category.Id)
            .FirstOrDefaultAsync(ct);
        if (learned is Guid learnedId) return learnedId;
        if (!string.IsNullOrWhiteSpace(source.Category))
        {
            var pluggyCategory = source.Category.Trim().ToUpper();
            var matching = await db.Categories.AsNoTracking()
                .Where(x => x.HouseholdId == householdId && x.IsActive && x.Type == expected &&
                            x.Name.ToUpper() == pluggyCategory)
                .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
            if (matching is Guid matchingId) return matchingId;
        }
        return fallback;
    }

    private async Task DeleteOwnerFinancialDataAsync(Guid userId, Guid householdId, CancellationToken ct)
    {
        var accountIds = await db.FinancialAccounts.Where(x => x.HouseholdId == householdId && x.ResponsibleUserId == userId).Select(x => x.Id).ToListAsync(ct);
        var cardIds = await db.CreditCards.Where(x => x.HouseholdId == householdId && x.ResponsibleUserId == userId).Select(x => x.Id).ToListAsync(ct);
        if (await db.FinancialTransactions.AnyAsync(x => x.HouseholdId == householdId && x.ResponsibleUserId != userId &&
                ((x.AccountId != null && accountIds.Contains(x.AccountId.Value)) || (x.CreditCardId != null && cardIds.Contains(x.CreditCardId.Value))), ct))
            throw new ConflictException("Existem lançamentos de outro usuário vinculados aos dados que seriam substituídos.");
        if (await db.CreditCardSubscriptions.AnyAsync(x => x.HouseholdId == householdId &&
                x.ResponsibleUserId != userId && cardIds.Contains(x.CreditCardId), ct) ||
            await db.RecurringTransactions.AnyAsync(x => x.HouseholdId == householdId &&
                x.ResponsibleUserId != userId && accountIds.Contains(x.AccountId), ct) ||
            await db.InstallmentPlans.AnyAsync(x => x.HouseholdId == householdId && x.ResponsibleUserId != userId &&
                ((x.SourceType == InstallmentSourceType.FinancialAccount && accountIds.Contains(x.SourceId)) ||
                 (x.SourceType == InstallmentSourceType.CreditCard && cardIds.Contains(x.SourceId))), ct))
            throw new ConflictException("Existem automações de outro usuário vinculadas aos dados que seriam substituídos.");
        var transactionIds = await db.FinancialTransactions.Where(x => x.HouseholdId == householdId && x.ResponsibleUserId == userId).Select(x => x.Id).ToListAsync(ct);
        db.AccountMovements.RemoveRange(db.AccountMovements.Where(x => transactionIds.Contains(x.TransactionId) || accountIds.Contains(x.AccountId)));
        var invoices = db.Invoices.Where(x => cardIds.Contains(x.CreditCardId));
        foreach (var invoice in await invoices.ToListAsync(ct)) db.Entry(invoice).Property(nameof(Invoice.PaymentTransactionId)).CurrentValue = null;
        await db.SaveChangesAsync(ct);
        db.FinancialTransactions.RemoveRange(db.FinancialTransactions.Where(x => transactionIds.Contains(x.Id)));
        db.Invoices.RemoveRange(invoices); db.CreditCardSubscriptions.RemoveRange(db.CreditCardSubscriptions.Where(x => cardIds.Contains(x.CreditCardId)));
        db.InstallmentPlans.RemoveRange(db.InstallmentPlans.Where(x => x.HouseholdId == householdId && x.ResponsibleUserId == userId));
        db.RecurringTransactions.RemoveRange(db.RecurringTransactions.Where(x => x.HouseholdId == householdId && x.ResponsibleUserId == userId));
        db.CreditCards.RemoveRange(db.CreditCards.Where(x => cardIds.Contains(x.Id))); db.FinancialAccounts.RemoveRange(db.FinancialAccounts.Where(x => accountIds.Contains(x.Id)));
        await db.SaveChangesAsync(ct);
    }

    private async Task<User> RequireAuthorizedUserAsync(CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == currentUser.UserId, ct) ?? throw new ForbiddenException("Usuário não autorizado.");
        if (!string.Equals(user.Email, _options.AuthorizedEmail, StringComparison.OrdinalIgnoreCase)) throw new ForbiddenException("Usuário não autorizado para Open Finance.");
        return user;
    }
    private async Task<Guid> RequireOwnedHouseholdAsync(Guid userId, CancellationToken ct) =>
        await db.HouseholdMemberships.Where(x => x.UserId == userId && x.Role == HouseholdRole.Owner).Select(x => x.HouseholdId).SingleOrDefaultAsync(ct) is var id && id != Guid.Empty
            ? id : throw new ConflictException("O usuário autorizado deve possuir um único núcleo financeiro.");
    private async Task<FinancialIntegration?> FindAuthorizedIntegrationAsync(CancellationToken ct) { var user = await RequireAuthorizedUserAsync(ct); return await db.FinancialIntegrations.SingleOrDefaultAsync(x => x.UserId == user.Id && x.Provider == Provider, ct); }
    private async Task<IDbContextTransaction?> BeginSerializableAsync(CancellationToken ct) => db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
}
