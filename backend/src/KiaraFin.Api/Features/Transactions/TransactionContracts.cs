using System.ComponentModel.DataAnnotations;
using KiaraFin.Api.Features.Finance;

namespace KiaraFin.Api.Features.Transactions;

public sealed record CreateTransactionRequest(
    [Required, StringLength(200, MinimumLength = 2)] string Description,
    decimal Amount,
    FinancialTransactionType Type,
    Guid CategoryId,
    Guid AccountId,
    DateOnly CompetenceDate,
    DateOnly DueDate,
    DateOnly? SettledDate,
    [StringLength(1000)] string? Notes,
    Guid ResponsibleUserId,
    ExpenseScope Scope);

public sealed record UpdateTransactionRequest(
    [Required, StringLength(200, MinimumLength = 2)] string Description,
    decimal Amount,
    Guid CategoryId,
    Guid AccountId,
    DateOnly CompetenceDate,
    DateOnly DueDate,
    [StringLength(1000)] string? Notes,
    Guid ResponsibleUserId,
    ExpenseScope Scope);

public sealed record SettleTransactionRequest(DateOnly SettledDate);

public sealed record CreateTransferRequest(
    [Required, StringLength(100)] string ClientRequestId,
    [Required, StringLength(200, MinimumLength = 2)] string Description,
    decimal Amount,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    DateOnly TransferDate,
    Guid ResponsibleUserId,
    [StringLength(1000)] string? Notes);

public sealed record TransactionResponse(
    Guid Id,
    string Description,
    decimal Amount,
    FinancialTransactionType Type,
    FinancialTransactionStatus Status,
    Guid? CategoryId,
    Guid? AccountId,
    Guid? DestinationAccountId,
    DateOnly CompetenceDate,
    DateOnly DueDate,
    DateOnly? SettledDate,
    string? Notes,
    Guid ResponsibleUserId,
    ExpenseScope Scope,
    Guid? RecurringTransactionId,
    Guid? CardSubscriptionId,
    string? ClientRequestId,
    DateTime CreatedAtUtc);
