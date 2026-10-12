using KiaraFin.Api.Common.Exceptions;

namespace KiaraFin.Api.Features.Finance;

public sealed class FinancialAccount
{
    private FinancialAccount() { }

    public FinancialAccount(
        Guid householdId,
        string name,
        FinancialAccountType type,
        decimal initialBalance,
        DateOnly initialBalanceDate,
        Guid responsibleUserId,
        string? institution = null,
        string? color = null,
        string? icon = null)
    {
        FinanceGuard.NotEmpty(householdId, nameof(householdId));
        FinanceGuard.NotEmpty(responsibleUserId, nameof(responsibleUserId));

        Id = Guid.NewGuid();
        HouseholdId = householdId;
        Name = FinanceGuard.RequiredText(name, nameof(name));
        Type = type;
        Institution = FinanceGuard.OptionalText(institution);
        Color = FinanceGuard.OptionalText(color);
        Icon = FinanceGuard.OptionalText(icon);
        InitialBalance = FinanceGuard.Money(initialBalance, nameof(initialBalance), allowNegative: true);
        InitialBalanceDate = initialBalanceDate;
        ResponsibleUserId = responsibleUserId;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public string Name { get; private set; } = null!;
    public FinancialAccountType Type { get; private set; }
    public string? Institution { get; private set; }
    public string? Color { get; private set; }
    public string? Icon { get; private set; }
    public decimal InitialBalance { get; private set; }
    public DateOnly InitialBalanceDate { get; private set; }
    public Guid ResponsibleUserId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public void SetActive(bool isActive, DateTime utcNow)
    {
        IsActive = isActive;
        UpdatedAtUtc = FinanceGuard.Utc(utcNow, nameof(utcNow));
    }

    public void Update(
        string name,
        FinancialAccountType type,
        Guid responsibleUserId,
        string? institution,
        string? color,
        string? icon,
        DateTime utcNow)
    {
        FinanceGuard.NotEmpty(responsibleUserId, nameof(responsibleUserId));
        Name = FinanceGuard.RequiredText(name, nameof(name));
        Type = type;
        ResponsibleUserId = responsibleUserId;
        Institution = FinanceGuard.OptionalText(institution);
        Color = FinanceGuard.OptionalText(color);
        Icon = FinanceGuard.OptionalText(icon);
        UpdatedAtUtc = FinanceGuard.Utc(utcNow, nameof(utcNow));
    }
}

public sealed class Category
{
    private Category() { }

    public Category(
        Guid householdId,
        string name,
        CategoryType type,
        string color,
        string icon,
        bool isSystem = false)
    {
        FinanceGuard.NotEmpty(householdId, nameof(householdId));

        Id = Guid.NewGuid();
        HouseholdId = householdId;
        Name = FinanceGuard.RequiredText(name, nameof(name));
        Type = type;
        Color = FinanceGuard.RequiredText(color, nameof(color));
        Icon = FinanceGuard.RequiredText(icon, nameof(icon));
        IsSystem = isSystem;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public string Name { get; private set; } = null!;
    public CategoryType Type { get; private set; }
    public string Color { get; private set; } = null!;
    public string Icon { get; private set; } = null!;
    public bool IsSystem { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public void SetActive(bool isActive, DateTime utcNow)
    {
        IsActive = isActive;
        UpdatedAtUtc = FinanceGuard.Utc(utcNow, nameof(utcNow));
    }

    public void Update(string name, string color, string icon, DateTime utcNow)
    {
        Name = FinanceGuard.RequiredText(name, nameof(name));
        Color = FinanceGuard.RequiredText(color, nameof(color));
        Icon = FinanceGuard.RequiredText(icon, nameof(icon));
        UpdatedAtUtc = FinanceGuard.Utc(utcNow, nameof(utcNow));
    }
}

public sealed class FinancialTransaction
{
    private FinancialTransaction() { }

    public FinancialTransaction(
        Guid householdId,
        string description,
        decimal amount,
        FinancialTransactionType type,
        Guid categoryId,
        Guid accountId,
        DateOnly competenceDate,
        DateOnly dueDate,
        Guid responsibleUserId,
        ExpenseScope scope,
        string? notes = null)
    {
        if (type is FinancialTransactionType.Transfer or FinancialTransactionType.InvoicePayment)
            throw new ArgumentException("Use the dedicated factory for this transaction type.", nameof(type));

        FinanceGuard.NotEmpty(householdId, nameof(householdId));
        FinanceGuard.NotEmpty(categoryId, nameof(categoryId));
        FinanceGuard.NotEmpty(accountId, nameof(accountId));
        FinanceGuard.NotEmpty(responsibleUserId, nameof(responsibleUserId));

        Id = Guid.NewGuid();
        HouseholdId = householdId;
        Description = FinanceGuard.RequiredText(description, nameof(description));
        Amount = FinanceGuard.PositiveMoney(amount, nameof(amount));
        Type = type;
        CategoryId = categoryId;
        AccountId = accountId;
        CompetenceDate = competenceDate;
        DueDate = dueDate;
        ResponsibleUserId = responsibleUserId;
        Scope = scope;
        Notes = FinanceGuard.OptionalText(notes);
        Status = FinancialTransactionStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public string Description { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public FinancialTransactionType Type { get; private set; }
    public FinancialTransactionStatus Status { get; private set; }
    public Guid? CategoryId { get; private set; }
    public Guid? AccountId { get; private set; }
    public Guid? DestinationAccountId { get; private set; }
    public Guid? CreditCardId { get; private set; }
    public Guid? InvoiceId { get; private set; }
    public Guid? RecurringTransactionId { get; private set; }
    public Guid? InstallmentPlanId { get; private set; }
    public int? InstallmentNumber { get; private set; }
    public int? InstallmentCount { get; private set; }
    public DateOnly CompetenceDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public DateOnly? SettledDate { get; private set; }
    public string? Notes { get; private set; }
    public Guid ResponsibleUserId { get; private set; }
    public ExpenseScope Scope { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public DateTime? CanceledAtUtc { get; private set; }
    public string? ClientRequestId { get; private set; }

    public static FinancialTransaction CreateTransfer(
        Guid householdId,
        string description,
        decimal amount,
        Guid sourceAccountId,
        Guid destinationAccountId,
        DateOnly transferDate,
        Guid responsibleUserId,
        string clientRequestId,
        string? notes = null)
    {
        if (sourceAccountId == destinationAccountId)
            throw new ArgumentException("Source and destination accounts must be different.");

        FinanceGuard.NotEmpty(householdId, nameof(householdId));
        FinanceGuard.NotEmpty(sourceAccountId, nameof(sourceAccountId));
        FinanceGuard.NotEmpty(destinationAccountId, nameof(destinationAccountId));
        FinanceGuard.NotEmpty(responsibleUserId, nameof(responsibleUserId));

        return new FinancialTransaction
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdId,
            Description = FinanceGuard.RequiredText(description, nameof(description)),
            Amount = FinanceGuard.PositiveMoney(amount, nameof(amount)),
            Type = FinancialTransactionType.Transfer,
            Status = FinancialTransactionStatus.Paid,
            AccountId = sourceAccountId,
            DestinationAccountId = destinationAccountId,
            CompetenceDate = transferDate,
            DueDate = transferDate,
            SettledDate = transferDate,
            ResponsibleUserId = responsibleUserId,
            Scope = ExpenseScope.Shared,
            Notes = FinanceGuard.OptionalText(notes),
            ClientRequestId = FinanceGuard.RequiredText(clientRequestId, nameof(clientRequestId)),
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void UpdatePending(
        string description,
        decimal amount,
        Guid categoryId,
        Guid accountId,
        DateOnly competenceDate,
        DateOnly dueDate,
        Guid responsibleUserId,
        ExpenseScope scope,
        string? notes,
        DateTime utcNow)
    {
        if (Status != FinancialTransactionStatus.Pending || Type == FinancialTransactionType.Transfer)
            throw new ConflictException("Somente receitas ou despesas pendentes podem ser editadas.");
        FinanceGuard.NotEmpty(categoryId, nameof(categoryId));
        FinanceGuard.NotEmpty(accountId, nameof(accountId));
        FinanceGuard.NotEmpty(responsibleUserId, nameof(responsibleUserId));
        Description = FinanceGuard.RequiredText(description, nameof(description));
        Amount = FinanceGuard.PositiveMoney(amount, nameof(amount));
        CategoryId = categoryId;
        AccountId = accountId;
        CompetenceDate = competenceDate;
        DueDate = dueDate;
        ResponsibleUserId = responsibleUserId;
        Scope = scope;
        Notes = FinanceGuard.OptionalText(notes);
        UpdatedAtUtc = FinanceGuard.Utc(utcNow, nameof(utcNow));
    }

    public static FinancialTransaction CreateCreditCardPurchase(
        Guid householdId,
        string description,
        decimal amount,
        Guid categoryId,
        Guid creditCardId,
        Guid invoiceId,
        DateOnly purchaseDate,
        Guid responsibleUserId,
        ExpenseScope scope,
        string? notes = null,
        DateOnly? dueDate = null)
    {
        FinanceGuard.NotEmpty(householdId, nameof(householdId));
        FinanceGuard.NotEmpty(categoryId, nameof(categoryId));
        FinanceGuard.NotEmpty(creditCardId, nameof(creditCardId));
        FinanceGuard.NotEmpty(invoiceId, nameof(invoiceId));
        FinanceGuard.NotEmpty(responsibleUserId, nameof(responsibleUserId));

        return new FinancialTransaction
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdId,
            Description = FinanceGuard.RequiredText(description, nameof(description)),
            Amount = FinanceGuard.PositiveMoney(amount, nameof(amount)),
            Type = FinancialTransactionType.Expense,
            Status = FinancialTransactionStatus.Pending,
            CategoryId = categoryId,
            CreditCardId = creditCardId,
            InvoiceId = invoiceId,
            CompetenceDate = purchaseDate,
            DueDate = dueDate ?? purchaseDate,
            ResponsibleUserId = responsibleUserId,
            Scope = scope,
            Notes = FinanceGuard.OptionalText(notes),
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public static FinancialTransaction CreateInvoicePayment(
        Guid householdId,
        Guid accountId,
        Guid invoiceId,
        decimal amount,
        DateOnly paidDate,
        Guid responsibleUserId)
    {
        FinanceGuard.NotEmpty(householdId, nameof(householdId));
        FinanceGuard.NotEmpty(accountId, nameof(accountId));
        FinanceGuard.NotEmpty(invoiceId, nameof(invoiceId));
        FinanceGuard.NotEmpty(responsibleUserId, nameof(responsibleUserId));

        return new FinancialTransaction
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdId,
            Description = "Pagamento de fatura",
            Amount = FinanceGuard.PositiveMoney(amount, nameof(amount)),
            Type = FinancialTransactionType.InvoicePayment,
            Status = FinancialTransactionStatus.Paid,
            AccountId = accountId,
            InvoiceId = invoiceId,
            CompetenceDate = paidDate,
            DueDate = paidDate,
            SettledDate = paidDate,
            ResponsibleUserId = responsibleUserId,
            Scope = ExpenseScope.Shared,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void LinkCreditCardPurchase(Guid creditCardId, Guid invoiceId)
    {
        if (Type != FinancialTransactionType.Expense || Status != FinancialTransactionStatus.Pending)
            throw new ConflictException("Somente despesas pendentes podem ser vinculadas a uma fatura.");

        FinanceGuard.NotEmpty(creditCardId, nameof(creditCardId));
        FinanceGuard.NotEmpty(invoiceId, nameof(invoiceId));
        CreditCardId = creditCardId;
        InvoiceId = invoiceId;
        AccountId = null;
    }

    public void LinkRecurrence(Guid recurringTransactionId)
    {
        FinanceGuard.NotEmpty(recurringTransactionId, nameof(recurringTransactionId));
        RecurringTransactionId = recurringTransactionId;
    }

    public void LinkInstallment(Guid installmentPlanId, int number, int count)
    {
        FinanceGuard.NotEmpty(installmentPlanId, nameof(installmentPlanId));
        if (number < 1 || count < 1 || number > count)
            throw new ArgumentOutOfRangeException(nameof(number));
        InstallmentPlanId = installmentPlanId;
        InstallmentNumber = number;
        InstallmentCount = count;
    }

    public void UpdatePendingCreditCardPurchase(
        string description,
        decimal amount,
        Guid categoryId,
        Guid responsibleUserId,
        ExpenseScope scope,
        DateTime utcNow)
    {
        if (Status != FinancialTransactionStatus.Pending || CreditCardId is null || InvoiceId is null)
            throw new ConflictException("Somente compras pendentes do cartão podem ser editadas.");
        FinanceGuard.NotEmpty(categoryId, nameof(categoryId));
        FinanceGuard.NotEmpty(responsibleUserId, nameof(responsibleUserId));
        Description = FinanceGuard.RequiredText(description, nameof(description));
        Amount = FinanceGuard.PositiveMoney(amount, nameof(amount));
        CategoryId = categoryId;
        ResponsibleUserId = responsibleUserId;
        Scope = scope;
        UpdatedAtUtc = FinanceGuard.Utc(utcNow, nameof(utcNow));
    }

    public void UpdateInvoiceDueDate(DateOnly dueDate, DateTime utcNow)
    {
        if (Status != FinancialTransactionStatus.Pending || InvoiceId is null)
            throw new ConflictException("Somente compras pendentes podem acompanhar o vencimento da fatura.");
        DueDate = dueDate;
        UpdatedAtUtc = FinanceGuard.Utc(utcNow, nameof(utcNow));
    }

    public void Settle(DateOnly settledDate, DateTime utcNow)
    {
        if (Status == FinancialTransactionStatus.Canceled)
            throw new ConflictException("Um lançamento cancelado não pode ser liquidado.");
        if (Status is FinancialTransactionStatus.Paid or FinancialTransactionStatus.Received)
            throw new ConflictException("O lançamento já está liquidado.");
        if (Type is FinancialTransactionType.Transfer or FinancialTransactionType.InvoicePayment)
            throw new ConflictException("Transferências são liquidadas no momento da criação.");

        Status = Type == FinancialTransactionType.Income
            ? FinancialTransactionStatus.Received
            : FinancialTransactionStatus.Paid;
        SettledDate = settledDate;
        UpdatedAtUtc = FinanceGuard.Utc(utcNow, nameof(utcNow));
    }

    public void MarkOverdue(DateOnly today, DateTime utcNow)
    {
        if (Status == FinancialTransactionStatus.Pending && DueDate < today)
        {
            Status = FinancialTransactionStatus.Overdue;
            UpdatedAtUtc = FinanceGuard.Utc(utcNow, nameof(utcNow));
        }
    }

    public void Cancel(DateTime utcNow)
    {
        if (Status == FinancialTransactionStatus.Canceled)
            return;
        if (Type is FinancialTransactionType.Transfer or FinancialTransactionType.InvoicePayment)
            throw new ConflictException("Uma movimentação de caixa exige um estorno compensatório explícito.");

        var timestamp = FinanceGuard.Utc(utcNow, nameof(utcNow));
        Status = FinancialTransactionStatus.Canceled;
        CanceledAtUtc = timestamp;
        UpdatedAtUtc = timestamp;
    }
}

public sealed class AccountMovement
{
    private AccountMovement() { }

    public AccountMovement(
        Guid householdId,
        Guid accountId,
        Guid transactionId,
        decimal signedAmount,
        AccountMovementKind kind,
        DateOnly movementDate,
        int sequence)
    {
        FinanceGuard.NotEmpty(householdId, nameof(householdId));
        FinanceGuard.NotEmpty(accountId, nameof(accountId));
        FinanceGuard.NotEmpty(transactionId, nameof(transactionId));
        if (signedAmount == 0)
            throw new ArgumentOutOfRangeException(nameof(signedAmount));

        Id = Guid.NewGuid();
        HouseholdId = householdId;
        AccountId = accountId;
        TransactionId = transactionId;
        SignedAmount = FinanceGuard.Money(signedAmount, nameof(signedAmount), allowNegative: true);
        Kind = kind;
        MovementDate = movementDate;
        Sequence = sequence;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid AccountId { get; private set; }
    public Guid TransactionId { get; private set; }
    public decimal SignedAmount { get; private set; }
    public AccountMovementKind Kind { get; private set; }
    public DateOnly MovementDate { get; private set; }
    public int Sequence { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}

public sealed class RecurringTransaction
{
    private RecurringTransaction() { }

    public RecurringTransaction(
        Guid householdId,
        string name,
        FinancialTransactionType type,
        Guid categoryId,
        decimal expectedAmount,
        int dueDay,
        Guid accountId,
        DateOnly startDate,
        Guid responsibleUserId,
        ExpenseScope scope,
        DateOnly? endDate = null)
    {
        if (type is FinancialTransactionType.Transfer or FinancialTransactionType.InvoicePayment)
            throw new ArgumentException("Recurring transfers are not supported in the first version.", nameof(type));
        if (endDate < startDate)
            throw new ArgumentException("End date must not be earlier than start date.", nameof(endDate));

        FinanceGuard.NotEmpty(householdId, nameof(householdId));
        FinanceGuard.NotEmpty(categoryId, nameof(categoryId));
        FinanceGuard.NotEmpty(accountId, nameof(accountId));
        FinanceGuard.NotEmpty(responsibleUserId, nameof(responsibleUserId));
        FinanceGuard.DayOfMonth(dueDay, nameof(dueDay));

        Id = Guid.NewGuid();
        HouseholdId = householdId;
        Name = FinanceGuard.RequiredText(name, nameof(name));
        Type = type;
        CategoryId = categoryId;
        ExpectedAmount = FinanceGuard.PositiveMoney(expectedAmount, nameof(expectedAmount));
        DueDay = dueDay;
        AccountId = accountId;
        StartDate = startDate;
        EndDate = endDate;
        Frequency = RecurrenceFrequency.Monthly;
        Status = RecurringTransactionStatus.Active;
        ResponsibleUserId = responsibleUserId;
        Scope = scope;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public string Name { get; private set; } = null!;
    public FinancialTransactionType Type { get; private set; }
    public Guid CategoryId { get; private set; }
    public decimal ExpectedAmount { get; private set; }
    public int DueDay { get; private set; }
    public Guid AccountId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public RecurrenceFrequency Frequency { get; private set; }
    public RecurringTransactionStatus Status { get; private set; }
    public Guid ResponsibleUserId { get; private set; }
    public ExpenseScope Scope { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public void Pause(DateTime utcNow) => ChangeStatus(RecurringTransactionStatus.Paused, utcNow);
    public void Resume(DateTime utcNow) => ChangeStatus(RecurringTransactionStatus.Active, utcNow);

    public void End(DateOnly endDate, DateTime utcNow)
    {
        if (endDate < StartDate)
            throw new ArgumentOutOfRangeException(nameof(endDate));

        EndDate = endDate;
        ChangeStatus(RecurringTransactionStatus.Ended, utcNow);
    }

    private void ChangeStatus(RecurringTransactionStatus status, DateTime utcNow)
    {
        Status = status;
        UpdatedAtUtc = FinanceGuard.Utc(utcNow, nameof(utcNow));
    }

    public void Update(
        string name,
        decimal expectedAmount,
        int dueDay,
        Guid categoryId,
        Guid accountId,
        Guid responsibleUserId,
        ExpenseScope scope,
        DateOnly? endDate,
        DateTime utcNow)
    {
        if (endDate < StartDate)
            throw new ArgumentException("End date must not be earlier than start date.", nameof(endDate));
        FinanceGuard.NotEmpty(categoryId, nameof(categoryId));
        FinanceGuard.NotEmpty(accountId, nameof(accountId));
        FinanceGuard.NotEmpty(responsibleUserId, nameof(responsibleUserId));
        FinanceGuard.DayOfMonth(dueDay, nameof(dueDay));
        Name = FinanceGuard.RequiredText(name, nameof(name));
        ExpectedAmount = FinanceGuard.PositiveMoney(expectedAmount, nameof(expectedAmount));
        DueDay = dueDay;
        CategoryId = categoryId;
        AccountId = accountId;
        ResponsibleUserId = responsibleUserId;
        Scope = scope;
        EndDate = endDate;
        UpdatedAtUtc = FinanceGuard.Utc(utcNow, nameof(utcNow));
    }
}

public sealed class CreditCard
{
    private CreditCard() { }

    public CreditCard(
        Guid householdId,
        string name,
        decimal totalLimit,
        int closingDay,
        int dueDay,
        Guid responsibleUserId,
        Guid defaultPaymentAccountId,
        string? institution = null,
        string? brand = null,
        string? color = null)
    {
        FinanceGuard.NotEmpty(householdId, nameof(householdId));
        FinanceGuard.NotEmpty(responsibleUserId, nameof(responsibleUserId));
        FinanceGuard.NotEmpty(defaultPaymentAccountId, nameof(defaultPaymentAccountId));
        FinanceGuard.DayOfMonth(closingDay, nameof(closingDay));
        FinanceGuard.DayOfMonth(dueDay, nameof(dueDay));

        Id = Guid.NewGuid();
        HouseholdId = householdId;
        Name = FinanceGuard.RequiredText(name, nameof(name));
        Institution = FinanceGuard.OptionalText(institution);
        Brand = FinanceGuard.OptionalText(brand);
        Color = FinanceGuard.OptionalText(color);
        TotalLimit = FinanceGuard.PositiveMoney(totalLimit, nameof(totalLimit));
        ClosingDay = closingDay;
        DueDay = dueDay;
        ResponsibleUserId = responsibleUserId;
        DefaultPaymentAccountId = defaultPaymentAccountId;
        Status = CreditCardStatus.Active;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Institution { get; private set; }
    public string? Brand { get; private set; }
    public string? Color { get; private set; }
    public decimal TotalLimit { get; private set; }
    public int ClosingDay { get; private set; }
    public int DueDay { get; private set; }
    public Guid ResponsibleUserId { get; private set; }
    public Guid DefaultPaymentAccountId { get; private set; }
    public CreditCardStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public void Update(
        string name,
        decimal totalLimit,
        int closingDay,
        int dueDay,
        Guid responsibleUserId,
        Guid defaultPaymentAccountId,
        string? institution,
        string? brand,
        string? color,
        DateTime utcNow)
    {
        FinanceGuard.NotEmpty(responsibleUserId, nameof(responsibleUserId));
        FinanceGuard.NotEmpty(defaultPaymentAccountId, nameof(defaultPaymentAccountId));
        FinanceGuard.DayOfMonth(closingDay, nameof(closingDay));
        FinanceGuard.DayOfMonth(dueDay, nameof(dueDay));
        Name = FinanceGuard.RequiredText(name, nameof(name));
        Institution = FinanceGuard.OptionalText(institution);
        Brand = FinanceGuard.OptionalText(brand);
        Color = FinanceGuard.OptionalText(color);
        TotalLimit = FinanceGuard.PositiveMoney(totalLimit, nameof(totalLimit));
        ClosingDay = closingDay;
        DueDay = dueDay;
        ResponsibleUserId = responsibleUserId;
        DefaultPaymentAccountId = defaultPaymentAccountId;
        UpdatedAtUtc = FinanceGuard.Utc(utcNow, nameof(utcNow));
    }

    public void SetActive(bool active, DateTime utcNow)
    {
        Status = active ? CreditCardStatus.Active : CreditCardStatus.Inactive;
        UpdatedAtUtc = FinanceGuard.Utc(utcNow, nameof(utcNow));
    }
}

public sealed class Invoice
{
    private Invoice() { }

    public Invoice(
        Guid householdId,
        Guid creditCardId,
        DateOnly cycleStartDate,
        DateOnly closingDate,
        DateOnly dueDate)
    {
        if (closingDate < cycleStartDate)
            throw new ArgumentException("Closing date must not be earlier than cycle start date.", nameof(closingDate));
        if (dueDate <= closingDate)
            throw new ArgumentException("Due date must be later than closing date.", nameof(dueDate));

        FinanceGuard.NotEmpty(householdId, nameof(householdId));
        FinanceGuard.NotEmpty(creditCardId, nameof(creditCardId));

        Id = Guid.NewGuid();
        HouseholdId = householdId;
        CreditCardId = creditCardId;
        CycleStartDate = cycleStartDate;
        ClosingDate = closingDate;
        DueDate = dueDate;
        Status = InvoiceStatus.Upcoming;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid CreditCardId { get; private set; }
    public DateOnly CycleStartDate { get; private set; }
    public DateOnly ClosingDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public decimal TotalAmount { get; private set; }
    public InvoiceStatus Status { get; private set; }
    public Guid? PaymentTransactionId { get; private set; }
    public DateOnly? PaidDate { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public void ReplaceCalculatedTotal(decimal totalAmount, DateTime utcNow)
    {
        if (Status is InvoiceStatus.Paid or InvoiceStatus.Canceled)
            throw new ConflictException("Uma fatura finalizada não pode ser recalculada.");

        TotalAmount = FinanceGuard.Money(totalAmount, nameof(totalAmount));
        UpdatedAtUtc = FinanceGuard.Utc(utcNow, nameof(utcNow));
    }

    public void MarkOpen(DateTime utcNow) => ChangeStatus(InvoiceStatus.Open, utcNow);
    public void Close(DateOnly dueDate, DateTime utcNow)
    {
        if (Status == InvoiceStatus.Closed)
            return;
        if (dueDate <= ClosingDate)
            throw new ConflictException("O vencimento deve ser posterior ao fechamento da fatura.");
        if (TotalAmount <= 0)
            throw new ConflictException("Uma fatura sem compras não pode ser fechada.");
        DueDate = dueDate;
        ChangeStatus(InvoiceStatus.Closed, utcNow);
    }

    public void PayInFull(Guid paymentTransactionId, DateOnly paidDate, decimal amount, DateTime utcNow)
    {
        if (Status == InvoiceStatus.Paid || PaymentTransactionId is not null)
            throw new ConflictException("A fatura já está paga.");
        if (Status == InvoiceStatus.Canceled)
            throw new ConflictException("Uma fatura cancelada não pode ser paga.");
        if (Status != InvoiceStatus.Closed)
            throw new ConflictException("Feche a fatura antes de realizar o pagamento.");
        FinanceGuard.NotEmpty(paymentTransactionId, nameof(paymentTransactionId));

        if (TotalAmount <= 0)
            throw new ConflictException("Uma fatura sem compras não pode ser paga.");

        var payment = FinanceGuard.PositiveMoney(amount, nameof(amount));
        if (payment != TotalAmount)
            throw new ConflictException("Pagamentos parciais de fatura não são suportados.");

        PaymentTransactionId = paymentTransactionId;
        PaidDate = paidDate;
        Status = InvoiceStatus.Paid;
        UpdatedAtUtc = FinanceGuard.Utc(utcNow, nameof(utcNow));
    }

    private void ChangeStatus(InvoiceStatus status, DateTime utcNow)
    {
        if (Status is InvoiceStatus.Paid or InvoiceStatus.Canceled)
            throw new ConflictException("Uma fatura finalizada não pode mudar de status.");

        Status = status;
        UpdatedAtUtc = FinanceGuard.Utc(utcNow, nameof(utcNow));
    }
}

public sealed class InstallmentPlan
{
    private InstallmentPlan() { }

    public InstallmentPlan(
        Guid householdId,
        string description,
        decimal totalAmount,
        int installmentCount,
        DateOnly purchaseDate,
        DateOnly firstInstallmentDate,
        Guid categoryId,
        InstallmentSourceType sourceType,
        Guid sourceId,
        Guid responsibleUserId,
        ExpenseScope scope,
        Guid? id = null)
    {
        if (installmentCount < 1)
            throw new ArgumentOutOfRangeException(nameof(installmentCount), "An installment plan must contain at least one installment.");
        if (firstInstallmentDate < purchaseDate)
            throw new ArgumentException("First installment date must not be earlier than purchase date.", nameof(firstInstallmentDate));

        FinanceGuard.NotEmpty(householdId, nameof(householdId));
        FinanceGuard.NotEmpty(categoryId, nameof(categoryId));
        FinanceGuard.NotEmpty(sourceId, nameof(sourceId));
        FinanceGuard.NotEmpty(responsibleUserId, nameof(responsibleUserId));

        if (id == Guid.Empty)
            throw new ArgumentException("Identifier must not be empty.", nameof(id));

        Id = id ?? Guid.NewGuid();
        HouseholdId = householdId;
        Description = FinanceGuard.RequiredText(description, nameof(description));
        TotalAmount = FinanceGuard.PositiveMoney(totalAmount, nameof(totalAmount));
        InstallmentCount = installmentCount;
        PurchaseDate = purchaseDate;
        FirstInstallmentDate = firstInstallmentDate;
        CategoryId = categoryId;
        SourceType = sourceType;
        SourceId = sourceId;
        ResponsibleUserId = responsibleUserId;
        Scope = scope;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public string Description { get; private set; } = null!;
    public decimal TotalAmount { get; private set; }
    public int InstallmentCount { get; private set; }
    public DateOnly PurchaseDate { get; private set; }
    public DateOnly FirstInstallmentDate { get; private set; }
    public Guid CategoryId { get; private set; }
    public InstallmentSourceType SourceType { get; private set; }
    public Guid SourceId { get; private set; }
    public Guid ResponsibleUserId { get; private set; }
    public ExpenseScope Scope { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public void UpdatePendingCardPurchase(
        string description,
        decimal totalAmount,
        Guid categoryId,
        Guid responsibleUserId,
        ExpenseScope scope,
        DateTime utcNow)
    {
        if (SourceType != InstallmentSourceType.CreditCard)
            throw new ConflictException("Somente compras de cartão podem ser alteradas por esta operação.");
        FinanceGuard.NotEmpty(categoryId, nameof(categoryId));
        FinanceGuard.NotEmpty(responsibleUserId, nameof(responsibleUserId));
        Description = FinanceGuard.RequiredText(description, nameof(description));
        TotalAmount = FinanceGuard.PositiveMoney(totalAmount, nameof(totalAmount));
        CategoryId = categoryId;
        ResponsibleUserId = responsibleUserId;
        Scope = scope;
        UpdatedAtUtc = FinanceGuard.Utc(utcNow, nameof(utcNow));
    }
}

internal static class FinanceGuard
{
    public static void NotEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Identifier must not be empty.", parameterName);
    }

    public static string RequiredText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value is required.", parameterName);
        return value.Trim();
    }

    public static string? OptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static decimal PositiveMoney(decimal value, string parameterName)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(parameterName, "Value must be positive.");
        return Money(value, parameterName);
    }

    public static decimal Money(decimal value, string parameterName, bool allowNegative = false)
    {
        if (!allowNegative && value < 0)
            throw new ArgumentOutOfRangeException(parameterName, "Value must not be negative.");
        if (decimal.Round(value, 2, MidpointRounding.ToEven) != value)
            throw new ArgumentException("Money must have at most two decimal places.", parameterName);
        return value;
    }

    public static int DayOfMonth(int value, string parameterName)
    {
        if (value is < 1 or > 31)
            throw new ArgumentOutOfRangeException(parameterName, "Day must be between 1 and 31.");
        return value;
    }

    public static DateTime Utc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Timestamp must be UTC.", parameterName);
        return value;
    }
}
