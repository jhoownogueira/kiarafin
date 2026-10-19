using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KiaraFin.Api.Common.Exceptions;
using KiaraFin.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KiaraFin.Api.Features.Pluggy;

[ApiController, Authorize, Route("api/integrations/pluggy")]
public sealed class PluggyController(PluggyIntegrationService service, AppDbContext db) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<PluggyStatusResponse>> Status(CancellationToken ct) => Ok(await service.GetStatusAsync(ct));

    [HttpPost("preview")]
    public async Task<ActionResult<PluggyPreviewResponse>> Preview(CancellationToken ct) => Ok(await service.PreviewAsync(ct));

    [HttpPost("activate")]
    public async Task<ActionResult<PluggyStatusResponse>> Activate(ActivatePluggyRequest request, CancellationToken ct)
    {
        await service.ActivateAsync(request.Confirmed, ct);
        return Ok(await service.GetStatusAsync(ct));
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync(CancellationToken ct)
    {
        var status = await service.GetStatusAsync(ct);
        if (!status.InitialImportCompleted || status.ItemId is null) throw new ConflictException("A integração ainda não foi ativada.");
        var integration = await db.FinancialIntegrations.SingleAsync(x => x.ItemId == status.ItemId && x.Provider == "PLUGGY", ct);
        var bucket = DateTime.UtcNow.ToString("yyyyMMddHH");
        var key = $"manual:{integration.Id}:{bucket}";
        await EnqueueOnceAsync(db, new PluggySyncJob(integration.Id, "manual", key, null), ct);
        return Accepted();
    }

    internal static async Task EnqueueOnceAsync(AppDbContext context, PluggySyncJob job, CancellationToken ct)
    {
        if (await context.PluggySyncJobs.AnyAsync(x => x.DeduplicationKey == job.DeduplicationKey, ct)) return;
        context.PluggySyncJobs.Add(job);
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            context.Entry(job).State = EntityState.Detached;
            if (!await context.PluggySyncJobs.AnyAsync(x => x.DeduplicationKey == job.DeduplicationKey, ct)) throw;
        }
    }
}

[ApiController, AllowAnonymous, Route("api/integrations/pluggy/webhook")]
public sealed class PluggyWebhookController(AppDbContext db, IOptions<PluggyOptions> options) : ControllerBase
{
    private static readonly HashSet<string> SupportedEvents = new(StringComparer.OrdinalIgnoreCase)
        { "item/updated", "item/error", "transactions/created", "transactions/updated", "transactions/deleted" };

    [HttpPost, RequestSizeLimit(65_536)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        var supplied = Request.Headers["X-Pluggy-Webhook-Secret"].ToString();
        if (!FixedTimeEquals(supplied, options.Value.WebhookSecret)) return Unauthorized();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8); var payload = await reader.ReadToEndAsync(ct);
        JsonDocument document; try { document = JsonDocument.Parse(payload); } catch (JsonException) { return BadRequest(); }
        using (document)
        {
            var root = document.RootElement;
            var eventName = Read(root, "event") ?? Read(root, "eventType");
            var itemId = Read(root, "itemId") ?? (root.TryGetProperty("data", out var data) ? Read(data, "itemId") : null);
            if (eventName is null || itemId is null || !SupportedEvents.Contains(eventName)) return Accepted();
            var integration = await db.FinancialIntegrations.SingleOrDefaultAsync(x => x.Provider == "PLUGGY" && x.ItemId == itemId && x.InitialImportCompleted, ct);
            if (integration is null) return Unauthorized();
            var eventId = Read(root, "id") ?? Read(root, "eventId") ?? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
            var key = $"webhook:{eventId}";
            await PluggyController.EnqueueOnceAsync(db, new PluggySyncJob(integration.Id, eventName, key, payload), ct);
            return Accepted();
        }
    }
    private static string? Read(JsonElement e, string name) => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
    private static bool FixedTimeEquals(string supplied, string expected)
    {
        if (string.IsNullOrWhiteSpace(supplied) || string.IsNullOrWhiteSpace(expected)) return false;
        var a = SHA256.HashData(Encoding.UTF8.GetBytes(supplied)); var b = SHA256.HashData(Encoding.UTF8.GetBytes(expected)); return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
