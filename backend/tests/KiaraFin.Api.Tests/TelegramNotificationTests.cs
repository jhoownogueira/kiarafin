using System.Net;
using System.Text.Json;
using KiaraFin.Api.Features.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KiaraFin.Api.Tests;

public sealed class TelegramNotificationTests
{
    [Fact]
    public async Task Sends_a_formatted_update_only_to_the_authorized_chat()
    {
        var handler = new CapturingHandler();
        var service = CreateService(handler);

        await service.NotifyAsync(new AccountUpdateNotification(
            "jhoownogueira@outlook.com", false, 2, 1, 0, 1,
            ["Meu & Banco"], new DateTime(2026, 10, 19, 20, 2, 0, DateTimeKind.Utc)), default);

        Assert.Equal("/botplaceholder-token/sendMessage", handler.Path);
        using var body = JsonDocument.Parse(handler.Body);
        Assert.Equal(734302182, body.RootElement.GetProperty("chat_id").GetInt64());
        var text = body.RootElement.GetProperty("text").GetString();
        Assert.Contains("💚", text, StringComparison.Ordinal);
        Assert.Contains("Meu &amp; Banco", text, StringComparison.Ordinal);
        Assert.Contains("2</b> novos lançamentos", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("outra-pessoa@example.com", 734302182)]
    [InlineData("jhoownogueira@outlook.com", 999)]
    public async Task Does_not_send_outside_the_authorized_identity(string email, long chatId)
    {
        var handler = new CapturingHandler();
        var service = CreateService(handler, chatId);

        await service.NotifyAsync(new AccountUpdateNotification(
            email, false, 1, 0, 0, 0, [], DateTime.UtcNow), default);

        Assert.Equal(0, handler.CallCount);
    }

    private static TelegramNotificationService CreateService(CapturingHandler handler, long chatId = 734302182)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.telegram.test/") };
        var options = Options.Create(new TelegramNotificationOptions
        {
            Enabled = true,
            BotToken = "placeholder-token",
            ChatId = chatId,
            AuthorizedEmail = "jhoownogueira@outlook.com"
        });
        return new TelegramNotificationService(client, options, NullLogger<TelegramNotificationService>.Instance);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public string? Path { get; private set; }
        public string Body { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            Path = request.RequestUri?.AbsolutePath;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
