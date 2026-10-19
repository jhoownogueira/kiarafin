using System.ComponentModel.DataAnnotations;
using KiaraFin.Api.Features.Finance;

namespace KiaraFin.Api.Features.Accounts;

public sealed record CreateAccountRequest(
    [Required, StringLength(120, MinimumLength = 2)] string Name,
    FinancialAccountType Type,
    [StringLength(120)] string? Institution,
    [StringLength(30)] string? Color,
    [StringLength(60)] string? Icon,
    decimal InitialBalance,
    DateOnly InitialBalanceDate,
    Guid ResponsibleUserId);

public sealed record UpdateAccountRequest(
    [Required, StringLength(120, MinimumLength = 2)] string Name,
    FinancialAccountType Type,
    [StringLength(120)] string? Institution,
    [StringLength(30)] string? Color,
    [StringLength(60)] string? Icon,
    Guid ResponsibleUserId);

public sealed record AccountResponse(
    Guid Id,
    string Name,
    FinancialAccountType Type,
    string? Institution,
    string? Color,
    string? Icon,
    decimal InitialBalance,
    DateOnly InitialBalanceDate,
    decimal CurrentBalance,
    decimal Income,
    decimal Expenses,
    Guid ResponsibleUserId,
    bool IsActive,
    string? SourceProvider,
    string? ExternalId,
    bool InstitutionControlled);

public sealed record AccountMovementResponse(
    Guid Id,
    Guid TransactionId,
    string Description,
    decimal Amount,
    AccountMovementKind Kind,
    DateOnly Date,
    DateTime CreatedAtUtc);
