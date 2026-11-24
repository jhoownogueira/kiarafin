using System.Text.Json;
using KiaraFin.Api.Common;
using KiaraFin.Api.Common.Security;
using KiaraFin.Api.Data;
using KiaraFin.Api.Features.Cards;
using KiaraFin.Api.Features.Forecasts;
using KiaraFin.Api.Features.Households;
using KiaraFin.Api.Features.Identity;
using KiaraFin.Api.Features.Pluggy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KiaraFin.Api.Tests;

public sealed class CardInvoiceCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 11, 24);

    [Fact]
    public void Open_invoice_sums_purchases_minus_credits_and_ignores_payments()
    {
        var card = Card();
        var invoices = CardInvoiceCalculator.Calculate(card, [], [
            Tx("Mercado", 100, CardTransactionKind.Purchase, Today.AddDays(-3)),
            Tx("Restaurante", 50.5m, CardTransactionKind.Purchase, Today.AddDays(-1)),
            Tx("Estorno restaurante", 20, CardTransactionKind.Credit, Today),
            Tx("Pagamento recebido", 900, CardTransactionKind.Payment, Today.AddDays(-5)),
        ], Today);

        Assert.Equal(130.5m, invoices.Current.Total);
        Assert.Equal(3, invoices.Current.Lines.Count);
        Assert.Single(invoices.Payments);
    }

    [Fact]
    public void Items_of_a_closed_bill_are_not_in_the_open_invoice()
    {
        var card = Card();
        var bill = Bill(card, "bill-oct", Today.AddDays(-10), Today.AddDays(-20), 300);
        var invoices = CardInvoiceCalculator.Calculate(card, [bill], [
            Tx("Antiga", 300, CardTransactionKind.Purchase, Today.AddDays(-25), billId: "bill-oct"),
            Tx("Nova", 40, CardTransactionKind.Purchase, Today.AddDays(-2)),
        ], Today);

        Assert.Equal(40, invoices.Current.Total);
        Assert.Equal(300, invoices.Closed.Single().Bill.TotalAmount);
        Assert.Single(invoices.Closed.Single().Lines);
    }

    [Fact]
    public void Unknown_bill_id_is_treated_as_open_invoice()
    {
        var invoices = CardInvoiceCalculator.Calculate(Card(), [], [
            Tx("Compra", 75, CardTransactionKind.Purchase, Today.AddDays(-1), billId: "open-bill"),
        ], Today);

        Assert.Equal(75, invoices.Current.Total);
    }

    [Fact]
    public void Future_installments_are_moved_out_of_the_current_invoice()
    {
        var invoices = CardInvoiceCalculator.Calculate(Card(), [], [
            Tx("TV PARC 03/10", 200, CardTransactionKind.Purchase, Today.AddDays(-4), installment: (3, 10)),
            Tx("TV PARC 04/10", 200, CardTransactionKind.Purchase, Today.AddDays(-4), installment: (4, 10)),
            Tx("TV PARC 05/10", 200, CardTransactionKind.Purchase, Today.AddDays(-4), installment: (5, 10)),
            Tx("Viagem", 120, CardTransactionKind.Purchase, Today.AddMonths(1)),
        ], Today);

        Assert.Equal(200, invoices.Current.Total);
        // 04/10 e 05/10 publicadas pelo banco, a viagem com data futura e 06..10 projetadas.
        Assert.Equal(8, invoices.Upcoming.Count);
        Assert.Equal(5, invoices.Upcoming.Count(x => x.Projected));
    }

    [Fact]
    public void Closed_bill_is_paid_when_a_payment_covers_it()
    {
        var card = Card();
        var bill = Bill(card, "bill", Today.AddDays(5), Today.AddDays(-5), 500);
        var unpaid = CardInvoiceCalculator.Calculate(card, [bill], [], Today);
        Assert.False(unpaid.Closed.Single().IsPaid);
        Assert.Equal(500, unpaid.OutstandingClosedDueFrom(Today));

        var paid = CardInvoiceCalculator.Calculate(card, [bill], [
            Tx("Pagamento de fatura", 500, CardTransactionKind.Payment, Today.AddDays(-1)),
        ], Today);
        Assert.True(paid.Closed.Single().IsPaid);
        Assert.Equal(0, paid.OutstandingClosedDueFrom(Today));
    }

    [Fact]
    public void Next_installment_of_the_last_closed_bill_is_projected_into_the_current_invoice()
    {
        // Formato real do Open Finance: cada parcela só aparece depois de faturada.
        var card = Card();
        var oct = Bill(card, "oct", new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 5), 8910.85m);
        var nov = Bill(card, "nov", new DateOnly(2026, 11, 12), new DateOnly(2026, 11, 5), 10675.70m, paid: 8908.52m);
        var aimee = new DateOnly(2026, 9, 17);
        var amora = new DateOnly(2026, 7, 18);
        var ideal = new DateOnly(2026, 1, 15);
        var invoices = CardInvoiceCalculator.Calculate(card, [oct, nov], [
            Tx("AIMEE", 129.90m, CardTransactionKind.Purchase, new DateOnly(2026, 9, 17), "oct", (1, 3), aimee),
            Tx("AIMEE", 129.90m, CardTransactionKind.Purchase, new DateOnly(2026, 10, 5), "nov", (2, 3), aimee),
            Tx("AMORA SHOES", 64.77m, CardTransactionKind.Purchase, new DateOnly(2026, 9, 4), "oct", (3, 6), amora),
            Tx("AMORA SHOES", 64.77m, CardTransactionKind.Purchase, new DateOnly(2026, 10, 5), "nov", (4, 6), amora),
            Tx("A IDEAL", 93.84m, CardTransactionKind.Purchase, new DateOnly(2026, 4, 6), null, (4, 4), ideal),
            Tx("AFTERCLICK", 55.20m, CardTransactionKind.Purchase, new DateOnly(2026, 11, 7), null, (1, 3), new DateOnly(2026, 11, 7)),
            Tx("LOJA ANTIGA", 50m, CardTransactionKind.Purchase, new DateOnly(2026, 9, 20), "oct", (1, 2), new DateOnly(2026, 9, 20)),
        ], Today);

        // Fatura atual: AIMEE 3/3 + AMORA 5/6 (projetadas) + AFTERCLICK 1/3 + A IDEAL 4/4 (sem fatura vinculada).
        Assert.Equal(129.90m + 64.77m + 55.20m + 93.84m, invoices.Current.Total);
        Assert.Equal(2, invoices.Current.Lines.Count(x => x.Projected));
        Assert.Equal(new DateOnly(2026, 12, 12), invoices.Current.DueDate);
        // Próximas: AMORA 6/6 + AFTERCLICK 2/3 e 3/3. LOJA ANTIGA 2/2 deveria estar em nov: não é projetada.
        Assert.Equal(3, invoices.Upcoming.Count);
        Assert.DoesNotContain(invoices.Upcoming, x => x.Transaction.Description == "LOJA ANTIGA");
        Assert.Contains(invoices.Upcoming, x => x.Transaction.Description == "AMORA SHOES" &&
            x.Transaction.InstallmentNumber == 6 && x.Transaction.Date == new DateOnly(2027, 1, 12));
    }

    [Fact]
    public void Projection_is_replaced_without_duplication_when_the_real_installment_arrives()
    {
        var card = Card();
        var nov = Bill(card, "nov", new DateOnly(2026, 11, 12), new DateOnly(2026, 11, 5), 1000);
        var purchase = new DateOnly(2026, 9, 11);
        var second = Tx("AMAZONMKTPLC*CGCENTERG", 79.80m, CardTransactionKind.Purchase, new DateOnly(2026, 10, 5), "nov", (2, 5), purchase);

        var before = CardInvoiceCalculator.Calculate(card, [nov], [second], Today);
        Assert.Equal(79.80m, before.Current.Total);
        Assert.True(before.Current.Lines.Single().Projected);
        Assert.Equal(2, before.Upcoming.Count);

        // O Open Finance publica a 3/5 real na fatura aberta.
        var third = Tx("AMAZONMKTPLC*CGCENTERG", 79.80m, CardTransactionKind.Purchase, new DateOnly(2026, 11, 5), null, (3, 5), purchase);
        var after = CardInvoiceCalculator.Calculate(card, [nov], [second, third], Today);
        Assert.Equal(79.80m, after.Current.Total);
        Assert.False(after.Current.Lines.Single().Projected);
        Assert.Equal([4, 5], after.Upcoming.Select(x => x.Transaction.InstallmentNumber!.Value));
    }

    [Fact]
    public void Payments_listed_in_a_bill_settle_the_previous_bill()
    {
        var card = Card();
        var oct = Bill(card, "oct", new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 5), 8910.85m, paid: 9124.04m);
        var nov = Bill(card, "nov", new DateOnly(2026, 11, 12), new DateOnly(2026, 11, 5), 10675.70m, paid: 8908.52m);
        var invoices = CardInvoiceCalculator.Calculate(card, [oct, nov], [], Today);

        Assert.True(invoices.Closed.Single(x => x.Bill.ExternalId == "oct").IsPaid);
        // A fatura de novembro já venceu: não entra mais como fatura fechada a pagar.
        Assert.Equal(0, invoices.OutstandingClosedDueFrom(Today));
    }

    internal static CreditCard Card()
    {
        var card = new CreditCard(Guid.NewGuid(), "card-1");
        card.Apply("Cartão", "Banco", "VISA", "1234", 5000, 4000, 1000, null, null, null, true, DateTime.UtcNow);
        return card;
    }

    private static CardBill Bill(CreditCard card, string id, DateOnly due, DateOnly closing, decimal total, decimal paid = 0)
    {
        var bill = new CardBill(card.Id, id);
        bill.Apply(due, closing, total, null, paid, null, DateTime.UtcNow);
        return bill;
    }

    private static CardTransaction Tx(string description, decimal amount, CardTransactionKind kind, DateOnly date,
        string? billId = null, (int Number, int Total)? installment = null, DateOnly? purchaseDate = null)
    {
        var tx = new CardTransaction(Guid.NewGuid(), Guid.NewGuid().ToString());
        tx.Apply(description, amount, kind, date, purchaseDate, false, billId, installment?.Number, installment?.Total,
            null, null, null, DateTime.UtcNow);
        return tx;
    }
}

public sealed class PluggyNormalizerTests
{
    [Fact]
    public void Positive_card_amount_is_a_purchase_even_when_type_says_credit()
    {
        var tx = new PluggyTransaction("t", "a", "Mercado", 159.2m, new DateOnly(2026, 11, 1), Type: "CREDIT");
        Assert.Equal(CardTransactionKind.Purchase, PluggyNormalizer.ClassifyCardTransaction(tx, []));
    }

    [Theory]
    [InlineData("Pagamento recebido", CardTransactionKind.Payment)]
    [InlineData("PAGTO DEBITO AUTOMATICO", CardTransactionKind.Payment)]
    [InlineData("Estorno Netflix", CardTransactionKind.Credit)]
    [InlineData("Crédito promocional", CardTransactionKind.Credit)]
    public void Negative_card_amount_is_payment_or_credit(string description, CardTransactionKind expected)
    {
        var tx = new PluggyTransaction("t", "a", description, -50, new DateOnly(2026, 11, 1));
        Assert.Equal(expected, PluggyNormalizer.ClassifyCardTransaction(tx, []));
    }

    [Fact]
    public void Credit_matching_a_bill_total_is_its_payment()
    {
        var tx = new PluggyTransaction("t", "a", "Recebido", -1234.56m, new DateOnly(2026, 11, 1));
        Assert.Equal(CardTransactionKind.Payment, PluggyNormalizer.ClassifyCardTransaction(tx, [1234.56m]));
    }
}

public sealed class PluggyParsingTests
{
    [Theory]
    [InlineData("2026-11-20T00:00:00.000Z", 20)]
    [InlineData("2026-11-20T01:30:00.000Z", 19)]
    [InlineData("2026-11-20T03:00:00.000Z", 20)]
    [InlineData("2026-11-20T00:00:00", 20)]
    [InlineData("2026-11-20", 20)]
    public void Dates_are_interpreted_in_brazilian_time(string value, int expectedDay)
    {
        Assert.Equal(new DateOnly(2026, 11, expectedDay), PluggyClient.ParseLocalDate(value));
    }

    [Fact]
    public void International_purchase_uses_amount_in_account_currency()
    {
        using var json = JsonDocument.Parse("""
            {"id":"t1","date":"2026-11-10T00:00:00.000Z","description":"AMAZON US","amount":10,
             "currencyCode":"USD","amountInAccountCurrency":57.31,"status":"PENDING",
             "creditCardMetadata":{"installmentNumber":1,"totalInstallments":1,"billId":null,"purchaseDate":"2026-11-09T00:00:00.000Z"}}
            """);
        var tx = PluggyClient.ParseTransaction(json.RootElement, "card");

        Assert.Equal(57.31m, tx.Amount);
        Assert.Equal("USD", tx.CurrencyCode);
        Assert.Equal(10, tx.OriginalAmount);
        Assert.True(tx.IsPending);
        Assert.Null(tx.BillId);
        Assert.Equal(new DateOnly(2026, 11, 9), tx.PurchaseDate);
    }

    [Fact]
    public void Explicit_null_numbers_are_treated_as_missing()
    {
        using var transaction = JsonDocument.Parse("""
            {"id":"t1","date":"2026-11-10T00:00:00.000Z","description":"Mercado","amount":42.5,
             "currencyCode":"BRL","amountInAccountCurrency":null,"category":null,
             "creditCardMetadata":{"installmentNumber":null,"totalInstallments":null,"billId":null}}
            """);
        var tx = PluggyClient.ParseTransaction(transaction.RootElement, "card");
        Assert.Equal(42.5m, tx.Amount);
        Assert.Null(tx.InstallmentNumber);
        Assert.Null(tx.CurrencyCode);

        using var account = JsonDocument.Parse("""
            {"id":"c1","type":"CREDIT","name":"Cartão","balance":10,"number":null,
             "creditData":{"creditLimit":null,"availableCreditLimit":null,"minimumPayment":null,"balanceCloseDate":null}}
            """);
        var parsed = PluggyClient.ParseAccount(account.RootElement);
        Assert.Null(parsed.CreditLimit);
        Assert.Null(parsed.MinimumPayment);

        using var bill = JsonDocument.Parse("""
            {"id":"b1","dueDate":"2026-11-10","totalAmount":800,"minimumPaymentAmount":null,
             "payments":[{"amount":null,"paymentDate":null}]}
            """);
        Assert.Equal(0, PluggyClient.ParseBill(bill.RootElement, "card").PaidAmount);
    }

    [Fact]
    public void Bill_payments_are_summed()
    {
        using var json = JsonDocument.Parse("""
            {"id":"b1","dueDate":"2026-11-10T00:00:00","billClosingDate":"2026-11-03T00:00:00","totalAmount":800,
             "minimumPaymentAmount":120,"payments":[{"amount":500,"paymentDate":"2026-11-05"},{"amount":300,"paymentDate":"2026-11-09"}]}
            """);
        var bill = PluggyClient.ParseBill(json.RootElement, "card");

        Assert.Equal(800, bill.PaidAmount);
        Assert.Equal(new DateOnly(2026, 11, 9), bill.LastPaymentDate);
        Assert.Equal(new DateOnly(2026, 11, 3), bill.ClosingDate);
    }
}

public sealed class PluggySyncAndForecastTests
{
    [Fact]
    public async Task Sync_mirrors_the_source_and_forecast_uses_the_current_invoice()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"sync-{Guid.NewGuid():N}").Options);
        var user = new User("Dono", "dono@example.test", "DONO@EXAMPLE.TEST", "hash");
        var household = new Household("Casa", user.Id);
        db.AddRange(user, household, new HouseholdMembership(household.Id, user.Id, HouseholdRole.Owner));
        db.ForecastEntries.AddRange(
            new ForecastEntry(household.Id, "Salário", ForecastEntryType.Salary, 5000, 5),
            new ForecastEntry(household.Id, "Aluguel", ForecastEntryType.FixedExpense, 1500, 10));
        await db.SaveChangesAsync();

        var today = BusinessDate.Today();
        var client = new FakePluggyClient();
        var accounts = new List<PluggyAccount>
        {
            new("bank-1", "BANK", "Conta", "Banco", 2500, "CHECKING_ACCOUNT"),
            new("card-1", "CREDIT", "Cartão", "Banco", 999)
        };
        client.Snapshot = new PluggySnapshot(Item(), accounts, [
            new("p1", "card-1", "Mercado", 200, today, Status: "PENDING"),
            new("p2", "card-1", "Farmácia", 50, today, Status: "PENDING"),
            new("pay", "card-1", "Pagamento recebido", -700, today, Status: "POSTED"),
            new("b1", "bank-1", "Pix recebido", 300, today, Status: "POSTED"),
        ]);

        var currentUser = new StubCurrentUser(user.Id);
        var forecast = new BalanceForecastService(db, new CardService(db, new HouseholdAccessService(db, currentUser)));
        var service = new PluggyIntegrationService(db, currentUser, client,
            Options.Create(new PluggyOptions { ItemId = "item-1", AuthorizedEmail = "dono@example.test" }),
            NullLogger<PluggyIntegrationService>.Instance, null, forecast);
        await service.ActivateAsync(true, default);

        Assert.Equal(2500, (await db.FinancialAccounts.SingleAsync()).Balance);
        Assert.Equal(250, (await forecast.CalculateAsync(household.Id, default)).CardInvoices);

        // A transação pendente "p1" é substituída pela postada "p1-posted" com outro id.
        client.Snapshot = client.Snapshot with
        {
            Transactions = [
                new("p1-posted", "card-1", "Mercado", 200, today, Status: "POSTED"),
                new("p2", "card-1", "Farmácia", 50, today, Status: "PENDING"),
                new("pay", "card-1", "Pagamento recebido", -700, today, Status: "POSTED"),
            ]
        };
        var result = await service.SynchronizeAsync(null, "manual", default);

        Assert.Equal(1, result.Inserted);
        Assert.Equal(2, result.Deleted);
        Assert.Equal(3, await db.CardTransactions.CountAsync());
        var balance = await forecast.CalculateAsync(household.Id, default);
        Assert.Equal(250, balance.CardInvoices);
        Assert.Equal(5000 - 1500 - 250, balance.Net);
    }

    private static PluggyItem Item() => new("item-1", "UPDATED", DateTime.UtcNow.AddMonths(6), "Banco", null);

    private sealed class FakePluggyClient : IPluggyClient
    {
        public PluggySnapshot Snapshot { get; set; } = null!;
        public Task<PluggySnapshot> ReadCompleteSnapshotAsync(string expectedItemId, CancellationToken cancellationToken) =>
            Task.FromResult(Snapshot);
    }

    private sealed class StubCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }
}

public sealed class CardDescriptionRuleTests
{
    [Fact]
    public async Task Rule_applies_alias_and_category_to_every_installment_and_is_removed_when_empty()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"rules-{Guid.NewGuid():N}").Options);
        var user = new User("Dono", "dono@example.test", "DONO@EXAMPLE.TEST", "hash");
        var household = new Household("Casa", user.Id);
        var games = new KiaraFin.Api.Features.Finance.Category(household.Id, "Jogos",
            KiaraFin.Api.Features.Finance.CategoryType.Expense, "#123456", "gamepad");
        var card = new CreditCard(household.Id, "card-1");
        card.Apply("Cartão", "Banco", null, null, null, null, 0, null, null, null, true, DateTime.UtcNow);
        db.AddRange(user, household, new HouseholdMembership(household.Id, user.Id, HouseholdRole.Owner), games, card);
        var today = BusinessDate.Today();
        foreach (var (id, number) in new[] { ("t1", 1), ("t2", 2) })
        {
            var tx = new CardTransaction(card.Id, id);
            tx.Apply($"LEVEL UP INTERACT {number}/4", 50, CardTransactionKind.Purchase, today, today, true, null,
                number, 4, null, null, null, DateTime.UtcNow);
            db.Add(tx);
        }
        await db.SaveChangesAsync();
        var service = new CardService(db, new HouseholdAccessService(db, new StubCurrentUser(user.Id)));

        await service.SaveRuleAsync(household.Id, new SaveDescriptionRuleRequest("level up interact 1/4", " HyperGames Site ", games.Id), default);
        var detail = await service.GetAsync(household.Id, card.Id, default);
        var all = detail.CurrentInvoice.Items!.Concat(detail.Upcoming).ToList();
        Assert.All(all, x => Assert.Equal("HyperGames Site", x.Alias));
        Assert.All(all, x => Assert.Equal("Jogos", x.UserCategory?.Name));

        await service.SaveRuleAsync(household.Id, new SaveDescriptionRuleRequest("LEVEL UP INTERACT", "", null), default);
        Assert.Empty(await db.CardDescriptionRules.ToListAsync());
    }

    private sealed class StubCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }
}
