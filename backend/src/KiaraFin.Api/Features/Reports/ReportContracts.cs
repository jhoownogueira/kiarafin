using KiaraFin.Api.Features.Finance;

namespace KiaraFin.Api.Features.Reports;

public sealed class ReportFilter
{
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }
    public Guid? AccountId { get; init; }
    public Guid? CardId { get; init; }
    public Guid? CategoryId { get; init; }
    public Guid? ResponsibleUserId { get; init; }
    public FinancialTransactionStatus? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record MonthlyReportItem(int Year, int Month, decimal Income, decimal Expenses, decimal Net);
public sealed record CategoryReportItem(Guid CategoryId, string Name, string Color, string Icon, decimal Amount, decimal Percentage);
public sealed record ResponsibleReportItem(Guid UserId, string Name, decimal Income, decimal Expenses, decimal Net);
public sealed record ScopeReportItem(ExpenseScope Scope, decimal Amount, decimal Percentage);
