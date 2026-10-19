using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace KiaraFin.Api.Features.Notifications;

public sealed class TelegramNotificationOptions
{
    public const string SectionName = "TelegramNotifications";
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://api.telegram.org";
    public string BotToken { get; set; } = "";
    public long ChatId { get; set; }
    public string AuthorizedEmail { get; set; } = "jhoownogueira@outlook.com";
}

public sealed record AccountUpdateNotification(
    string Email,
    bool InitialImport,
    int NewTransactions,
    int UpdatedTransactions,
    int RemovedTransactions,
    int AccountsChanged,
    IReadOnlyList<string> Institutions,
    DateTime OccurredAtUtc)
{
    public bool HasChanges => InitialImport || NewTransactions > 0 || UpdatedTransactions > 0 ||
        RemovedTransactions > 0 || AccountsChanged > 0;
}

public interface IAccountNotificationService
{
    Task NotifyAsync(AccountUpdateNotification notification, CancellationToken cancellationToken);
}

public sealed class TelegramNotificationService(
    HttpClient httpClient,
    IOptions<TelegramNotificationOptions> options,
    ILogger<TelegramNotificationService> logger) : IAccountNotificationService
{
    private const long AllowedChatId = 734302182;
    private const string AllowedEmail = "jhoownogueira@outlook.com";
    private readonly TelegramNotificationOptions _options = options.Value;

    public async Task NotifyAsync(AccountUpdateNotification notification, CancellationToken cancellationToken)
    {
        if (!_options.Enabled || !notification.HasChanges ||
            _options.ChatId != AllowedChatId ||
            !string.Equals(_options.AuthorizedEmail, AllowedEmail, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(notification.Email, AllowedEmail, StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                $"bot{_options.BotToken}/sendMessage",
                new
                {
                    chat_id = AllowedChatId,
                    text = BuildMessage(notification),
                    parse_mode = "HTML",
                    disable_web_page_preview = true
                },
                cancellationToken);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Telegram notification failed with HTTP {StatusCode}", (int)response.StatusCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (HttpRequestException)
        {
            logger.LogWarning("Telegram notification could not reach the Bot API");
        }
    }

    private static string BuildMessage(AccountUpdateNotification value)
    {
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(value.OccurredAtUtc, DateTimeKind.Utc),
            TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"));
        var lines = new List<string>
        {
            value.InitialImport ? "💚 <b>KiaraFin conectado!</b>" : "💚 <b>KiaraFin atualizado!</b>",
            "",
            value.InitialImport
                ? "🎉 Sua importação inicial do Open Finance foi concluída."
                : "🏦 O Open Finance trouxe novidades para sua conta."
        };
        if (value.Institutions.Count > 0)
            lines.Add($"🏛️ {string.Join(", ", value.Institutions.Select(WebUtility.HtmlEncode))}");
        lines.Add("");
        if (value.AccountsChanged > 0) lines.Add($"💳 {FormatCount(value.AccountsChanged, "conta ou cartão atualizado", "contas ou cartões atualizados")}");
        if (value.NewTransactions > 0) lines.Add($"✨ {FormatCount(value.NewTransactions, "novo lançamento", "novos lançamentos")}");
        if (value.UpdatedTransactions > 0) lines.Add($"🔄 {FormatCount(value.UpdatedTransactions, "lançamento atualizado", "lançamentos atualizados")}");
        if (value.RemovedTransactions > 0) lines.Add($"🗑️ {FormatCount(value.RemovedTransactions, "lançamento removido", "lançamentos removidos")}");
        lines.Add("");
        lines.Add($"🕒 {localTime:dd/MM/yyyy 'às' HH:mm}");
        lines.Add("🔐 Notificação privada enviada somente para você.");
        return string.Join('\n', lines);
    }

    private static string FormatCount(int count, string singular, string plural) =>
        $"<b>{count}</b> {(count == 1 ? singular : plural)}";
}
