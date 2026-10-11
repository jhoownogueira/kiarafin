namespace KiaraFin.Api.Common;

public static class BusinessDate
{
    private static readonly TimeZoneInfo SaoPaulo =
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public static DateOnly Today() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, SaoPaulo));
}
