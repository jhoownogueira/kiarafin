namespace KiaraFin.Api.Features.Pluggy;

public enum FinancialIntegrationStatus
{
    Disabled,
    PendingConfirmation,
    InitialSync,
    Connected,
    Syncing,
    Error,
    ConsentExpired
}

public enum ExternalTransactionKind
{
    Unknown,
    Income,
    Expense,
    InvoicePayment,
    Transfer,
    Refund
}

public enum PluggyJobStatus { Pending, Processing, Completed, Failed }

public sealed class FinancialIntegration
{
    private FinancialIntegration() { }
    public FinancialIntegration(Guid userId, Guid householdId, string provider, string itemId)
    {
        Id = Guid.NewGuid(); UserId = userId; HouseholdId = householdId;
        Provider = provider; ItemId = itemId; Status = FinancialIntegrationStatus.PendingConfirmation;
        CreatedAtUtc = DateTime.UtcNow;
    }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid HouseholdId { get; private set; }
    public string Provider { get; private set; } = null!;
    public string ItemId { get; private set; } = null!;
    public FinancialIntegrationStatus Status { get; private set; }
    public DateTime? ActivatedAtUtc { get; private set; }
    public DateTime? LastSuccessfulSyncAtUtc { get; private set; }
    public DateTime? LastAttemptAtUtc { get; private set; }
    public int AccountCount { get; private set; }
    public int TransactionCount { get; private set; }
    public DateTime? ConsentExpiresAtUtc { get; private set; }
    public string? LastError { get; private set; }
    public bool InitialImportCompleted { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public void BeginSync(bool initial, DateTime now) { Status = initial ? FinancialIntegrationStatus.InitialSync : FinancialIntegrationStatus.Syncing; LastAttemptAtUtc = now; LastError = null; UpdatedAtUtc = now; }
    public void Complete(int accounts, int transactions, DateTime? consentExpiresAt, DateTime now)
    {
        Status = FinancialIntegrationStatus.Connected; ActivatedAtUtc ??= now; LastSuccessfulSyncAtUtc = now;
        AccountCount = accounts; TransactionCount = transactions; ConsentExpiresAtUtc = consentExpiresAt;
        InitialImportCompleted = true; LastError = null; UpdatedAtUtc = now;
    }
    public void Fail(string error, bool consentExpired, DateTime now)
    {
        Status = consentExpired ? FinancialIntegrationStatus.ConsentExpired : FinancialIntegrationStatus.Error;
        LastAttemptAtUtc = now; LastError = error.Length > 500 ? error[..500] : error; UpdatedAtUtc = now;
    }
    public void SetItem(string itemId, DateTime now) { ItemId = itemId; Status = FinancialIntegrationStatus.PendingConfirmation; UpdatedAtUtc = now; }
    public void ReplaceRenewedItem(string itemId, DateTime now)
    { ItemId = itemId; Status = FinancialIntegrationStatus.Syncing; LastError = null; UpdatedAtUtc = now; }
}

public sealed class ExternalFinancialAccount
{
    private ExternalFinancialAccount() { }
    public ExternalFinancialAccount(Guid integrationId, Guid? financialAccountId, Guid? creditCardId,
        string externalAccountId, string accountType, string? institutionName, string contentHash)
    {
        if ((financialAccountId is null) == (creditCardId is null))
            throw new ArgumentException("A conta externa deve apontar para uma conta bancária ou cartão.");
        Id = Guid.NewGuid(); IntegrationId = integrationId; FinancialAccountId = financialAccountId;
        CreditCardId = creditCardId;
        ExternalAccountId = externalAccountId; AccountType = accountType; InstitutionName = institutionName;
        ContentHash = contentHash; LastSeenAtUtc = DateTime.UtcNow;
    }
    public Guid Id { get; private set; }
    public Guid IntegrationId { get; private set; }
    public Guid? FinancialAccountId { get; private set; }
    public Guid? CreditCardId { get; private set; }
    public string ExternalAccountId { get; private set; } = null!;
    public string AccountType { get; private set; } = null!;
    public string? InstitutionName { get; private set; }
    public string ContentHash { get; private set; } = null!;
    public DateTime LastSeenAtUtc { get; private set; }
    public void Refresh(string accountType, string? institutionName, string hash, DateTime now)
    { AccountType = accountType; InstitutionName = institutionName; ContentHash = hash; LastSeenAtUtc = now; }
}

public sealed class ExternalTransactionMapping
{
    private ExternalTransactionMapping() { }
    public ExternalTransactionMapping(Guid integrationId, Guid externalAccountId, string externalTransactionId,
        Guid? financialTransactionId, ExternalTransactionKind kind, string contentHash, DateTime? sourceUpdatedAtUtc,
        string? providerId = null, string? providerCode = null, DateTime? transactionDateTimeUtc = null)
    {
        Id = Guid.NewGuid(); IntegrationId = integrationId; ExternalAccountId = externalAccountId;
        ExternalTransactionId = externalTransactionId; FinancialTransactionId = financialTransactionId;
        Kind = kind; ContentHash = contentHash; SourceUpdatedAtUtc = sourceUpdatedAtUtc; ProviderId = providerId;
        ProviderCode = providerCode; TransactionDateTimeUtc = transactionDateTimeUtc; LastSeenAtUtc = DateTime.UtcNow;
    }
    public Guid Id { get; private set; }
    public Guid IntegrationId { get; private set; }
    public Guid ExternalAccountId { get; private set; }
    public string ExternalTransactionId { get; private set; } = null!;
    public Guid? FinancialTransactionId { get; private set; }
    public ExternalTransactionKind Kind { get; private set; }
    public string ContentHash { get; private set; } = null!;
    public DateTime? SourceUpdatedAtUtc { get; private set; }
    public string? ProviderId { get; private set; }
    public string? ProviderCode { get; private set; }
    public DateTime? TransactionDateTimeUtc { get; private set; }
    public DateTime LastSeenAtUtc { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public void Refresh(Guid? transactionId, ExternalTransactionKind kind, string hash, DateTime? updated, DateTime now,
        string? providerId = null, string? providerCode = null, DateTime? transactionDateTimeUtc = null)
    { FinancialTransactionId = transactionId; Kind = kind; ContentHash = hash; SourceUpdatedAtUtc = updated;
      ProviderId = providerId; ProviderCode = providerCode; TransactionDateTimeUtc = transactionDateTimeUtc;
      LastSeenAtUtc = now; DeletedAtUtc = null; }
    public void MoveToAccount(Guid externalAccountId) => ExternalAccountId = externalAccountId;
    public void MarkDeleted(DateTime now) { DeletedAtUtc = now; LastSeenAtUtc = now; }
}

public sealed class FinancialIntegrationSync
{
    private FinancialIntegrationSync() { }
    public FinancialIntegrationSync(Guid integrationId, string trigger)
    { Id = Guid.NewGuid(); IntegrationId = integrationId; Trigger = trigger; StartedAtUtc = DateTime.UtcNow; }
    public Guid Id { get; private set; }
    public Guid IntegrationId { get; private set; }
    public string Trigger { get; private set; } = null!;
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? FinishedAtUtc { get; private set; }
    public bool? Succeeded { get; private set; }
    public int InsertedCount { get; private set; }
    public int UpdatedCount { get; private set; }
    public int DeletedCount { get; private set; }
    public string? Error { get; private set; }
    public void Complete(int inserted, int updated, int deleted, DateTime now)
    { Succeeded = true; InsertedCount = inserted; UpdatedCount = updated; DeletedCount = deleted; FinishedAtUtc = now; }
    public void Fail(string error, DateTime now) { Succeeded = false; Error = error.Length > 500 ? error[..500] : error; FinishedAtUtc = now; }
}

public sealed class PluggySyncJob
{
    private PluggySyncJob() { }
    public PluggySyncJob(Guid integrationId, string trigger, string deduplicationKey, string? payload)
    { Id = Guid.NewGuid(); IntegrationId = integrationId; Trigger = trigger; DeduplicationKey = deduplicationKey; Payload = payload; Status = PluggyJobStatus.Pending; CreatedAtUtc = DateTime.UtcNow; AvailableAtUtc = CreatedAtUtc; }
    public Guid Id { get; private set; }
    public Guid IntegrationId { get; private set; }
    public string Trigger { get; private set; } = null!;
    public string DeduplicationKey { get; private set; } = null!;
    public string? Payload { get; private set; }
    public PluggyJobStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime AvailableAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string? LastError { get; private set; }
    public void Start(DateTime now) { Status = PluggyJobStatus.Processing; Attempts++; AvailableAtUtc = now.AddMinutes(15); }
    public void Complete(DateTime now) { Status = PluggyJobStatus.Completed; CompletedAtUtc = now; LastError = null; }
    public void Retry(string error, DateTime availableAt) { Status = Attempts >= 5 ? PluggyJobStatus.Failed : PluggyJobStatus.Pending; LastError = error.Length > 500 ? error[..500] : error; AvailableAtUtc = availableAt; }
}
