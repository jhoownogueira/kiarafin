using System.Text;
using KiaraFin.Api.Data;
using KiaraFin.Api.Features.Identity;
using KiaraFin.Api.Features.Pluggy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace KiaraFin.Api.Tests;

public sealed class PluggyWebhookTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("wrong")]
    public async Task Missing_or_invalid_secret_is_rejected(string? supplied)
    {
        await using var db = CreateDatabase();
        var result = await ReceiveAsync(db, supplied, "{\"event\":\"item/updated\",\"itemId\":\"item-1\"}");
        Assert.IsType<UnauthorizedResult>(result);
        Assert.Empty(await db.PluggySyncJobs.ToListAsync());
    }

    [Fact]
    public async Task Unknown_item_is_rejected()
    {
        await using var db = CreateDatabase();
        var result = await ReceiveAsync(db, "webhook-secret",
            "{\"event\":\"item/updated\",\"itemId\":\"unknown\"}");
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Repeated_event_is_idempotent()
    {
        await using var db = CreateDatabase();
        var user = new User("Owner", "jhoownogueira@outlook.com", "JHOOWNOGUEIRA@OUTLOOK.COM", "hash");
        var household = new Household("Casa", user.Id);
        var integration = new FinancialIntegration(user.Id, household.Id, "PLUGGY", "item-1");
        integration.Complete(1, 1, DateTime.UtcNow.AddMonths(6), DateTime.UtcNow);
        db.AddRange(user, household, new HouseholdMembership(household.Id, user.Id, HouseholdRole.Owner), integration);
        await db.SaveChangesAsync();
        const string payload = "{\"id\":\"event-1\",\"event\":\"transactions/updated\",\"itemId\":\"item-1\"}";

        Assert.IsType<AcceptedResult>(await ReceiveAsync(db, "webhook-secret", payload));
        Assert.IsType<AcceptedResult>(await ReceiveAsync(db, "webhook-secret", payload));
        Assert.Single(await db.PluggySyncJobs.ToListAsync());
    }

    private static AppDbContext CreateDatabase() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"webhook-{Guid.NewGuid():N}").Options);

    private static async Task<IActionResult> ReceiveAsync(AppDbContext db, string? secret, string payload)
    {
        var controller = new PluggyWebhookController(db, Options.Create(new PluggyOptions { WebhookSecret = "webhook-secret" }));
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        if (secret is not null) context.Request.Headers["X-Pluggy-Webhook-Secret"] = secret;
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return await controller.Receive(default);
    }
}
