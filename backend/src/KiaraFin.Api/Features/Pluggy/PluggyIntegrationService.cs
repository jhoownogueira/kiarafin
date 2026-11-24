using System.Data;
using KiaraFin.Api.Common.Exceptions;
using KiaraFin.Api.Common.Security;
using KiaraFin.Api.Data;
using KiaraFin.Api.Features.Cards;
using KiaraFin.Api.Features.Finance;
using KiaraFin.Api.Features.Forecasts;
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

/// <summary>
/// Espelha o Item Pluggy no banco: contas, cartões, faturas fechadas e lançamentos.
/// A Pluggy é a fonte da verdade; cada sincronização insere, atualiza e remove registros
/// para que o estado local seja exatamente o snapshot recebido. Isso é essencial porque
/// lançamentos pendentes costumam ser substituídos por lançamentos postados com outro id.
/// </summary>
public sealed class PluggyIntegrationService(AppDbContext db, ICurrentUser currentUser,
    IPluggyClient client, IOptions<PluggyOptions> options, ILogger<PluggyIntegrationService> logger,
    IAccountNotificationService? notifications = null, BalanceForecastService? forecast = null)
{
    private const string Provider = "PLUGGY";
    private readonly PluggyOptions _options = options.Value;

    public async Task<PluggyStatusResponse> GetStatusAsync(CancellationToken ct)
    {
        var user = await RequireAuthorizedUserAsync(ct);
        var integration = await db.FinancialIntegrations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == user.Id && x.Provider == Provider, ct);
        if (integration is null)
            return new(FinancialIntegrationStatus.Disabled, null, null, null, null, 0, 0, null, null, false, [], null);
        var accountInstitutions = await db.FinancialAccounts.AsNoTracking()
            .Where(x => x.HouseholdId == integration.HouseholdId && x.SourceProvider == Provider && x.Institution != null)
            .Select(x => x.Institution!).ToListAsync(ct);
        var cardInstitutions = await db.CreditCards.AsNoTracking()
            .Where(x => x.HouseholdId == integration.HouseholdId && x.Institution != null)
            .Select(x => x.Institution!).ToListAsync(ct);
        var institutions = accountInstitutions.Concat(cardInstitutions)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        DateTime? next = integration.Status == FinancialIntegrationStatus.Connected &&
                         integration.LastSuccessfulSyncAtUtc is DateTime last
            ? last.AddHours(_options.SyncIntervalHours) : null;
        return new(integration.Status, integration.ItemId, integration.ActivatedAtUtc, integration.LastSuccessfulSyncAtUtc,
            integration.LastAttemptAtUtc, integration.AccountCount, integration.TransactionCount, integration.ConsentExpiresAtUtc,
            integration.LastError, integration.InitialImportCompleted, institutions, next);
    }

    public async Task<PluggyPreviewResponse> PreviewAsync(CancellationToken ct)
    {
        var user = await RequireAuthorizedUserAsync(ct);
        var householdId = await RequireOwnedHouseholdAsync(user.Id, ct);
        var snapshot = await client.ReadCompleteSnapshotAsync(_options.ItemId, ct);
        var cardIds = db.CreditCards.Where(x => x.HouseholdId == householdId).Select(x => x.Id);
        return new(
            await db.FinancialAccounts.CountAsync(x => x.HouseholdId == householdId, ct),
            await cardIds.CountAsync(ct),
            await db.BankTransactions.CountAsync(x => x.HouseholdId == householdId, ct) +
            await db.CardTransactions.CountAsync(x => cardIds.Contains(x.CardId), ct),
            snapshot.Accounts.Count, snapshot.Transactions.Count, snapshot.Item.ConsentExpiresAtUtc);
    }

    public async Task<PluggySyncResult> ActivateAsync(bool confirmed, CancellationToken ct)
    {
        if (!confirmed) throw new AppValidationException("A confirmação explícita é obrigatória.");
        var user = await RequireAuthorizedUserAsync(ct);
        var householdId = await RequireOwnedHouseholdAsync(user.Id, ct);
        var snapshot = await client.ReadCompleteSnapshotAsync(_options.ItemId, ct);
        var previousForecast = await CurrentForecastAsync(householdId, ct);
        await using var tx = await BeginSerializableAsync(ct);
        var integration = await db.FinancialIntegrations
            .SingleOrDefaultAsync(x => x.UserId == user.Id && x.Provider == Provider, ct);
        if (integration?.InitialImportCompleted == true)
            throw new ConflictException("A importação inicial já foi concluída.");
        if (integration is null)
        {
            integration = new FinancialIntegration(user.Id, householdId, Provider, snapshot.Item.Id);
            db.FinancialIntegrations.Add(integration);
        }
        integration.BeginSync(true, DateTime.UtcNow);
        var result = await PersistSnapshotAsync(householdId, snapshot, ct);
        integration.Complete(snapshot.Accounts.Count, snapshot.Transactions.Count, snapshot.Item.ConsentExpiresAtUtc, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        await NotifyChangesAsync(user.Email, result, snapshot, true, ct);
        await NotifyForecastChangeAsync(user.Email, householdId, previousForecast, ct);
        return result;
    }

    public async Task<PluggySyncResult> SynchronizeAsync(Guid? integrationId, string trigger, CancellationToken ct)
    {
        var integration = integrationId is Guid id
            ? await db.FinancialIntegrations.SingleOrDefaultAsync(x => x.Id == id, ct)
            : await FindAuthorizedIntegrationAsync(ct);
        if (integration is null || !integration.InitialImportCompleted)
            throw new ConflictException("A integração ainda não foi ativada.");
        var targetItemId = !string.IsNullOrWhiteSpace(_options.ItemId) ? _options.ItemId : integration.ItemId;
        PluggySnapshot snapshot;
        try { snapshot = await client.ReadCompleteSnapshotAsync(targetItemId, ct); }
        catch (Exception ex)
        {
            await RecordFailureAsync(integration.Id, trigger, ex, ct);
            throw;
        }
        var previousForecast = await CurrentForecastAsync(integration.HouseholdId, ct);
        await using var tx = await BeginSerializableAsync(ct);
        try
        {
            if (db.Database.IsRelational())
            {
                var acquired = await db.Database.SqlQueryRaw<bool>(
                    "SELECT pg_try_advisory_xact_lock(hashtextextended({0}, 0)) AS \"Value\"",
                    integration.Id.ToString()).SingleAsync(ct);
                if (!acquired) throw new ConflictException("Já existe uma sincronização em execução.");
            }
            if (!string.Equals(integration.ItemId, targetItemId, StringComparison.Ordinal))
                integration.ReplaceRenewedItem(targetItemId, DateTime.UtcNow);
            integration.BeginSync(false, DateTime.UtcNow);
            var sync = new FinancialIntegrationSync(integration.Id, trigger);
            db.FinancialIntegrationSyncs.Add(sync);
            var result = await PersistSnapshotAsync(integration.HouseholdId, snapshot, ct);
            sync.Complete(result.Inserted, result.Updated, result.Deleted, DateTime.UtcNow);
            integration.Complete(snapshot.Accounts.Count, snapshot.Transactions.Count, snapshot.Item.ConsentExpiresAtUtc, DateTime.UtcNow);
            await db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
            await NotifyChangesAsync(_options.AuthorizedEmail, result, snapshot, false, ct);
            await NotifyForecastChangeAsync(_options.AuthorizedEmail, integration.HouseholdId, previousForecast, ct);
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
            if (tx is not null) await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            await RecordFailureAsync(integration.Id, trigger, ex, ct);
            logger.LogWarning(ex, "Pluggy sync failed for integration {IntegrationId}", integration.Id);
            throw;
        }
    }

    internal async Task<PluggySyncResult> PersistSnapshotAsync(Guid householdId, PluggySnapshot snapshot, CancellationToken ct)
    {
        ValidateSnapshot(snapshot);
        var now = DateTime.UtcNow;
        int inserted = 0, updated = 0, deleted = 0, accountsChanged = 0;
        var transactionsByAccount = snapshot.Transactions.ToLookup(x => x.AccountId, StringComparer.Ordinal);
        var billsByAccount = snapshot.CreditCardBills.ToLookup(x => x.AccountId, StringComparer.Ordinal);

        var accounts = await db.FinancialAccounts
            .Where(x => x.HouseholdId == householdId && x.SourceProvider == Provider)
            .ToDictionaryAsync(x => x.ExternalId!, StringComparer.Ordinal, ct);
        var cards = await db.CreditCards.Where(x => x.HouseholdId == householdId)
            .ToDictionaryAsync(x => x.ExternalId, StringComparer.Ordinal, ct);

        foreach (var source in snapshot.Accounts.Where(x => !x.IsCreditCard))
        {
            var institution = source.InstitutionName ?? snapshot.Item.ConnectorName;
            if (!accounts.TryGetValue(source.Id, out var account))
            {
                account = FinancialAccount.FromInstitution(householdId, Provider, source.Id, source.Name,
                    MapAccountType(source), source.Balance, institution);
                db.FinancialAccounts.Add(account);
                accounts[source.Id] = account;
                accountsChanged++;
            }
            else if (account.SynchronizeFromInstitution(source.Name, MapAccountType(source), source.Balance, institution, now))
                accountsChanged++;

            var (i, u, d) = await SyncBankTransactionsAsync(householdId, account, transactionsByAccount[source.Id], now, ct);
            inserted += i; updated += u; deleted += d;
        }

        foreach (var source in snapshot.Accounts.Where(x => x.IsCreditCard))
        {
            if (!cards.TryGetValue(source.Id, out var card))
            {
                card = new CreditCard(householdId, source.Id);
                db.CreditCards.Add(card);
                cards[source.Id] = card;
            }
            if (card.Apply(source.Name, source.InstitutionName ?? snapshot.Item.ConnectorName, source.Brand,
                    source.Number, source.CreditLimit, source.AvailableCreditLimit, source.Balance,
                    source.MinimumPayment, source.BalanceCloseDate, source.BalanceDueDate,
                    IsActiveCard(source.Status), now))
                accountsChanged++;

            var bills = billsByAccount[source.Id].ToList();
            await SyncBillsAsync(card, bills, now, ct);
            var (i, u, d) = await SyncCardTransactionsAsync(card, transactionsByAccount[source.Id],
                bills.Select(x => x.TotalAmount).ToList(), now, ct);
            inserted += i; updated += u; deleted += d;
        }

        // Contas e cartões que não existem mais na origem são removidos (com seus lançamentos).
        var accountIds = snapshot.Accounts.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var stale in accounts.Where(x => !accountIds.Contains(x.Key)).Select(x => x.Value))
        {
            db.BankTransactions.RemoveRange(db.BankTransactions.Where(x => x.AccountId == stale.Id));
            db.FinancialAccounts.Remove(stale);
            accountsChanged++;
        }
        foreach (var stale in cards.Where(x => !accountIds.Contains(x.Key)).Select(x => x.Value))
        {
            db.CardTransactions.RemoveRange(db.CardTransactions.Where(x => x.CardId == stale.Id));
            db.CardBills.RemoveRange(db.CardBills.Where(x => x.CardId == stale.Id));
            db.CreditCards.Remove(stale);
            accountsChanged++;
        }
        await db.SaveChangesAsync(ct);
        return new(inserted, updated, deleted, accountsChanged);
    }

    private async Task<(int Inserted, int Updated, int Deleted)> SyncBankTransactionsAsync(Guid householdId,
        FinancialAccount account, IEnumerable<PluggyTransaction> sources, DateTime now, CancellationToken ct)
    {
        var existing = await db.BankTransactions.Where(x => x.AccountId == account.Id)
            .ToDictionaryAsync(x => x.ExternalId, StringComparer.Ordinal, ct);
        int inserted = 0, updated = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            seen.Add(source.Id);
            if (!existing.TryGetValue(source.Id, out var transaction))
            {
                transaction = new BankTransaction(householdId, account.Id, source.Id);
                db.BankTransactions.Add(transaction);
                transaction.Apply(source.Description, source.Amount, source.Date, source.IsPending, source.Category, now);
                inserted++;
            }
            else if (transaction.Apply(source.Description, source.Amount, source.Date, source.IsPending, source.Category, now))
                updated++;
        }
        var stale = existing.Values.Where(x => !seen.Contains(x.ExternalId)).ToList();
        db.BankTransactions.RemoveRange(stale);
        return (inserted, updated, stale.Count);
    }

    private async Task SyncBillsAsync(CreditCard card, IReadOnlyList<PluggyBill> sources, DateTime now, CancellationToken ct)
    {
        var existing = await db.CardBills.Where(x => x.CardId == card.Id)
            .ToDictionaryAsync(x => x.ExternalId, StringComparer.Ordinal, ct);
        foreach (var source in sources)
        {
            if (!existing.TryGetValue(source.Id, out var bill))
            {
                bill = new CardBill(card.Id, source.Id);
                db.CardBills.Add(bill);
            }
            bill.Apply(source.DueDate, source.ClosingDate, Round(source.TotalAmount), Round(source.MinimumPayment),
                Round(source.PaidAmount), source.LastPaymentDate, now);
        }
        var ids = sources.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        db.CardBills.RemoveRange(existing.Values.Where(x => !ids.Contains(x.ExternalId)));
    }

    private async Task<(int Inserted, int Updated, int Deleted)> SyncCardTransactionsAsync(CreditCard card,
        IEnumerable<PluggyTransaction> sources, IReadOnlyCollection<decimal> billTotals, DateTime now, CancellationToken ct)
    {
        var existing = await db.CardTransactions.Where(x => x.CardId == card.Id)
            .ToDictionaryAsync(x => x.ExternalId, StringComparer.Ordinal, ct);
        int inserted = 0, updated = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            if (source.Amount == 0) continue;
            seen.Add(source.Id);
            var kind = PluggyNormalizer.ClassifyCardTransaction(source, billTotals);
            var isNew = !existing.TryGetValue(source.Id, out var transaction);
            if (isNew)
            {
                transaction = new CardTransaction(card.Id, source.Id);
                db.CardTransactions.Add(transaction);
            }
            var validInstallment = source.InstallmentNumber is int n && source.TotalInstallments is int t && n >= 1 && t >= n;
            var changed = transaction!.Apply(
                source.Description.Trim(), Round(Math.Abs(source.Amount)), kind, source.Date, source.PurchaseDate,
                source.IsPending, string.IsNullOrWhiteSpace(source.BillId) ? null : source.BillId,
                validInstallment ? source.InstallmentNumber : null, validInstallment ? source.TotalInstallments : null,
                source.Category, source.CurrencyCode,
                source.OriginalAmount is decimal original ? Round(Math.Abs(original)) : null, now);
            if (isNew) inserted++;
            else if (changed) updated++;
        }
        var stale = existing.Values.Where(x => !seen.Contains(x.ExternalId)).ToList();
        db.CardTransactions.RemoveRange(stale);
        return (inserted, updated, stale.Count);
    }

    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static decimal? Round(decimal? value) => value is decimal v ? Round(v) : null;

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

    private async Task<decimal?> CurrentForecastAsync(Guid householdId, CancellationToken ct)
    {
        if (forecast is null || notifications is null) return null;
        try { return (await forecast.CalculateAsync(householdId, ct)).Net; }
        catch (Exception exception) when (exception is not OperationCanceledException) { return null; }
    }

    private async Task NotifyForecastChangeAsync(string email, Guid householdId, decimal? previousResult, CancellationToken ct)
    {
        if (forecast is null || notifications is null) return;
        try
        {
            var current = await forecast.CalculateAsync(householdId, ct);
            await notifications.NotifyForecastAsync(BalanceForecastNotification.From(
                email, BalanceForecastNotificationReason.OpenFinanceSync, current, previousResult), ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning("Forecast notification failed after a successful Pluggy synchronization");
        }
    }

    private static bool IsActiveCard(string? status) =>
        !string.Equals(status, "BLOCKED", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(status, "CANCELLED", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(status, "CANCELED", StringComparison.OrdinalIgnoreCase);

    private static FinancialAccountType MapAccountType(PluggyAccount account) => account.Type.ToUpperInvariant() switch
    {
        "BANK" when account.Subtype?.Contains("SAV", StringComparison.OrdinalIgnoreCase) == true => FinancialAccountType.Savings,
        "BANK" => FinancialAccountType.Checking,
        _ => FinancialAccountType.Other
    };

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

    private static void ValidateSnapshot(PluggySnapshot snapshot)
    {
        var accountIds = snapshot.Accounts.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        if (accountIds.Count != snapshot.Accounts.Count)
            throw new PluggyIncompleteResponseException("A Pluggy retornou contas duplicadas.");
        if (snapshot.Transactions.Select(x => (x.AccountId, x.Id)).Distinct().Count() != snapshot.Transactions.Count)
            throw new PluggyIncompleteResponseException("A Pluggy retornou transações duplicadas.");
        if (snapshot.CreditCardBills.Select(x => (x.AccountId, x.Id)).Distinct().Count() != snapshot.CreditCardBills.Count)
            throw new PluggyIncompleteResponseException("A Pluggy retornou faturas duplicadas.");
        if (snapshot.Transactions.Any(x => !accountIds.Contains(x.AccountId)) ||
            snapshot.CreditCardBills.Any(x => !accountIds.Contains(x.AccountId)))
            throw new PluggyIncompleteResponseException("A Pluggy retornou dados associados a uma conta ausente.");
    }

    private async Task<User> RequireAuthorizedUserAsync(CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == currentUser.UserId, ct)
            ?? throw new ForbiddenException("Usuário não autorizado.");
        if (!string.Equals(user.Email, _options.AuthorizedEmail, StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Usuário não autorizado para Open Finance.");
        return user;
    }

    private async Task<Guid> RequireOwnedHouseholdAsync(Guid userId, CancellationToken ct) =>
        await db.HouseholdMemberships.Where(x => x.UserId == userId && x.Role == HouseholdRole.Owner)
            .Select(x => x.HouseholdId).SingleOrDefaultAsync(ct) is var id && id != Guid.Empty
            ? id : throw new ConflictException("O usuário autorizado deve possuir um único núcleo financeiro.");

    private async Task<FinancialIntegration?> FindAuthorizedIntegrationAsync(CancellationToken ct)
    {
        var user = await RequireAuthorizedUserAsync(ct);
        return await db.FinancialIntegrations.SingleOrDefaultAsync(x => x.UserId == user.Id && x.Provider == Provider, ct);
    }

    private async Task<IDbContextTransaction?> BeginSerializableAsync(CancellationToken ct) =>
        db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
}
