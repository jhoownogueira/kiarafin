namespace KiaraFin.Api.Features.Cards;

public sealed record CardItemResponse(
    Guid Id,
    string Description,
    decimal Amount,
    decimal Effect,
    CardTransactionKind Kind,
    DateOnly Date,
    DateOnly? PurchaseDate,
    bool IsPending,
    int? InstallmentNumber,
    int? TotalInstallments,
    string? Category,
    string? CurrencyCode,
    decimal? OriginalAmount,
    bool Projected);

public sealed record OpenInvoiceResponse(
    decimal Total,
    DateOnly? ClosingDate,
    DateOnly? DueDate,
    int ItemCount,
    IReadOnlyList<CardItemResponse>? Items);

public sealed record ClosedInvoiceResponse(
    Guid Id,
    DateOnly DueDate,
    DateOnly? ClosingDate,
    decimal TotalAmount,
    decimal? MinimumPayment,
    decimal PaidAmount,
    decimal Outstanding,
    bool IsPaid,
    decimal ItemsTotal,
    IReadOnlyList<CardItemResponse>? Items);

public sealed record CardSummaryResponse(
    Guid Id,
    string Name,
    string? Institution,
    string? Brand,
    string? Number,
    decimal? CreditLimit,
    decimal? AvailableLimit,
    decimal ReportedBalance,
    bool IsActive,
    DateTime? UpdatedAtUtc,
    OpenInvoiceResponse CurrentInvoice,
    decimal OutstandingClosedInvoices,
    ClosedInvoiceResponse? LastClosedInvoice);

public sealed record CardDetailResponse(
    CardSummaryResponse Card,
    OpenInvoiceResponse CurrentInvoice,
    IReadOnlyList<CardItemResponse> Upcoming,
    IReadOnlyList<ClosedInvoiceResponse> ClosedInvoices,
    IReadOnlyList<CardItemResponse> Payments);
