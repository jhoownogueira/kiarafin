using KiaraFin.Api.Features.Finance;

namespace KiaraFin.Api.Features.Installments;

public sealed record CreateInstallmentPlanRequest(
    Guid IdempotencyKey,
    string Description,
    decimal TotalAmount,
    int InstallmentCount,
    DateOnly PurchaseDate,
    DateOnly FirstInstallmentDate,
    Guid CategoryId,
    InstallmentSourceType SourceType,
    Guid SourceId,
    Guid ResponsibleUserId,
    ExpenseScope Scope);

public sealed record InstallmentItemResponse(
    Guid TransactionId,
    int Number,
    int Count,
    decimal Amount,
    DateOnly DueDate,
    Guid? InvoiceId,
    FinancialTransactionStatus Status);

public sealed record InstallmentPlanResponse(
    Guid Id,
    string Description,
    decimal TotalAmount,
    int InstallmentCount,
    decimal RemainingAmount,
    DateOnly PurchaseDate,
    InstallmentSourceType SourceType,
    Guid SourceId,
    IReadOnlyList<InstallmentItemResponse> Installments);
