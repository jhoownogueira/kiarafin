using KiaraFin.Api.Common;
using KiaraFin.Api.Common.Exceptions;
using KiaraFin.Api.Data;
using KiaraFin.Api.Features.Households;
using Microsoft.EntityFrameworkCore;

namespace KiaraFin.Api.Features.Cards;

public sealed record CardWithInvoices(CreditCard Card, CardInvoices Invoices);

public sealed class CardService(AppDbContext dbContext, HouseholdAccessService access)
{
    public async Task<IReadOnlyList<CardSummaryResponse>> ListAsync(Guid householdId, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        var today = BusinessDate.Today();
        var cards = await LoadAsync(householdId, null, today, cancellationToken);
        var mapper = await ItemMapperAsync(householdId, cancellationToken);
        return cards.Select(x => Summary(x, today, mapper)).ToList();
    }

    public async Task<CardDetailResponse> GetAsync(Guid householdId, Guid cardId, CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        var today = BusinessDate.Today();
        var item = (await LoadAsync(householdId, cardId, today, cancellationToken)).SingleOrDefault()
            ?? throw new NotFoundException("Cartão não encontrado.");
        var mapper = await ItemMapperAsync(householdId, cancellationToken);
        return new CardDetailResponse(
            Summary(item, today, mapper),
            Open(item.Invoices.Current, mapper, includeItems: true),
            item.Invoices.Upcoming.Select(mapper.Map).ToList(),
            item.Invoices.Closed.Select(x => Closed(x, mapper, includeItems: true)).ToList(),
            item.Invoices.Payments.OrderByDescending(x => x.Date).Select(x => mapper.Map(new InvoiceLine(x, 0))).ToList());
    }

    /// <summary>
    /// Cria, altera ou remove (apelido e categoria vazios) a regra da descrição informada.
    /// A regra vale para todos os lançamentos de cartão com a mesma descrição normalizada.
    /// </summary>
    public async Task<DescriptionRuleResponse> SaveRuleAsync(Guid householdId, SaveDescriptionRuleRequest request,
        CancellationToken cancellationToken)
    {
        await access.RequireMembershipAsync(householdId, cancellationToken);
        var key = CardInvoiceCalculator.NormalizeDescription(request.Description);
        if (key.Length == 0) throw new AppValidationException("Informe a descrição do lançamento.");
        if (request.CategoryId is Guid categoryId &&
            !await dbContext.Categories.AnyAsync(x => x.Id == categoryId && x.HouseholdId == householdId, cancellationToken))
            throw new AppValidationException("Categoria não encontrada.");

        var rule = await dbContext.CardDescriptionRules
            .SingleOrDefaultAsync(x => x.HouseholdId == householdId && x.Key == key, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Alias) && request.CategoryId is null)
        {
            if (rule is not null) dbContext.CardDescriptionRules.Remove(rule);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new DescriptionRuleResponse(key, null, null);
        }
        if (rule is null)
        {
            rule = new CardDescriptionRule(householdId, key);
            dbContext.CardDescriptionRules.Add(rule);
        }
        rule.Apply(request.Alias, request.CategoryId, DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new DescriptionRuleResponse(rule.Key, rule.Alias, rule.CategoryId);
    }

    /// <summary>
    /// Agrupa os lançamentos das faturas atuais (inclui parcelas projetadas e estornos) pela
    /// categoria definida nas regras de descrição. Sem regra/categoria = "Sem categoria".
    /// </summary>
    public async Task<IReadOnlyList<CategoryInvoiceGroup>> CurrentInvoiceByCategoryAsync(Guid householdId,
        IReadOnlyList<CardWithInvoices> cards, CancellationToken cancellationToken)
    {
        var mapper = await ItemMapperAsync(householdId, cancellationToken);
        return cards
            .SelectMany(card => card.Invoices.Current.Lines.Select(line => new CategoryInvoiceItem(card.Card.Name, mapper.Map(line))))
            .GroupBy(x => x.Item.UserCategory?.Id)
            .Select(group =>
            {
                var category = group.First().Item.UserCategory;
                return new CategoryInvoiceGroup(category?.Id, category?.Name ?? "Sem categoria", category?.Color,
                    group.Sum(x => x.Item.Effect),
                    group.OrderByDescending(x => x.Item.Effect).ThenBy(x => x.Item.Description).ToList());
            })
            .OrderByDescending(x => x.Total)
            .ToList();
    }

    /// <summary>Faturas calculadas de todos os cartões ativos (usado pela previsão de saldo).</summary>
    public Task<IReadOnlyList<CardWithInvoices>> LoadActiveAsync(Guid householdId, DateOnly today,
        CancellationToken cancellationToken) => LoadAsync(householdId, null, today, cancellationToken, activeOnly: true);

    private async Task<IReadOnlyList<CardWithInvoices>> LoadAsync(Guid householdId, Guid? cardId, DateOnly today,
        CancellationToken cancellationToken, bool activeOnly = false)
    {
        var cardsQuery = dbContext.CreditCards.AsNoTracking().Where(x => x.HouseholdId == householdId);
        if (cardId is Guid id) cardsQuery = cardsQuery.Where(x => x.Id == id);
        if (activeOnly) cardsQuery = cardsQuery.Where(x => x.IsActive);
        var cards = await cardsQuery.OrderByDescending(x => x.IsActive).ThenBy(x => x.Name).ToListAsync(cancellationToken);
        var ids = cards.Select(x => x.Id).ToList();
        var bills = (await dbContext.CardBills.AsNoTracking().Where(x => ids.Contains(x.CardId)).ToListAsync(cancellationToken))
            .ToLookup(x => x.CardId);
        var transactions = (await dbContext.CardTransactions.AsNoTracking().Where(x => ids.Contains(x.CardId)).ToListAsync(cancellationToken))
            .ToLookup(x => x.CardId);
        return cards.Select(card => new CardWithInvoices(card,
            CardInvoiceCalculator.Calculate(card, bills[card.Id].ToList(), transactions[card.Id].ToList(), today))).ToList();
    }

    private async Task<ItemMapper> ItemMapperAsync(Guid householdId, CancellationToken cancellationToken)
    {
        var rules = await dbContext.CardDescriptionRules.AsNoTracking()
            .Where(x => x.HouseholdId == householdId).ToListAsync(cancellationToken);
        var categories = await dbContext.Categories.AsNoTracking()
            .Where(x => x.HouseholdId == householdId)
            .ToDictionaryAsync(x => x.Id, x => new CardItemCategory(x.Id, x.Name, x.Color), cancellationToken);
        return new ItemMapper(rules.ToDictionary(x => x.Key, StringComparer.Ordinal), categories);
    }

    private static CardSummaryResponse Summary(CardWithInvoices item, DateOnly today, ItemMapper mapper)
    {
        var card = item.Card;
        var last = item.Invoices.Closed.FirstOrDefault();
        return new CardSummaryResponse(card.Id, card.Name, card.Institution, card.Brand, card.Number,
            card.CreditLimit, card.AvailableLimit, card.ReportedBalance, card.IsActive, card.UpdatedAtUtc,
            Open(item.Invoices.Current, mapper, includeItems: false),
            item.Invoices.OutstandingClosedDueFrom(today),
            last is null ? null : Closed(last, mapper, includeItems: false));
    }

    private static OpenInvoiceResponse Open(OpenInvoice invoice, ItemMapper mapper, bool includeItems) => new(
        invoice.Total, invoice.ClosingDate, invoice.DueDate, invoice.Lines.Count,
        includeItems ? invoice.Lines.Select(mapper.Map).ToList() : null);

    private static ClosedInvoiceResponse Closed(ClosedInvoice invoice, ItemMapper mapper, bool includeItems) => new(
        invoice.Bill.Id, invoice.Bill.DueDate, invoice.Bill.ClosingDate, invoice.Bill.TotalAmount,
        invoice.Bill.MinimumPayment, invoice.PaidAmount, invoice.Outstanding, invoice.IsPaid,
        invoice.Lines.Sum(x => x.Effect), includeItems ? invoice.Lines.Select(mapper.Map).ToList() : null);

    private sealed class ItemMapper(IReadOnlyDictionary<string, CardDescriptionRule> rules,
        IReadOnlyDictionary<Guid, CardItemCategory> categories)
    {
        public CardItemResponse Map(InvoiceLine line)
        {
            var x = line.Transaction;
            rules.TryGetValue(CardInvoiceCalculator.NormalizeDescription(x.Description), out var rule);
            var category = rule?.CategoryId is Guid categoryId ? categories.GetValueOrDefault(categoryId) : null;
            return new CardItemResponse(x.Id, x.Description, x.Amount, line.Effect, x.Kind, x.Date, x.PurchaseDate,
                x.IsPending, x.InstallmentNumber, x.TotalInstallments, x.Category, x.CurrencyCode, x.OriginalAmount,
                line.Projected, rule?.Alias, category);
        }
    }
}
