using KiaraFin.Api.Common.Security;
using KiaraFin.Api.Data;
using KiaraFin.Api.Features.Accounts;
using KiaraFin.Api.Features.Categories;
using KiaraFin.Api.Features.Finance;
using KiaraFin.Api.Features.Households;
using KiaraFin.Api.Features.Identity;
using KiaraFin.Api.Features.Transactions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KiaraFin.Api.Tests;

public sealed class FinanceFeatureTests
{
    [Fact]
    public void PostgreSql_model_generates_the_finance_schema()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=kiarafin_model_test;Username=test;Password=test")
            .Options);

        var script = db.Database.GenerateCreateScript();

        Assert.Contains("financial_accounts", script, StringComparison.Ordinal);
        Assert.Contains("account_movements", script, StringComparison.Ordinal);
        Assert.Contains("financial_transactions", script, StringComparison.Ordinal);
        Assert.Contains("numeric(18,2)", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Pending_and_settled_transactions_affect_balance_consistently()
    {
        await using var fixture = await FinanceFixture.CreateAsync();
        var service = fixture.Transactions;
        var today = new DateOnly(2026, 10, 11);

        await service.CreateAsync(fixture.Household.Id, new CreateTransactionRequest(
            "Receita recebida", 500m, FinancialTransactionType.Income, fixture.IncomeCategory.Id,
            fixture.Source.Id, today, today, today, null, fixture.User.Id, ExpenseScope.Shared), default);
        await service.CreateAsync(fixture.Household.Id, new CreateTransactionRequest(
            "Despesa paga", 125.50m, FinancialTransactionType.Expense, fixture.ExpenseCategory.Id,
            fixture.Source.Id, today, today, today, null, fixture.User.Id, ExpenseScope.Shared), default);
        await service.CreateAsync(fixture.Household.Id, new CreateTransactionRequest(
            "Despesa pendente", 900m, FinancialTransactionType.Expense, fixture.ExpenseCategory.Id,
            fixture.Source.Id, today, today, null, null, fixture.User.Id, ExpenseScope.Shared), default);

        var account = await fixture.Accounts.GetAsync(fixture.Household.Id, fixture.Source.Id, default);
        Assert.Equal(1374.50m, account.CurrentBalance);
        Assert.Equal(500m, account.Income);
        Assert.Equal(125.50m, account.Expenses);
    }

    [Fact]
    public async Task Canceling_a_settled_transaction_adds_one_compensating_movement()
    {
        await using var fixture = await FinanceFixture.CreateAsync();
        var today = new DateOnly(2026, 10, 11);
        var expense = await fixture.Transactions.CreateAsync(fixture.Household.Id, new CreateTransactionRequest(
            "Compra", 80m, FinancialTransactionType.Expense, fixture.ExpenseCategory.Id,
            fixture.Source.Id, today, today, today, null, fixture.User.Id, ExpenseScope.Individual), default);

        await fixture.Transactions.CancelAsync(fixture.Household.Id, expense.Id, default);
        await fixture.Transactions.CancelAsync(fixture.Household.Id, expense.Id, default);

        var account = await fixture.Accounts.GetAsync(fixture.Household.Id, fixture.Source.Id, default);
        Assert.Equal(1000m, account.CurrentBalance);
        Assert.Equal(2, await fixture.Db.AccountMovements.CountAsync(x => x.TransactionId == expense.Id));
    }

    [Fact]
    public async Task Transfer_is_atomic_double_entry_and_idempotent_by_client_request_id()
    {
        await using var fixture = await FinanceFixture.CreateAsync();
        var request = new CreateTransferRequest(
            "transfer-2026-10-11-001", "Reserva mensal", 300m,
            fixture.Source.Id, fixture.Destination.Id, new DateOnly(2026, 10, 11),
            fixture.User.Id, null);

        var first = await fixture.Transactions.TransferAsync(fixture.Household.Id, request, default);
        var retry = await fixture.Transactions.TransferAsync(fixture.Household.Id, request, default);

        Assert.Equal(first.Id, retry.Id);
        var movements = await fixture.Db.AccountMovements
            .Where(x => x.TransactionId == first.Id).OrderBy(x => x.Sequence).ToListAsync();
        Assert.Collection(movements,
            movement => Assert.Equal(-300m, movement.SignedAmount),
            movement => Assert.Equal(300m, movement.SignedAmount));
        Assert.Equal(700m, (await fixture.Accounts.GetAsync(fixture.Household.Id, fixture.Source.Id, default)).CurrentBalance);
        Assert.Equal(500m, (await fixture.Accounts.GetAsync(fixture.Household.Id, fixture.Destination.Id, default)).CurrentBalance);
    }

    [Fact]
    public async Task Default_category_seed_is_idempotent()
    {
        await using var fixture = await FinanceFixture.CreateAsync(seedDefaults: false);
        var seeder = new DefaultCategorySeeder(fixture.Db);

        await seeder.EnsureSeededAsync(fixture.Household.Id, default);
        await seeder.EnsureSeededAsync(fixture.Household.Id, default);
        await fixture.Db.SaveChangesAsync();
        await seeder.EnsureSeededAsync(fixture.Household.Id, default);
        await fixture.Db.SaveChangesAsync();

        Assert.Equal(13, await fixture.Db.Categories.CountAsync(
            x => x.HouseholdId == fixture.Household.Id && x.IsSystem));
    }

    private sealed class FinanceFixture : IAsyncDisposable
    {
        private FinanceFixture(AppDbContext db, User user, Household household,
            FinancialAccount source, FinancialAccount destination, Category incomeCategory,
            Category expenseCategory)
        {
            Db = db;
            User = user;
            Household = household;
            Source = source;
            Destination = destination;
            IncomeCategory = incomeCategory;
            ExpenseCategory = expenseCategory;
            var access = new HouseholdAccessService(db, new StubCurrentUser(user.Id));
            Transactions = new TransactionService(db, access);
            Accounts = new AccountService(db, access);
        }

        public AppDbContext Db { get; }
        public User User { get; }
        public Household Household { get; }
        public FinancialAccount Source { get; }
        public FinancialAccount Destination { get; }
        public Category IncomeCategory { get; }
        public Category ExpenseCategory { get; }
        public TransactionService Transactions { get; }
        public AccountService Accounts { get; }

        public static async Task<FinanceFixture> CreateAsync(bool seedDefaults = true)
        {
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"finance-{Guid.NewGuid():N}").Options);
            var user = new User("Finance user", "finance@example.test", "FINANCE@EXAMPLE.TEST", "hash");
            var household = new Household("Casa", user.Id);
            var membership = new HouseholdMembership(household.Id, user.Id, HouseholdRole.Owner);
            var source = new FinancialAccount(household.Id, "Conta", FinancialAccountType.Checking,
                1000m, new DateOnly(2026, 1, 1), user.Id);
            var destination = new FinancialAccount(household.Id, "Reserva", FinancialAccountType.Savings,
                200m, new DateOnly(2026, 1, 1), user.Id);
            var income = new Category(household.Id, "Test income", CategoryType.Income, "#000", "plus");
            var expense = new Category(household.Id, "Test expense", CategoryType.Expense, "#111", "minus");
            db.AddRange(user, household, membership, source, destination, income, expense);
            await db.SaveChangesAsync();
            if (seedDefaults)
                await db.SaveChangesAsync();
            return new FinanceFixture(db, user, household, source, destination, income, expense);
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class StubCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }
}
