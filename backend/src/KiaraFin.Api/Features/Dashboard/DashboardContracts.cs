using KiaraFin.Api.Features.Finance;
using KiaraFin.Api.Features.Forecasts;

namespace KiaraFin.Api.Features.Dashboard;

public sealed record DashboardIndicators(
    decimal AvailableBalance,
    decimal MonthIncome,
    decimal MonthExpenses,
    decimal ProjectedBalance,
    int UpcomingBills,
    decimal OpenInvoices,
    decimal CommittedInstallments);

public sealed record IncomeExpensePoint(string Period, decimal Income, decimal Expenses);
public sealed record CategoryExpensePoint(Guid CategoryId, string Name, string Color, string Icon, decimal Amount);
public sealed record BalancePoint(string Period, decimal Balance);
public sealed record ForecastPoint(string Period, decimal Income, decimal Expenses, decimal Invoices, decimal Net);
public sealed record UpcomingDueItem(Guid Id, string Kind, string Description, decimal Amount, DateOnly DueDate);
public sealed record LatestTransactionItem(
    Guid Id, string Description, decimal Amount, FinancialTransactionType Type,
    FinancialTransactionStatus Status, DateOnly CompetenceDate);
public sealed record DashboardInvoiceItem(
    Guid Id, Guid CreditCardId, string CardName, decimal Amount, DateOnly DueDate, InvoiceStatus Status);
public sealed record DashboardCardItem(
    Guid Id, string Name, string? Color, decimal? TotalLimit, decimal? UsedLimit, decimal? AvailableLimit);

public sealed record BalanceForecastSummary(
    decimal StartingBalance,
    decimal MonthlySalary,
    decimal MonthlyExtraIncome,
    decimal MonthlyFixedExpenses,
    decimal KnownCardInvoices,
    decimal ProjectedBalance);

public sealed record BalanceForecastMonth(
    string Period,
    decimal Salary,
    decimal ExtraIncome,
    decimal FixedExpenses,
    decimal CardInvoices,
    decimal Net,
    decimal ProjectedBalance);

public sealed record BalanceForecastEntry(
    Guid Id,
    string Name,
    ForecastEntryType Type,
    decimal Amount,
    int ExpectedDay);

public sealed record BalanceForecastAnalysis(
    BalanceForecastSummary Summary,
    IReadOnlyList<BalanceForecastMonth> Months,
    IReadOnlyList<BalanceForecastEntry> Entries);

public sealed record DashboardResponse(
    string Period,
    DashboardIndicators Indicators,
    IReadOnlyList<IncomeExpensePoint> IncomeVsExpenses,
    IReadOnlyList<CategoryExpensePoint> ExpensesByCategory,
    IReadOnlyList<BalancePoint> BalanceEvolution,
    IReadOnlyList<ForecastPoint> Forecast,
    IReadOnlyList<UpcomingDueItem> UpcomingDue,
    IReadOnlyList<LatestTransactionItem> LatestTransactions,
    IReadOnlyList<DashboardInvoiceItem> UpcomingInvoices,
    IReadOnlyList<DashboardCardItem> Cards,
    BalanceForecastAnalysis BalanceForecast);
