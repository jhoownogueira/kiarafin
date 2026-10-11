using KiaraFin.Api.Features.Finance;

namespace KiaraFin.Api.Features.RecurringTransactions;

public sealed record CreateRecurringTransactionRequest(
    string Name,
    FinancialTransactionType Type,
    Guid CategoryId,
    decimal ExpectedAmount,
    int DueDay,
    Guid AccountId,
    DateOnly StartDate,
    DateOnly? EndDate,
    Guid ResponsibleUserId,
    ExpenseScope Scope);

public sealed record UpdateRecurringTransactionRequest(
    string Name,
    Guid CategoryId,
    decimal ExpectedAmount,
    int DueDay,
    Guid AccountId,
    DateOnly? EndDate,
    Guid ResponsibleUserId,
    ExpenseScope Scope);

public sealed record EndRecurringTransactionRequest(DateOnly EndDate);

public sealed record RecurringTransactionResponse(
    Guid Id,
    string Name,
    FinancialTransactionType Type,
    Guid CategoryId,
    decimal ExpectedAmount,
    int DueDay,
    Guid AccountId,
    DateOnly StartDate,
    DateOnly? EndDate,
    RecurringTransactionStatus Status,
    Guid ResponsibleUserId,
    ExpenseScope Scope);

public sealed record RecurrenceGenerationResponse(int Created, IReadOnlyList<Guid> TransactionIds);
