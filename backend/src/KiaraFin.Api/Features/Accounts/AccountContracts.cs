using System.ComponentModel.DataAnnotations;
using KiaraFin.Api.Features.Finance;

namespace KiaraFin.Api.Features.Accounts;

public sealed record SaveAccountRequest(
    [Required, StringLength(120, MinimumLength = 2)] string Name,
    FinancialAccountType Type,
    [StringLength(120)] string? Institution,
    [StringLength(30)] string? Color,
    [StringLength(60)] string? Icon,
    decimal Balance);

public sealed record AccountResponse(
    Guid Id,
    string Name,
    FinancialAccountType Type,
    string? Institution,
    string? Color,
    string? Icon,
    decimal CurrentBalance,
    bool IsActive,
    string? SourceProvider,
    bool InstitutionControlled,
    DateTime? UpdatedAtUtc);

public sealed record AccountTransactionResponse(
    Guid Id,
    string Description,
    decimal Amount,
    DateOnly Date,
    bool IsPending,
    string? Category);
