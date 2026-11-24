using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Text.RegularExpressions;
using KiaraFin.Api.Common.Exceptions;
using Microsoft.Extensions.Options;

namespace KiaraFin.Api.Features.Pluggy;

public sealed class PluggyOptions
{
    public const string SectionName = "Pluggy";
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://api.pluggy.ai";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string ItemId { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
    public string AuthorizedEmail { get; set; } = "";
    public int SyncIntervalHours { get; set; } = 24;
}

public sealed record PluggyItem(string Id, string Status, DateTime? ConsentExpiresAtUtc, string? ConnectorName, string? ErrorCode);

public sealed record PluggyAccount(string Id, string Type, string Name, string? InstitutionName, decimal Balance,
    string? Subtype = null, string? Number = null, decimal? CreditLimit = null, decimal? AvailableCreditLimit = null,
    decimal? MinimumPayment = null, DateOnly? BalanceCloseDate = null, DateOnly? BalanceDueDate = null,
    string? Brand = null, string? Status = null)
{
    public bool IsCreditCard => Type.Equals("CREDIT", StringComparison.OrdinalIgnoreCase);
}

/// <param name="Amount">Valor na moeda da conta (amountInAccountCurrency quando informado), com o sinal da Pluggy.</param>
/// <param name="Date">Data local (America/Sao_Paulo).</param>
public sealed record PluggyTransaction(string Id, string AccountId, string Description, decimal Amount, DateOnly Date,
    string? Type = null, string? OperationType = null, string? Status = null, string? Category = null,
    int? InstallmentNumber = null, int? TotalInstallments = null, string? BillId = null, DateOnly? PurchaseDate = null,
    string? CurrencyCode = null, decimal? OriginalAmount = null)
{
    public bool IsPending => string.Equals(Status, "PENDING", StringComparison.OrdinalIgnoreCase);
}

public sealed record PluggyBill(string Id, string AccountId, DateOnly DueDate, DateOnly? ClosingDate,
    decimal TotalAmount, decimal? MinimumPayment = null, decimal PaidAmount = 0, DateOnly? LastPaymentDate = null);

public sealed record PluggySnapshot(PluggyItem Item, IReadOnlyList<PluggyAccount> Accounts,
    IReadOnlyList<PluggyTransaction> Transactions, IReadOnlyList<PluggyBill>? Bills = null)
{
    public IReadOnlyList<PluggyBill> CreditCardBills => Bills ?? [];
}

public interface IPluggyClient { Task<PluggySnapshot> ReadCompleteSnapshotAsync(string expectedItemId, CancellationToken cancellationToken); }

public sealed partial class PluggyClient(HttpClient httpClient, IOptions<PluggyOptions> options) : IPluggyClient
{
    private readonly PluggyOptions _options = options.Value;
    private readonly SemaphoreSlim _authLock = new(1, 1);
    private string? _apiKey;
    private DateTime _apiKeyExpiresAtUtc;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<PluggySnapshot> ReadCompleteSnapshotAsync(string expectedItemId, CancellationToken cancellationToken)
    {
        if (!_options.Enabled) throw new ConflictException("A integração Pluggy está desabilitada.");
        if (string.IsNullOrWhiteSpace(expectedItemId)) throw new AppValidationException("ItemId Pluggy não configurado.");
        using var itemJson = await GetAsync($"items/{Uri.EscapeDataString(expectedItemId)}", cancellationToken)
            ?? throw new PluggyUnavailableException("O Item Pluggy configurado não foi encontrado.");
        var item = ParseItem(itemJson.RootElement);
        if (!string.Equals(item.Id, expectedItemId, StringComparison.Ordinal)) throw new AppValidationException("O ItemId retornado não corresponde à configuração.");
        if (item.ConsentExpiresAtUtc <= DateTime.UtcNow ||
            string.Equals(item.ErrorCode, "USER_AUTHORIZATION_EXPIRED", StringComparison.OrdinalIgnoreCase) ||
            item.Status.Contains("EXPIRED", StringComparison.OrdinalIgnoreCase))
            throw new PluggyConsentExpiredException("O consentimento Pluggy expirou.");
        if (!IsHealthyStatus(item.Status)) throw new PluggyUnavailableException($"Item Pluggy indisponível ({item.Status}).");
        var accounts = await ReadAllPagesAsync("accounts", $"itemId={Uri.EscapeDataString(expectedItemId)}", ParseAccount, cancellationToken);
        if (accounts.Count == 0) throw new PluggyIncompleteResponseException("A Pluggy não retornou contas para o Item configurado.");
        var transactions = new List<PluggyTransaction>();
        var bills = new List<PluggyBill>();
        foreach (var account in accounts)
        {
            transactions.AddRange(await ReadAllTransactionsAsync(account.Id, cancellationToken));
            if (account.Type.Equals("CREDIT", StringComparison.OrdinalIgnoreCase))
                bills.AddRange(await ReadAllPagesAsync("bills", $"accountId={Uri.EscapeDataString(account.Id)}",
                    e => ParseBill(e, account.Id), cancellationToken, allowMissingResource: true));
        }
        return new PluggySnapshot(item, accounts, transactions, bills);
    }

    private async Task<List<T>> ReadAllPagesAsync<T>(string resource, string filter, Func<JsonElement,T> parse,
        CancellationToken ct, bool allowMissingResource = false)
    {
        var all = new List<T>(); int page = 1; int? expectedPages = null; int? expectedTotal = null;
        do
        {
            using var doc = await GetAsync($"{resource}?{filter}&page={page}&pageSize=500", ct, allowMissingResource);
            if (doc is null)
            {
                if (page == 1) return [];
                throw new PluggyIncompleteResponseException($"Paginação incompleta em {resource}.");
            }
            var root = doc.RootElement;
            if (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                throw new PluggyIncompleteResponseException($"Resposta paginada inválida em {resource}.");
            expectedPages ??= ReadInt(root, "totalPages") ?? 1; expectedTotal ??= ReadInt(root, "total");
            if (expectedPages < 1 || page > expectedPages) throw new PluggyIncompleteResponseException($"Paginação inconsistente em {resource}.");
            var count = 0; foreach (var element in results.EnumerateArray()) { all.Add(parse(element)); count++; }
            if (page < expectedPages && count == 0) throw new PluggyIncompleteResponseException($"Página intermediária vazia em {resource}.");
            page++;
        } while (page <= expectedPages);
        if (expectedTotal is not null && all.Count != expectedTotal) throw new PluggyIncompleteResponseException($"Paginação incompleta em {resource}: esperado {expectedTotal}, recebido {all.Count}.");
        return all;
    }

    private async Task<List<PluggyTransaction>> ReadAllTransactionsAsync(string accountId, CancellationToken ct)
    {
        var all = new List<PluggyTransaction>();
        var path = $"v2/transactions?accountId={Uri.EscapeDataString(accountId)}";
        var visited = new HashSet<string>(StringComparer.Ordinal) { path };
        while (true)
        {
            using var doc = await GetAsync(path, ct);
            var root = doc?.RootElement ?? throw new PluggyIncompleteResponseException("Resposta vazia em transactions.");
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                throw new PluggyIncompleteResponseException("Resposta paginada inválida em transactions.");
            foreach (var element in results.EnumerateArray()) all.Add(ParseTransaction(element, accountId));

            if (!root.TryGetProperty("next", out var next))
                throw new PluggyIncompleteResponseException("Cursor ausente em transactions.");
            if (next.ValueKind == JsonValueKind.Null) return all;
            if (next.ValueKind != JsonValueKind.String || next.GetString() is not string query ||
                !query.StartsWith("?", StringComparison.Ordinal) || query.Length == 1)
                throw new PluggyIncompleteResponseException("Cursor inválido em transactions.");

            path = $"v2/transactions{query}";
            if (!visited.Add(path)) throw new PluggyIncompleteResponseException("Cursor repetido em transactions.");
        }
    }

    private async Task<JsonDocument?> GetAsync(string path, CancellationToken ct, bool allowNotFound = false)
    {
        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var key = await GetApiKeyAsync(attempt > 0, ct);
                using var request = new HttpRequestMessage(HttpMethod.Get, path); request.Headers.Add("X-API-KEY", key);
                using var response = await httpClient.SendAsync(request, ct);
                if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0) continue;
                if (allowNotFound && response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden) return null;
                if (!response.IsSuccessStatusCode) throw new PluggyUnavailableException($"Pluggy retornou HTTP {(int)response.StatusCode}.");
                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                try { return await JsonDocument.ParseAsync(stream, cancellationToken: ct); }
                catch (JsonException) { throw new PluggyIncompleteResponseException("Resposta JSON inválida da Pluggy."); }
            }
        }
        catch (HttpRequestException)
        {
            throw new PluggyUnavailableException("Não foi possível acessar a API da Pluggy.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new PluggyUnavailableException("A API da Pluggy demorou demais para responder.");
        }
        throw new PluggyUnavailableException("Não foi possível autenticar na Pluggy.");
    }

    private async Task<string> GetApiKeyAsync(bool force, CancellationToken ct)
    {
        if (!force && _apiKey is not null && _apiKeyExpiresAtUtc > DateTime.UtcNow.AddMinutes(2)) return _apiKey;
        await _authLock.WaitAsync(ct);
        try
        {
            if (!force && _apiKey is not null && _apiKeyExpiresAtUtc > DateTime.UtcNow.AddMinutes(2)) return _apiKey;
            try
            {
                using var response = await httpClient.PostAsJsonAsync("auth", new { clientId = _options.ClientId, clientSecret = _options.ClientSecret }, Json, ct);
                if (!response.IsSuccessStatusCode) throw new PluggyUnavailableException("Falha de autenticação na Pluggy.");
                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                if (!doc.RootElement.TryGetProperty("apiKey", out var key) || string.IsNullOrWhiteSpace(key.GetString()))
                    throw new PluggyIncompleteResponseException("A autenticação Pluggy não retornou apiKey.");
                _apiKey = key.GetString(); _apiKeyExpiresAtUtc = DateTime.UtcNow.AddMinutes(110); return _apiKey!;
            }
            catch (JsonException)
            {
                throw new PluggyIncompleteResponseException("Resposta de autenticação inválida da Pluggy.");
            }
        }
        finally { _authLock.Release(); }
    }

    private static PluggyItem ParseItem(JsonElement e) => new(
        RequiredString(e, "id"), OptionalString(e, "status") ?? "UNKNOWN",
        ReadDateTime(e, "consentExpiresAt"),
        e.TryGetProperty("connector", out var c) ? OptionalString(c, "name") : null,
        e.TryGetProperty("error", out var error) ? OptionalString(error, "code") : null);

    internal static PluggyAccount ParseAccount(JsonElement e)
    {
        var institution = e.TryGetProperty("institution", out var i) ? OptionalString(i, "name") : null;
        decimal? creditLimit = null, available = null, minimum = null;
        DateOnly? close = null, due = null;
        string? brand = null, status = null;
        if (e.TryGetProperty("creditData", out var credit) && credit.ValueKind == JsonValueKind.Object)
        {
            creditLimit = ReadDecimal(credit, "creditLimit");
            available = ReadDecimal(credit, "availableCreditLimit");
            minimum = ReadDecimal(credit, "minimumPayment");
            close = ReadLocalDate(credit, "balanceCloseDate");
            due = ReadLocalDate(credit, "balanceDueDate");
            brand = OptionalString(credit, "brand");
            status = OptionalString(credit, "status");
        }
        return new(RequiredString(e, "id"), RequiredString(e, "type"), RequiredString(e, "name"), institution,
            ReadDecimal(e, "balance") ?? throw new PluggyIncompleteResponseException("Conta sem saldo."),
            OptionalString(e, "subtype"), OptionalString(e, "number"), creditLimit, available, minimum,
            close, due, brand, status);
    }

    internal static PluggyTransaction ParseTransaction(JsonElement e, string accountId)
    {
        var date = ReadLocalDate(e, "date") ?? throw new PluggyIncompleteResponseException("Transação sem data válida.");
        string? category = null;
        if (e.TryGetProperty("category", out var cat))
            category = cat.ValueKind == JsonValueKind.String ? cat.GetString() : OptionalString(cat, "description") ?? OptionalString(cat, "name");
        int? number = null, count = null;
        string? billId = null;
        DateOnly? purchaseDate = null;
        if (e.TryGetProperty("creditCardMetadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object)
        {
            number = ReadInt(metadata, "installmentNumber");
            count = ReadInt(metadata, "totalInstallments");
            billId = OptionalString(metadata, "billId");
            purchaseDate = ReadLocalDate(metadata, "purchaseDate");
        }
        var amount = ReadDecimal(e, "amount") ?? throw new PluggyIncompleteResponseException("Transação sem valor.");
        var currency = OptionalString(e, "currencyCode");
        // Compras internacionais: "amount" vem na moeda original; o valor que entra na fatura é
        // amountInAccountCurrency (em reais).
        var accountAmount = ReadDecimal(e, "amountInAccountCurrency");
        var foreign = currency is not null && !currency.Equals("BRL", StringComparison.OrdinalIgnoreCase);
        return new(RequiredString(e, "id"), accountId,
            OptionalString(e, "description") ?? OptionalString(e, "descriptionRaw") ?? "Sem descrição",
            accountAmount ?? amount, date, OptionalString(e, "type"), OptionalString(e, "operationType"),
            OptionalString(e, "status"), category, number, count, billId, purchaseDate,
            foreign ? currency : null, foreign ? amount : null);
    }

    internal static PluggyBill ParseBill(JsonElement e, string accountId)
    {
        decimal paid = 0;
        DateOnly? lastPayment = null;
        if (e.TryGetProperty("payments", out var values) && values.ValueKind == JsonValueKind.Array)
            foreach (var payment in values.EnumerateArray())
            {
                paid += Math.Abs(ReadDecimal(payment, "amount") ?? 0);
                var date = ReadLocalDate(payment, "paymentDate");
                if (date is not null && (lastPayment is null || date > lastPayment)) lastPayment = date;
            }
        return new(RequiredString(e, "id"), accountId,
            ReadLocalDate(e, "dueDate") ?? throw new PluggyIncompleteResponseException("Fatura sem vencimento."),
            ReadLocalDate(e, "billClosingDate") ?? ReadLocalDate(e, "closingDate"),
            ReadDecimal(e, "totalAmount") ?? throw new PluggyIncompleteResponseException("Fatura sem valor total."),
            ReadDecimal(e, "minimumPaymentAmount"), paid, lastPayment);
    }

    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    /// <summary>
    /// A Pluggy envia datas em ISO8601 UTC. Meia-noite UTC exata (ou data sem fuso) é uma data
    /// "pura" e é usada como está; qualquer outro horário é convertido para o horário de Brasília,
    /// senão uma compra às 22h do dia 19 viraria dia 20 e poderia cair na fatura errada.
    /// </summary>
    internal static DateOnly? ParseLocalDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var hasOffset = OffsetSuffix().IsMatch(value);
        if (!hasOffset)
            return DateOnly.TryParseExact(value[..Math.Min(10, value.Length)], "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var plain) ? plain : null;
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var instant))
            return null;
        var utc = instant.UtcDateTime;
        if (utc.TimeOfDay == TimeSpan.Zero) return DateOnly.FromDateTime(utc);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, SaoPaulo));
    }

    [GeneratedRegex(@"T.*(Z|[+-]\d{2}:?\d{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex OffsetSuffix();

    private static DateOnly? ReadLocalDate(JsonElement e, string name) => ParseLocalDate(OptionalString(e, name));
    private static bool IsHealthyStatus(string status) => status.Equals("UPDATED",StringComparison.OrdinalIgnoreCase);
    private static string RequiredString(JsonElement e,string name)=>OptionalString(e,name) ?? throw new PluggyIncompleteResponseException($"Campo obrigatório ausente: {name}.");
    private static string? OptionalString(JsonElement e,string name)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(name,out var p)&&p.ValueKind==JsonValueKind.String?p.GetString():null;
    private static int? ReadInt(JsonElement e,string name)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(name,out var p)&&p.TryGetInt32(out var n)?n:null;
    private static decimal? ReadDecimal(JsonElement e,string name)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(name,out var p)&&p.TryGetDecimal(out var n)?n:null;
    private static DateTime? ReadDateTime(JsonElement e,string name)=>OptionalString(e,name) is string value&&DateTime.TryParse(value,null,System.Globalization.DateTimeStyles.AdjustToUniversal|System.Globalization.DateTimeStyles.AssumeUniversal,out var d)?d.ToUniversalTime():null;
}

public sealed class PluggyUnavailableException(string message) : Exception(message);
public sealed class PluggyIncompleteResponseException(string message) : Exception(message);
public sealed class PluggyConsentExpiredException(string message) : Exception(message);
