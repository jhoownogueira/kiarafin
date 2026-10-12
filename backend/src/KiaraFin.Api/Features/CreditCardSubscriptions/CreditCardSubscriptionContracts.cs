using KiaraFin.Api.Features.Finance;

namespace KiaraFin.Api.Features.CreditCardSubscriptions;

public sealed record CreateCreditCardSubscriptionRequest(
    string Name,
    decimal Amount,
    int ChargeDay,
    DateOnly StartDate,
    DateOnly? EndDate,
    Guid CategoryId,
    Guid ResponsibleUserId,
    ExpenseScope Scope);

public sealed record UpdateCreditCardSubscriptionRequest(
    string Name,
    decimal Amount,
    int ChargeDay,
    DateOnly? EndDate,
    Guid CategoryId,
    Guid ResponsibleUserId,
    ExpenseScope Scope);

public sealed record EndCreditCardSubscriptionRequest(DateOnly EndDate);

public sealed record CreditCardSubscriptionResponse(
    Guid Id,
    Guid CreditCardId,
    string Name,
    decimal Amount,
    int ChargeDay,
    DateOnly StartDate,
    DateOnly? EndDate,
    Guid CategoryId,
    Guid ResponsibleUserId,
    ExpenseScope Scope,
    RecurringTransactionStatus Status);

public sealed record CreditCardSubscriptionGenerationResponse(int CreatedCount);
