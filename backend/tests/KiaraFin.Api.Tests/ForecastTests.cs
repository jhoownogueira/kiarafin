using KiaraFin.Api.Common;
using KiaraFin.Api.Common.Security;
using KiaraFin.Api.Data;
using KiaraFin.Api.Features.Dashboard;
using KiaraFin.Api.Features.Finance;
using KiaraFin.Api.Features.Forecasts;
using KiaraFin.Api.Features.Households;
using KiaraFin.Api.Features.Identity;
using KiaraFin.Api.Features.Notifications;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KiaraFin.Api.Tests;

public sealed class ForecastTests
{
    [Fact]
    public async Task Forecast_crud_never_creates_financial_transactions()
    {
        await using var fixture = await ForecastFixture.CreateAsync();
        var created = await fixture.Forecasts.CreateAsync(
            fixture.Household.Id,
            new SaveForecastEntryRequest("Aluguel", ForecastEntryType.FixedExpense, 1500m, 10),
            default);

        var updated = await fixture.Forecasts.UpdateAsync(
            fixture.Household.Id, created.Id,
            new SaveForecastEntryRequest("Aluguel atualizado", ForecastEntryType.FixedExpense, 1600m, 12),
            default);

        Assert.Equal(1600m, updated.Amount);
        Assert.Equal(12, updated.ExpectedDay);
        Assert.Empty(await fixture.Db.FinancialTransactions.ToListAsync());

        await fixture.Forecasts.DeleteAsync(fixture.Household.Id, created.Id, default);
        Assert.Empty(await fixture.Forecasts.ListAsync(fixture.Household.Id, default));
        Assert.Empty(await fixture.Db.FinancialTransactions.ToListAsync());
    }

    [Fact]
    public async Task Dashboard_forecast_combines_monthly_assumptions_official_invoice_and_forming_card()
    {
        await using var fixture = await ForecastFixture.CreateAsync();
        var today = BusinessDate.Today();
        fixture.Db.ForecastEntries.AddRange(
            new ForecastEntry(fixture.Household.Id, "Salário", ForecastEntryType.Salary, 5000m, 5),
            new ForecastEntry(fixture.Household.Id, "Freelance", ForecastEntryType.ExtraIncome, 100m, 15),
            new ForecastEntry(fixture.Household.Id, "Aluguel", ForecastEntryType.FixedExpense, 1000m, 10));

        var card = new CreditCard(
            fixture.Household.Id, "Cartão", 10000m, 5, 12,
            fixture.User.Id, fixture.Account.Id);
        fixture.Db.CreditCards.Add(card);
        var invoice = Invoice.CreateInstitutionControlled(
            fixture.Household.Id, card.Id, "PLUGGY", "invoice-test",
            today.AddDays(-1), today.AddDays(1), 500m, null, DateTime.UtcNow);
        fixture.Db.Invoices.Add(invoice);
        var formingPurchase = FinancialTransaction.CreateInstitutionCreditCardEntry(
            fixture.Household.Id, "Compra em formação", 250m, fixture.ExpenseCategory.Id,
            card.Id, null, today, fixture.User.Id, settled: false, isRefund: false);
        formingPurchase.MarkInstitutionControlled("PLUGGY", "transaction-test");
        fixture.Db.FinancialTransactions.Add(formingPurchase);
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Dashboard.GetAsync(
            fixture.Household.Id, today.ToString("yyyy-MM"), default);

        Assert.Equal(6, result.BalanceForecast.Months.Count);
        Assert.Equal(5000m, result.BalanceForecast.Summary.MonthlySalary);
        Assert.Equal(100m, result.BalanceForecast.Summary.MonthlyExtraIncome);
        Assert.Equal(1000m, result.BalanceForecast.Summary.MonthlyFixedExpenses);
        Assert.Equal(750m, result.BalanceForecast.Summary.KnownCardInvoices);
        Assert.Equal(3750m, result.BalanceForecast.Summary.EstimatedCardInvoices);
        Assert.Equal(750m, result.BalanceForecast.Months[0].CardInvoices);
        Assert.Equal(0m, result.BalanceForecast.Months[0].EstimatedCardInvoices);
        Assert.Equal(750m, result.BalanceForecast.Months[1].EstimatedCardInvoices);
        Assert.Equal(20100m, result.BalanceForecast.Summary.ProjectedBalance);
    }

    [Fact]
    public async Task Changing_a_forecast_notifies_the_new_current_month_result()
    {
        await using var fixture = await ForecastFixture.CreateAsync();
        var access = new HouseholdAccessService(fixture.Db, new StubCurrentUser(fixture.User.Id));
        var notifications = new CapturingNotificationService();
        var service = new ForecastService(
            fixture.Db, access, new DashboardService(fixture.Db, access), notifications);

        await service.CreateAsync(
            fixture.Household.Id,
            new SaveForecastEntryRequest("Salário", ForecastEntryType.Salary, 5000m, 5),
            default);

        var notification = Assert.Single(notifications.Forecasts);
        Assert.Equal(BalanceForecastNotificationReason.ForecastEdited, notification.Reason);
        Assert.Equal(0m, notification.PreviousResult);
        Assert.Equal(5000m, notification.Result);
    }

    private sealed class ForecastFixture : IAsyncDisposable
    {
        private ForecastFixture(
            AppDbContext db, User user, Household household, FinancialAccount account,
            Category expenseCategory, ForecastService forecasts, DashboardService dashboard)
        {
            Db = db;
            User = user;
            Household = household;
            Account = account;
            ExpenseCategory = expenseCategory;
            Forecasts = forecasts;
            Dashboard = dashboard;
        }

        public AppDbContext Db { get; }
        public User User { get; }
        public Household Household { get; }
        public FinancialAccount Account { get; }
        public Category ExpenseCategory { get; }
        public ForecastService Forecasts { get; }
        public DashboardService Dashboard { get; }

        public static async Task<ForecastFixture> CreateAsync()
        {
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"forecast-{Guid.NewGuid():N}").Options);
            var user = new User("Forecast user", "forecast@example.test", "FORECAST@EXAMPLE.TEST", "hash");
            var household = new Household("Casa", user.Id);
            var membership = new HouseholdMembership(household.Id, user.Id, HouseholdRole.Owner);
            var today = BusinessDate.Today();
            var account = new FinancialAccount(
                household.Id, "Conta", FinancialAccountType.Checking, 1000m,
                today.AddMonths(-1), user.Id);
            var category = new Category(
                household.Id, "Compras", CategoryType.Expense, "#111", "receipt");
            db.AddRange(user, household, membership, account, category);
            await db.SaveChangesAsync();
            var access = new HouseholdAccessService(db, new StubCurrentUser(user.Id));
            return new ForecastFixture(
                db, user, household, account, category,
                new ForecastService(db, access), new DashboardService(db, access));
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class StubCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private sealed class CapturingNotificationService : IAccountNotificationService
    {
        public List<BalanceForecastNotification> Forecasts { get; } = [];

        public Task NotifyAsync(
            AccountUpdateNotification notification, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task NotifyForecastAsync(
            BalanceForecastNotification notification, CancellationToken cancellationToken)
        {
            Forecasts.Add(notification);
            return Task.CompletedTask;
        }
    }
}
