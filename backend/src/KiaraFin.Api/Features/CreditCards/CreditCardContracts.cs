using KiaraFin.Api.Features.Finance;
using KiaraFin.Api.Features.Installments;

namespace KiaraFin.Api.Features.CreditCards;

public sealed record CreateCreditCardRequest(
    string Name,
    string? Institution,
    string? Brand,
    string? Color,
    decimal TotalLimit,
    int ClosingDay,
    int DueDay,
    Guid ResponsibleUserId,
    Guid DefaultPaymentAccountId);

public sealed record UpdateCreditCardRequest(
    string Name,
    string? Institution,
    string? Brand,
    string? Color,
    decimal TotalLimit,
    int ClosingDay,
    int DueDay,
    Guid ResponsibleUserId,
    Guid DefaultPaymentAccountId,
    bool IsActive);

public sealed record CreditCardPurchaseRequest(
    Guid IdempotencyKey,
    string Description,
    decimal TotalAmount,
    int InstallmentCount,
    DateOnly PurchaseDate,
    DateOnly? FirstInstallmentDate,
    Guid CategoryId,
    Guid ResponsibleUserId,
    ExpenseScope Scope);

public sealed record PayInvoiceRequest(Guid AccountId, DateOnly PaidDate);

public sealed record CloseInvoiceRequest(DateOnly DueDate);

public sealed record UpdateCreditCardPurchaseRequest(
    string Description,
    decimal TotalAmount,
    Guid CategoryId,
    Guid ResponsibleUserId,
    ExpenseScope Scope);

public sealed record InvoiceSummaryResponse(
    Guid Id,
    DateOnly? ClosingDate,
    DateOnly DueDate,
    decimal TotalAmount,
    InvoiceStatus Status,
    DateOnly? PaidDate);

public sealed record InvoiceDetailResponse(
    Guid Id,
    Guid CreditCardId,
    DateOnly? CycleStartDate,
    DateOnly? ClosingDate,
    DateOnly DueDate,
    decimal TotalAmount,
    InvoiceStatus Status,
    DateOnly? PaidDate,
    Guid? PaymentTransactionId,
    IReadOnlyList<InvoicePurchaseResponse> Purchases);

public sealed record InvoicePurchaseResponse(
    Guid Id,
    Guid? InstallmentPlanId,
    Guid? CardSubscriptionId,
    string Description,
    decimal Amount,
    decimal TotalPurchaseAmount,
    DateOnly CompetenceDate,
    Guid CategoryId,
    Guid ResponsibleUserId,
    ExpenseScope Scope,
    int? InstallmentNumber,
    int? InstallmentCount,
    bool IsRefund);

public sealed record CreditCardResponse(
    Guid Id,
    string Name,
    string? Institution,
    string? Brand,
    string? Color,
    decimal? TotalLimit,
    decimal? UsedLimit,
    decimal? AvailableLimit,
    int? ClosingDay,
    int? DueDay,
    Guid ResponsibleUserId,
    Guid? DefaultPaymentAccountId,
    CreditCardStatus Status,
    IReadOnlyList<InvoiceSummaryResponse> Invoices,
    string? SourceProvider,
    string? ExternalId,
    bool InstitutionControlled);

public sealed record InvoicePaymentResponse(Guid InvoiceId, Guid PaymentTransactionId, InvoiceStatus Status);
