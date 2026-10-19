namespace KiaraFin.Api.Features.Forecasts;

public enum ForecastEntryType
{
    Salary,
    ExtraIncome,
    FixedExpense
}

public sealed class ForecastEntry
{
    private ForecastEntry() { }

    public ForecastEntry(Guid householdId, string name, ForecastEntryType type, decimal amount, int expectedDay)
    {
        if (householdId == Guid.Empty)
            throw new ArgumentException("O núcleo financeiro é obrigatório.", nameof(householdId));

        Id = Guid.NewGuid();
        HouseholdId = householdId;
        Apply(name, type, amount, expectedDay);
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public string Name { get; private set; } = null!;
    public ForecastEntryType Type { get; private set; }
    public decimal Amount { get; private set; }
    public int ExpectedDay { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public void Update(string name, ForecastEntryType type, decimal amount, int expectedDay, DateTime utcNow)
    {
        Apply(name, type, amount, expectedDay);
        UpdatedAtUtc = utcNow.Kind == DateTimeKind.Utc
            ? utcNow
            : throw new ArgumentException("A data de atualização deve estar em UTC.", nameof(utcNow));
    }

    private void Apply(string name, ForecastEntryType type, decimal amount, int expectedDay)
    {
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type), "O tipo da previsão é inválido.");
        var normalizedName = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
            throw new ArgumentException("Informe o nome da previsão.", nameof(name));
        if (normalizedName.Length > 160)
            throw new ArgumentException("O nome da previsão deve ter no máximo 160 caracteres.", nameof(name));
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "O valor previsto deve ser maior que zero.");
        if (expectedDay is < 1 or > 31)
            throw new ArgumentOutOfRangeException(nameof(expectedDay), "O dia esperado deve estar entre 1 e 31.");

        Name = normalizedName;
        Type = type;
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        ExpectedDay = expectedDay;
    }
}
