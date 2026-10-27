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

    [Fact]
    public async Task Null_item_error_is_accepted()
    {
        var handler = new DelegateHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/auth")) return Response(HttpStatusCode.OK, "{\"apiKey\":\"key\"}");
            if (request.RequestUri.AbsolutePath.EndsWith("/items/item-1")) return Response(HttpStatusCode.OK, ItemWithNullErrorJson);
            if (request.RequestUri.AbsolutePath.EndsWith("/accounts")) return Response(HttpStatusCode.OK, AccountsJson);
            if (request.RequestUri.AbsolutePath.EndsWith("/transactions")) return Response(HttpStatusCode.OK, EmptyPageJson);
            return Response(HttpStatusCode.NotFound, "{}");
        });

        var snapshot = await CreateClient(handler).ReadCompleteSnapshotAsync("item-1", default);

        Assert.Null(snapshot.Item.ErrorCode);
    }

    [Fact]
    public async Task Transactions_are_read_from_v2_with_cursor_pagination()
    {
        var requestedTransactionPaths = new List<string>();
        var handler = new DelegateHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/auth")) return Response(HttpStatusCode.OK, "{\"apiKey\":\"key\"}");
            if (request.RequestUri.AbsolutePath.EndsWith("/items/item-1")) return Response(HttpStatusCode.OK, ItemJson);
            if (request.RequestUri.AbsolutePath.EndsWith("/accounts")) return Response(HttpStatusCode.OK, AccountsJson);
            if (request.RequestUri.AbsolutePath == "/v2/transactions")
            {
                requestedTransactionPaths.Add(request.RequestUri.PathAndQuery);
                return request.RequestUri.Query.Contains("after=cursor-1", StringComparison.Ordinal)
                    ? Response(HttpStatusCode.OK, TransactionCursorLastPageJson)
                    : Response(HttpStatusCode.OK, TransactionCursorFirstPageJson);
            }
            return Response(HttpStatusCode.NotFound, "{}");
        });

        var snapshot = await CreateClient(handler).ReadCompleteSnapshotAsync("item-1", default);

        Assert.Equal(2, snapshot.Transactions.Count);
        Assert.Equal(
            ["/v2/transactions?accountId=account-1", "/v2/transactions?accountId=account-1&after=cursor-1"],
            requestedTransactionPaths);
    }

    [Fact]
    public async Task Network_failure_is_reported_as_pluggy_unavailable()
    {
        var client = CreateClient(new ThrowingHandler(new HttpRequestException("sensitive network detail")));

        var error = await Assert.ThrowsAsync<PluggyUnavailableException>(() =>
            client.ReadCompleteSnapshotAsync("item-1", default));

        Assert.Equal("Não foi possível acessar a API da Pluggy.", error.Message);
        Assert.DoesNotContain("sensitive", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Invalid_authentication_json_is_reported_as_incomplete_response()
    {
        var handler = new DelegateHandler(_ => Response(HttpStatusCode.OK, "not-json"));

        var error = await Assert.ThrowsAsync<PluggyIncompleteResponseException>(() =>
            CreateClient(handler).ReadCompleteSnapshotAsync("item-1", default));

        Assert.Contains("autenticação inválida", error.Message, StringComparison.OrdinalIgnoreCase);
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
    private const string ItemWithNullErrorJson = """
        {"id":"item-1","status":"UPDATED","consentExpiresAt":"2099-01-01T00:00:00Z","connector":{"name":"Meu Pluggy"},"error":null}
        """;
    private const string AccountsJson = """
        {"page":1,"total":1,"totalPages":1,"results":[{"id":"account-1","type":"BANK","subtype":"CHECKING_ACCOUNT","name":"Conta","balance":100}]}
        """;
    private const string CreditAccountsJson = """
        {"page":1,"total":1,"totalPages":1,"results":[{"id":"card-1","type":"CREDIT","name":"Cartão","balance":100}]}
        """;
    private const string EmptyPageJson = """{"page":1,"total":0,"totalPages":1,"results":[],"next":null}""";
    private const string TransactionCursorFirstPageJson = """
        {"results":[{"id":"transaction-1","date":"2026-10-18","description":"Compra 1","amount":10}],"next":"?accountId=account-1&after=cursor-1"}
        """;
    private const string TransactionCursorLastPageJson = """
        {"results":[{"id":"transaction-2","date":"2026-10-19","description":"Compra 2","amount":20}],"next":null}
        """;

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response(request));
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
    }
}
