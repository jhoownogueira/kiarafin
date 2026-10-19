using System.Net;
using System.Text;
using KiaraFin.Api.Features.Pluggy;
using Microsoft.Extensions.Options;
using Xunit;

namespace KiaraFin.Api.Tests;

public sealed class PluggyClientTests
{
    [Fact]
    public async Task Invalid_credentials_are_rejected_without_exposing_secrets()
    {
        var handler = new DelegateHandler(_ => Response(HttpStatusCode.Unauthorized, "{}"));
        var client = CreateClient(handler);

        var error = await Assert.ThrowsAsync<PluggyUnavailableException>(() =>
            client.ReadCompleteSnapshotAsync("item-1", default));

        Assert.Contains("autenticação", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unauthorized_api_key_is_renewed_once_and_snapshot_completes()
    {
        var authCalls = 0;
        var handler = new DelegateHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/auth"))
                return Response(HttpStatusCode.OK, $"{{\"apiKey\":\"key-{++authCalls}\"}}");
            var key = request.Headers.GetValues("X-API-KEY").Single();
            if (request.RequestUri.AbsolutePath.EndsWith("/items/item-1") && key == "key-1")
                return Response(HttpStatusCode.Unauthorized, "{}");
            if (request.RequestUri.AbsolutePath.EndsWith("/items/item-1")) return Response(HttpStatusCode.OK, ItemJson);
            if (request.RequestUri.AbsolutePath.EndsWith("/accounts")) return Response(HttpStatusCode.OK, AccountsJson);
            if (request.RequestUri.AbsolutePath.EndsWith("/transactions")) return Response(HttpStatusCode.OK, EmptyPageJson);
            return Response(HttpStatusCode.NotFound, "{}");
        });

        var snapshot = await CreateClient(handler).ReadCompleteSnapshotAsync("item-1", default);

        Assert.Equal(2, authCalls);
        Assert.Single(snapshot.Accounts);
        Assert.Empty(snapshot.Transactions);
    }

    [Fact]
    public async Task Incomplete_pagination_is_rejected()
    {
        var handler = new DelegateHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/auth")) return Response(HttpStatusCode.OK, "{\"apiKey\":\"key\"}");
            if (request.RequestUri.AbsolutePath.EndsWith("/items/item-1")) return Response(HttpStatusCode.OK, ItemJson);
            if (request.RequestUri.AbsolutePath.EndsWith("/accounts"))
                return Response(HttpStatusCode.OK, "{\"page\":1,\"total\":2,\"totalPages\":2,\"results\":[]}");
            return Response(HttpStatusCode.NotFound, "{}");
        });

        await Assert.ThrowsAsync<PluggyIncompleteResponseException>(() =>
            CreateClient(handler).ReadCompleteSnapshotAsync("item-1", default));
    }

    [Fact]
    public async Task Missing_bills_resource_does_not_block_credit_card_transactions()
    {
        var handler = new DelegateHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/auth")) return Response(HttpStatusCode.OK, "{\"apiKey\":\"key\"}");
            if (request.RequestUri.AbsolutePath.EndsWith("/items/item-1")) return Response(HttpStatusCode.OK, ItemJson);
            if (request.RequestUri.AbsolutePath.EndsWith("/accounts")) return Response(HttpStatusCode.OK, CreditAccountsJson);
            if (request.RequestUri.AbsolutePath.EndsWith("/transactions")) return Response(HttpStatusCode.OK, EmptyPageJson);
            if (request.RequestUri.AbsolutePath.EndsWith("/bills")) return Response(HttpStatusCode.NotFound, "{}");
            return Response(HttpStatusCode.NotFound, "{}");
        });

        var snapshot = await CreateClient(handler).ReadCompleteSnapshotAsync("item-1", default);

        Assert.Single(snapshot.Accounts);
        Assert.Empty(snapshot.CreditCardBills);
    }

    private static PluggyClient CreateClient(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.pluggy.test/") };
        return new PluggyClient(http, Options.Create(new PluggyOptions
        {
            Enabled = true, ClientId = "client", ClientSecret = "secret", ItemId = "item-1"
        }));
    }

    private static HttpResponseMessage Response(HttpStatusCode status, string json) => new(status)
    { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private const string ItemJson = """
        {"id":"item-1","status":"UPDATED","consentExpiresAt":"2099-01-01T00:00:00Z","connector":{"name":"Meu Pluggy"}}
        """;
    private const string AccountsJson = """
        {"page":1,"total":1,"totalPages":1,"results":[{"id":"account-1","type":"BANK","subtype":"CHECKING_ACCOUNT","name":"Conta","balance":100}]}
        """;
    private const string CreditAccountsJson = """
        {"page":1,"total":1,"totalPages":1,"results":[{"id":"card-1","type":"CREDIT","name":"Cartão","balance":100}]}
        """;
    private const string EmptyPageJson = """{"page":1,"total":0,"totalPages":1,"results":[]}""";

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response(request));
    }
}
