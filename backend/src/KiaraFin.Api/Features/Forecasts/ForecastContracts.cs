namespace KiaraFin.Api.Features.Forecasts;

public sealed record SaveForecastEntryRequest(
    string Name,
    ForecastEntryType Type,
    decimal Amount,
    int ExpectedDay);

public sealed record ForecastEntryResponse(
    Guid Id,
    string Name,
    ForecastEntryType Type,
    decimal Amount,
    int ExpectedDay,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
