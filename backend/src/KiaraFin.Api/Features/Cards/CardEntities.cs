namespace KiaraFin.Api.Features.Cards;

public enum CardTransactionKind
{
    /// <summary>Compra, tarifa, juros ou IOF: aumenta a fatura.</summary>
    Purchase,
    /// <summary>Estorno ou crédito: reduz a fatura.</summary>
    Credit,
    /// <summary>Pagamento de fatura: não entra no total da fatura.</summary>
    Payment
}

/// <summary>Cartão de crédito espelhado de uma conta CREDIT da Pluggy.</summary>
public sealed class CreditCard
{
    private CreditCard() { }

    public CreditCard(Guid householdId, string externalId)
    {
        Id = Guid.NewGuid();
        HouseholdId = householdId;
        ExternalId = externalId;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public string ExternalId { get; private set; } = null!;
    public string Name { get; private set; } = "";
    public string? Institution { get; private set; }
    public string? Brand { get; private set; }
    public string? Number { get; private set; }
    public decimal? CreditLimit { get; private set; }
    public decimal? AvailableLimit { get; private set; }
    /// <summary>Campo "balance" da Pluggy, guardado só para conferência.</summary>
    public decimal ReportedBalance { get; private set; }
    public decimal? MinimumPayment { get; private set; }
    public DateOnly? BalanceCloseDate { get; private set; }
    public DateOnly? BalanceDueDate { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public bool Apply(string name, string? institution, string? brand, string? number, decimal? creditLimit,
        decimal? availableLimit, decimal reportedBalance, decimal? minimumPayment, DateOnly? closeDate,
        DateOnly? dueDate, bool isActive, DateTime utcNow)
    {
        var changed = Name != name || Institution != institution || Brand != brand || Number != number ||
                      CreditLimit != creditLimit || AvailableLimit != availableLimit ||
                      ReportedBalance != reportedBalance || MinimumPayment != minimumPayment ||
                      BalanceCloseDate != closeDate || BalanceDueDate != dueDate || IsActive != isActive;
        Name = name; Institution = institution; Brand = brand; Number = number;
        CreditLimit = creditLimit; AvailableLimit = availableLimit; ReportedBalance = reportedBalance;
        MinimumPayment = minimumPayment; BalanceCloseDate = closeDate; BalanceDueDate = dueDate;
        IsActive = isActive;
        if (changed) UpdatedAtUtc = utcNow;
        return changed;
    }
}

/// <summary>Fatura fechada, exatamente como retornada pelo recurso /bills da Pluggy.</summary>
public sealed class CardBill
{
    private CardBill() { }

    public CardBill(Guid cardId, string externalId)
    {
        Id = Guid.NewGuid();
        CardId = cardId;
        ExternalId = externalId;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid CardId { get; private set; }
    public string ExternalId { get; private set; } = null!;
    public DateOnly DueDate { get; private set; }
    public DateOnly? ClosingDate { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal? MinimumPayment { get; private set; }
    public decimal PaidAmount { get; private set; }
    public DateOnly? LastPaymentDate { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public bool Apply(DateOnly dueDate, DateOnly? closingDate, decimal totalAmount, decimal? minimumPayment,
        decimal paidAmount, DateOnly? lastPaymentDate, DateTime utcNow)
    {
        var changed = DueDate != dueDate || ClosingDate != closingDate || TotalAmount != totalAmount ||
                      MinimumPayment != minimumPayment || PaidAmount != paidAmount ||
                      LastPaymentDate != lastPaymentDate;
        DueDate = dueDate; ClosingDate = closingDate; TotalAmount = totalAmount;
        MinimumPayment = minimumPayment; PaidAmount = paidAmount; LastPaymentDate = lastPaymentDate;
        if (changed) UpdatedAtUtc = utcNow;
        return changed;
    }
}

/// <summary>Lançamento do cartão, espelhado da Pluggy.</summary>
public sealed class CardTransaction
{
    private CardTransaction() { }

    public CardTransaction(Guid cardId, string externalId)
    {
        Id = Guid.NewGuid();
        CardId = cardId;
        ExternalId = externalId;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid CardId { get; private set; }
    public string ExternalId { get; private set; } = null!;
    public string Description { get; private set; } = "";
    /// <summary>Valor em reais, sempre positivo. O efeito na fatura vem de <see cref="Kind"/>.</summary>
    public decimal Amount { get; private set; }
    public CardTransactionKind Kind { get; private set; }
    public DateOnly Date { get; private set; }
    public DateOnly? PurchaseDate { get; private set; }
    public bool IsPending { get; private set; }
    /// <summary>Id externo da fatura fechada (creditCardMetadata.billId). Nulo = fatura aberta.</summary>
    public string? BillExternalId { get; private set; }
    public int? InstallmentNumber { get; private set; }
    public int? TotalInstallments { get; private set; }
    public string? Category { get; private set; }
    public string? CurrencyCode { get; private set; }
    public decimal? OriginalAmount { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Efeito no total da fatura: compra soma, crédito subtrai, pagamento não conta.</summary>
    public decimal InvoiceEffect => Kind switch
    {
        CardTransactionKind.Purchase => Amount,
        CardTransactionKind.Credit => -Amount,
        _ => 0m
    };

    public bool Apply(string description, decimal amount, CardTransactionKind kind, DateOnly date,
        DateOnly? purchaseDate, bool isPending, string? billExternalId, int? installmentNumber,
        int? totalInstallments, string? category, string? currencyCode, decimal? originalAmount, DateTime utcNow)
    {
        var changed = Description != description || Amount != amount || Kind != kind || Date != date ||
                      PurchaseDate != purchaseDate || IsPending != isPending || BillExternalId != billExternalId ||
                      InstallmentNumber != installmentNumber || TotalInstallments != totalInstallments ||
                      Category != category || CurrencyCode != currencyCode || OriginalAmount != originalAmount;
        Description = description; Amount = amount; Kind = kind; Date = date; PurchaseDate = purchaseDate;
        IsPending = isPending; BillExternalId = billExternalId; InstallmentNumber = installmentNumber;
        TotalInstallments = totalInstallments; Category = category; CurrencyCode = currencyCode;
        OriginalAmount = originalAmount;
        if (changed) UpdatedAtUtc = utcNow;
        return changed;
    }
}

/// <summary>
/// Apelido e categoria do usuário para uma descrição de lançamento de cartão. Vale para todos os
/// lançamentos cuja descrição normalizada (maiúsculas, sem sufixo de parcela) seja igual a <see cref="Key"/>.
/// </summary>
public sealed class CardDescriptionRule
{
    private CardDescriptionRule() { }

    public CardDescriptionRule(Guid householdId, string key)
    {
        Id = Guid.NewGuid();
        HouseholdId = householdId;
        Key = key;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public string Key { get; private set; } = null!;
    public string? Alias { get; private set; }
    public Guid? CategoryId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public void Apply(string? alias, Guid? categoryId, DateTime utcNow)
    {
        Alias = string.IsNullOrWhiteSpace(alias) ? null : alias.Trim();
        CategoryId = categoryId;
        UpdatedAtUtc = utcNow;
    }
}
