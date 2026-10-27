using KiaraFin.Api.Common.Security;
using KiaraFin.Api.Data;
using KiaraFin.Api.Features.CreditCards;
using KiaraFin.Api.Features.Finance;
using KiaraFin.Api.Features.Households;
using KiaraFin.Api.Features.Identity;
using KiaraFin.Api.Features.Installments;
using KiaraFin.Api.Features.Pluggy;
using KiaraFin.Api.Features.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KiaraFin.Api.Tests;

public sealed class PluggyIntegrationTests
{
    [Fact]
    public void Normalizer_uses_explicit_direction_instead_of_amount_sign()
    {
        var account = Account("BANK", 1000m);
        var debitWithPositiveAmount = Transaction("debit", 25m, "DEBIT", "PURCHASE", "POSTED");
        var creditWithNegativeAmount = Transaction("credit", -80m, "CREDIT", "DEPOSIT", "POSTED");
        Assert.Equal(ExternalTransactionKind.Expense, PluggyNormalizer.Normalize(account, debitWithPositiveAmount).Kind);
        Assert.Equal(ExternalTransactionKind.Income, PluggyNormalizer.Normalize(account, creditWithNegativeAmount).Kind);
    }

    [Fact]
    public void Normalizer_does_not_double_count_card_payment_and_offsets_refund_from_expenses()
    {
        var card = Account("CREDIT", -100m);
        var payment = Transaction("payment", 500m, "CREDIT", "CREDIT_CARD_PAYMENT", "POSTED");
        var refund = Transaction("refund", 30m, "CREDIT", "REFUND", "POSTED");
        Assert.False(PluggyNormalizer.Normalize(card, payment).CreatesLedgerEntry);
        var normalizedRefund = PluggyNormalizer.Normalize(card, refund);
        Assert.Equal(ExternalTransactionKind.Refund, normalizedRefund.Kind);
        Assert.True(normalizedRefund.CreatesLedgerEntry);
        Assert.True(normalizedRefund.IsRefund);
        Assert.Equal(FinancialTransactionType.Expense, normalizedRefund.FinancialType);
    }

    [Fact]
    public async Task Credit_account_creates_card_bill_and_real_card_entries_without_bank_account()
    {
        await using var db = CreateDatabase();
        var (user, household) = await SeedOwnerAsync(db);
        var card = new PluggyAccount("card-1", "CREDIT", "Mastercard Black", "Banco", 420m,
            "CREDIT_CARD", "card-hash", 5000m, 4580m, new DateOnly(2026, 10, 10),
            new DateOnly(2026, 10, 17), "MASTERCARD", "ACTIVE");
        var purchase = Transaction("card-purchase", 120m, "DEBIT", "PURCHASE", "POSTED") with
            { AccountId = "card-1", BillId = "bill-1", InstallmentNumber = 2, InstallmentCount = 6 };
        var refund = Transaction("card-refund", -20m, "CREDIT", "REFUND", "POSTED") with
            { AccountId = "card-1", BillId = "bill-1" };
        var bill = new PluggyBill("bill-1", "card-1", new DateOnly(2026, 10, 17),
            new DateOnly(2026, 10, 10), 100m, null, "bill-hash");
        var snapshot = new PluggySnapshot(new PluggyItem("item-1", "UPDATED", DateTime.UtcNow.AddMonths(6), "Meu Pluggy", null),
            [card], [purchase, refund], [bill]);
        var service = CreateService(db, user, new StubClient(snapshot));

        await service.ActivateAsync(true, default);

        Assert.Empty(await db.FinancialAccounts.ToListAsync());
        var importedCard = await db.CreditCards.SingleAsync();
        Assert.True(importedCard.InstitutionControlled);
        Assert.Equal(5000m, importedCard.TotalLimit);
        Assert.Equal(420m, importedCard.CurrentBalance);
        Assert.Equal(4580m, importedCard.AvailableLimit);
        var importedBill = await db.Invoices.SingleAsync();
        Assert.Equal("bill-1", importedBill.ExternalId);
        var entries = await db.FinancialTransactions.OrderBy(x => x.ExternalId).ToListAsync();
        Assert.Equal(2, entries.Count);
        Assert.All(entries, x => Assert.Equal(importedCard.Id, x.CreditCardId));
        Assert.Contains(entries, x => x.ExternalId == "card-refund" && x.IsRefund);
        Assert.Contains(entries, x => x.ExternalId == "card-purchase" && x.InstallmentNumber == 2 && x.InstallmentCount == 6);
        Assert.Empty(await db.AccountMovements.ToListAsync());
    }

    [Fact]
    public async Task Forming_invoice_contains_only_unbilled_pending_card_purchases()
    {
        await using var db = CreateDatabase();
        var (user, household) = await SeedOwnerAsync(db);
        var card = new PluggyAccount("card-1", "CREDIT", "Visa", "Banco", 300m,
            "CREDIT_CARD", "card-hash", 5000m, 4700m, null,
            new DateOnly(2026, 11, 12), "VISA", "ACTIVE");
        var bank = new PluggyAccount("bank-1", "BANK", "Conta", "Banco", 1000m,
            "CHECKING_ACCOUNT", "bank-hash");
        var cardPurchase = Transaction("card-pending", 300m, "DEBIT", "PURCHASE", "PENDING") with
            { AccountId = "card-1", Date = new DateOnly(2026, 10, 5) };
        var bankDebit = Transaction("bank-pending", 50m, "DEBIT", "PURCHASE", "PENDING") with
            { AccountId = "bank-1", Date = new DateOnly(2026, 10, 5) };
        var snapshot = new PluggySnapshot(
            new PluggyItem("item-1", "UPDATED", DateTime.UtcNow.AddMonths(6), "Meu Pluggy", null),
            [card, bank], [cardPurchase, bankDebit], []);

        await CreateService(db, user, new StubClient(snapshot)).ActivateAsync(true, default);
        var currentUser = new StubCurrentUser(user.Id);
        var cards = new CreditCardService(db, currentUser, new InstallmentService(db, currentUser));
        var importedCard = await db.CreditCards.SingleAsync();

        var response = await cards.GetAsync(household.Id, importedCard.Id, default);

        var forming = Assert.Single(response.FormingInvoices);
        var purchase = Assert.Single(forming.Purchases);
        Assert.Equal("card-pending", (await db.FinancialTransactions.SingleAsync(x => x.Id == purchase.Id)).ExternalId);
        Assert.Equal(300m, forming.EstimatedTotal);
        Assert.DoesNotContain(forming.Purchases, x => x.Description == "bank-pending");

        var transactions = new TransactionService(db, new HouseholdAccessService(db, currentUser));
        var ledger = await transactions.ListAsync(household.Id, 1, 20,
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), null, null, null, null, default);
        Assert.Equal(FinancialTransactionStatus.Pending,
            ledger.Items.Single(x => x.Description == "card-pending").Status);
        Assert.Equal(FinancialTransactionStatus.Overdue,
            ledger.Items.Single(x => x.Description == "bank-pending").Status);
    }

    [Fact]
    public async Task Missing_transaction_is_preserved_until_deleted_event_and_can_reappear()
    {
        await using var db = CreateDatabase();
        var (user, _) = await SeedOwnerAsync(db);
        var full = new PluggySnapshot(new PluggyItem("item-1", "UPDATED", DateTime.UtcNow.AddMonths(6), "Banco", null),
            [Account("BANK", 900m)], [Transaction("purchase", 100m, "DEBIT", "PURCHASE", "POSTED")]);
        var empty = full with { Transactions = [] };
        var client = new MutableClient(full);
        var service = CreateService(db, user, client);
        await service.ActivateAsync(true, default);
        var integration = await db.FinancialIntegrations.SingleAsync();

        client.Snapshot = empty;
        await service.SynchronizeAsync(integration.Id, "scheduler", default);
        Assert.Equal(FinancialTransactionStatus.Paid, (await db.FinancialTransactions.SingleAsync()).Status);

        await service.SynchronizeAsync(integration.Id, "transactions/deleted", default,
            "{\"data\":{\"id\":\"purchase\"}}");
        Assert.Equal(FinancialTransactionStatus.Canceled, (await db.FinancialTransactions.SingleAsync()).Status);

        client.Snapshot = full;
        await service.SynchronizeAsync(integration.Id, "scheduler", default);
        Assert.Equal(FinancialTransactionStatus.Paid, (await db.FinancialTransactions.SingleAsync()).Status);
        Assert.Single(await db.AccountMovements.ToListAsync());
    }

    [Fact]
    public async Task Reclassification_to_non_ledger_and_back_reuses_the_internal_transaction()
    {
        await using var db = CreateDatabase();
        var (user, _) = await SeedOwnerAsync(db);
        var purchase = Transaction("source-1", 100m, "DEBIT", "PURCHASE", "POSTED");
        var initial = new PluggySnapshot(new PluggyItem("item-1", "UPDATED", DateTime.UtcNow.AddMonths(6), "Banco", null),
            [Account("BANK", 900m)], [purchase]);
        var client = new MutableClient(initial);
        var service = CreateService(db, user, client);
        await service.ActivateAsync(true, default);
        var integration = await db.FinancialIntegrations.SingleAsync();
        var originalId = (await db.FinancialTransactions.SingleAsync()).Id;

        client.Snapshot = initial with
        {
            Transactions = [purchase with { OperationType = "CREDIT_CARD_PAYMENT", ContentHash = "payment-hash" }]
        };
        await service.SynchronizeAsync(integration.Id, "scheduler", default);
        Assert.Equal(FinancialTransactionStatus.Canceled, (await db.FinancialTransactions.SingleAsync()).Status);

        client.Snapshot = initial with
        {
            Transactions = [purchase with { ContentHash = "purchase-corrected-hash" }]
        };
        await service.SynchronizeAsync(integration.Id, "scheduler", default);

        var restored = await db.FinancialTransactions.SingleAsync();
        Assert.Equal(originalId, restored.Id);
        Assert.Equal(FinancialTransactionStatus.Paid, restored.Status);
    }

    [Fact]
    public async Task Activation_and_two_syncs_are_idempotent_and_preserve_authoritative_balance()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"pluggy-{Guid.NewGuid():N}").Options);
        var user = new User("Owner", "owner@example.test", "OWNER@EXAMPLE.TEST", "hash");
        var household = new Household("Casa", user.Id);
        db.AddRange(user, household, new HouseholdMembership(household.Id, user.Id, HouseholdRole.Owner));
        db.FinancialAccounts.Add(new FinancialAccount(household.Id, "Manual", FinancialAccountType.Checking, 10m,
            new DateOnly(2026, 1, 1), user.Id));
        await db.SaveChangesAsync();
        var snapshot = new PluggySnapshot(new PluggyItem("item-1", "UPDATED", DateTime.UtcNow.AddMonths(6), "Banco Real", null),
            [Account("BANK", 900m)],
            [Transaction("purchase", 100m, "DEBIT", "PURCHASE", "POSTED"), Transaction("pending", 40m, "DEBIT", "PURCHASE", "PENDING")]);
        var settings = Options.Create(new PluggyOptions { Enabled = true, ItemId = "item-1", AuthorizedEmail = user.Email });
        var service = new PluggyIntegrationService(db, new StubCurrentUser(user.Id), new StubClient(snapshot), settings,
            NullLogger<PluggyIntegrationService>.Instance);

        await service.ActivateAsync(true, default);
        var integration = await db.FinancialIntegrations.SingleAsync();
        await service.SynchronizeAsync(integration.Id, "test", default);
        await service.SynchronizeAsync(integration.Id, "test", default);

        Assert.Equal(2, await db.ExternalTransactionMappings.CountAsync());
        Assert.Equal(2, await db.FinancialTransactions.CountAsync());
        Assert.Single(await db.AccountMovements.ToListAsync());
        var account = await db.FinancialAccounts.SingleAsync();
        var net = await db.AccountMovements.SumAsync(x => x.SignedAmount);
        Assert.Equal(900m, account.InitialBalance + net);
        Assert.Equal(FinancialTransactionStatus.Pending,
            (await db.FinancialTransactions.SingleAsync(x => x.ExternalId == "pending")).Status);
    }

    private static PluggyAccount Account(string type, decimal balance) => new("account-1", type, "Conta", "Banco", balance, null, $"hash-{type}-{balance}");
    private static PluggyTransaction Transaction(string id, decimal amount, string type, string operation, string status) =>
        new(id, "account-1", id, amount, new DateOnly(2026, 10, 1), type, operation, status, null, null, null, null, null, null, null, $"hash-{id}");
    private sealed class StubClient(PluggySnapshot snapshot) : IPluggyClient { public Task<PluggySnapshot> ReadCompleteSnapshotAsync(string expectedItemId, CancellationToken cancellationToken) => Task.FromResult(snapshot); }
    private sealed class MutableClient(PluggySnapshot snapshot) : IPluggyClient
    {
        public PluggySnapshot Snapshot { get; set; } = snapshot;
        public Task<PluggySnapshot> ReadCompleteSnapshotAsync(string expectedItemId, CancellationToken cancellationToken) => Task.FromResult(Snapshot);
    }
    private sealed class StubCurrentUser(Guid id) : ICurrentUser { public Guid UserId { get; } = id; }

    private static AppDbContext CreateDatabase() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"pluggy-{Guid.NewGuid():N}").Options);
    private static async Task<(User User, Household Household)> SeedOwnerAsync(AppDbContext db)
    {
        var user = new User("Owner", "owner@example.test", "OWNER@EXAMPLE.TEST", "hash");
        var household = new Household("Casa", user.Id);
        db.AddRange(user, household, new HouseholdMembership(household.Id, user.Id, HouseholdRole.Owner));
        await db.SaveChangesAsync();
        return (user, household);
    }
    private static PluggyIntegrationService CreateService(AppDbContext db, User user, IPluggyClient client) =>
        new(db, new StubCurrentUser(user.Id), client,
            Options.Create(new PluggyOptions { Enabled = true, ItemId = "item-1", AuthorizedEmail = user.Email }),
            NullLogger<PluggyIntegrationService>.Instance);
}
