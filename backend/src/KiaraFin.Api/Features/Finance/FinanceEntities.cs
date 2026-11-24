namespace KiaraFin.Api.Features.Finance;

/// <summary>
/// Conta bancária. Contas do Open Finance têm o saldo espelhado da instituição;
/// contas manuais têm o saldo informado pelo usuário.
/// </summary>
public sealed class FinancialAccount
{
    private FinancialAccount() { }

    public FinancialAccount(Guid householdId, string name, FinancialAccountType type, decimal balance,
        string? institution = null, string? color = null, string? icon = null)
    {
        FinanceGuard.NotEmpty(householdId, nameof(householdId));
        Id = Guid.NewGuid();
        HouseholdId = householdId;
        Name = FinanceGuard.RequiredText(name, nameof(name));
        Type = type;
        Balance = FinanceGuard.Money(balance, nameof(balance), allowNegative: true);
        Institution = FinanceGuard.OptionalText(institution);
        Color = FinanceGuard.OptionalText(color);
        Icon = FinanceGuard.OptionalText(icon);
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static FinancialAccount FromInstitution(Guid householdId, string provider, string externalId,
        string name, FinancialAccountType type, decimal balance, string? institution)
    {
        var account = new FinancialAccount(householdId, name, type, 0, institution);
        account.SourceProvider = FinanceGuard.RequiredText(provider, nameof(provider));
        account.ExternalId = FinanceGuard.RequiredText(externalId, nameof(externalId));
        account.SynchronizeFromInstitution(name, type, balance, institution, DateTime.UtcNow);
        return account;
    }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public string Name { get; private set; } = null!;
    public FinancialAccountType Type { get; private set; }
    public string? Institution { get; private set; }
    public string? Color { get; private set; }
    public string? Icon { get; private set; }
    public decimal Balance { get; private set; }
    public bool IsActive { get; private set; }
    public string? SourceProvider { get; private set; }
    public string? ExternalId { get; private set; }
    public bool InstitutionControlled => ExternalId is not null;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <returns>true quando algum campo mudou.</returns>
    public bool SynchronizeFromInstitution(string name, FinancialAccountType type, decimal balance,
        string? institution, DateTime utcNow)
    {
        var normalizedName = FinanceGuard.RequiredText(name, nameof(name));
        var normalizedInstitution = FinanceGuard.OptionalText(institution);
        var rounded = decimal.Round(balance, 2, MidpointRounding.AwayFromZero);
        var changed = Name != normalizedName || Type != type || Balance != rounded ||
                      Institution != normalizedInstitution || !IsActive;
        Name = normalizedName;
        Type = type;
        Balance = rounded;
        Institution = normalizedInstitution;
        IsActive = true;
        if (changed) UpdatedAtUtc = FinanceGuard.Utc(utcNow, nameof(utcNow));
        return changed;
    }

    public void Update(string name, FinancialAccountType type, string? institution, string? color,
        string? icon, decimal balance, DateTime utcNow)
    {
        Name = FinanceGuard.RequiredText(name, nameof(name));
        Type = type;
        Institution = FinanceGuard.OptionalText(institution);
        Color = FinanceGuard.OptionalText(color);
        Icon = FinanceGuard.OptionalText(icon);
        Balance = FinanceGuard.Money(balance, nameof(balance), allowNegative: true);
        UpdatedAtUtc = FinanceGuard.Utc(utcNow, nameof(utcNow));
    }

    public void SetActive(bool isActive, DateTime utcNow)
    {
        IsActive = isActive;
        UpdatedAtUtc = FinanceGuard.Utc(utcNow, nameof(utcNow));
    }
}

/// <summary>Movimentação de uma conta bancária, espelhada do Open Finance.</summary>
public sealed class BankTransaction
{
    private BankTransaction() { }

    public BankTransaction(Guid householdId, Guid accountId, string externalId)
    {
        Id = Guid.NewGuid();
        HouseholdId = householdId;
        AccountId = accountId;
        ExternalId = FinanceGuard.RequiredText(externalId, nameof(externalId));
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid AccountId { get; private set; }
    public string ExternalId { get; private set; } = null!;
    public string Description { get; private set; } = "";
    /// <summary>Positivo = entrada; negativo = saída.</summary>
    public decimal Amount { get; private set; }
    public DateOnly Date { get; private set; }
    public bool IsPending { get; private set; }
    public string? Category { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <returns>true quando algum campo mudou.</returns>
    public bool Apply(string description, decimal amount, DateOnly date, bool isPending, string? category, DateTime utcNow)
    {
        var normalized = FinanceGuard.RequiredText(description, nameof(description));
        var rounded = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        var changed = Description != normalized || Amount != rounded || Date != date ||
                      IsPending != isPending || Category != category;
        Description = normalized;
        Amount = rounded;
        Date = date;
        IsPending = isPending;
        Category = category;
        if (changed) UpdatedAtUtc = utcNow;
        return changed;
    }
}

public sealed class Category
{
    private Category() { }

    public Category(Guid householdId, string name, CategoryType type, string color, string icon, bool isSystem = false)
    {
        FinanceGuard.NotEmpty(householdId, nameof(householdId));
        Id = Guid.NewGuid();
        HouseholdId = householdId;
        Name = FinanceGuard.RequiredText(name, nameof(name));
        Type = type;
        Color = FinanceGuard.RequiredText(color, nameof(color));
        Icon = FinanceGuard.RequiredText(icon, nameof(icon));
        IsSystem = isSystem;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public string Name { get; private set; } = null!;
    public CategoryType Type { get; private set; }
    public string Color { get; private set; } = null!;
    public string Icon { get; private set; } = null!;
    public bool IsSystem { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public void SetActive(bool isActive, DateTime utcNow)
    {
        IsActive = isActive;
        UpdatedAtUtc = FinanceGuard.Utc(utcNow, nameof(utcNow));
    }

    public void Update(string name, string color, string icon, DateTime utcNow)
    {
        Name = FinanceGuard.RequiredText(name, nameof(name));
        Color = FinanceGuard.RequiredText(color, nameof(color));
        Icon = FinanceGuard.RequiredText(icon, nameof(icon));
        UpdatedAtUtc = FinanceGuard.Utc(utcNow, nameof(utcNow));
    }
}

internal static class FinanceGuard
{
    public static void NotEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Identifier must not be empty.", parameterName);
    }

    public static string RequiredText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value is required.", parameterName);
        return value.Trim();
    }

    public static string? OptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static decimal Money(decimal value, string parameterName, bool allowNegative = false)
    {
        if (!allowNegative && value < 0)
            throw new ArgumentOutOfRangeException(parameterName, "Value must not be negative.");
        if (decimal.Round(value, 2, MidpointRounding.ToEven) != value)
            throw new ArgumentException("Money must have at most two decimal places.", parameterName);
        return value;
    }

    public static DateTime Utc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Timestamp must be UTC.", parameterName);
        return value;
    }
}
