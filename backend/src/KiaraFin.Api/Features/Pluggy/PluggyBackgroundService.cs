using System.Data;
using KiaraFin.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KiaraFin.Api.Features.Pluggy;

public sealed class PluggyBackgroundService(IServiceScopeFactory scopes, IOptions<PluggyOptions> options,
    ILogger<PluggyBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        do { await TickAsync(stoppingToken); } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task TickAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var now = DateTime.UtcNow;
            var due = await db.FinancialIntegrations.Where(x => x.InitialImportCompleted && x.Status != FinancialIntegrationStatus.ConsentExpired &&
                (x.LastSuccessfulSyncAtUtc == null || x.LastSuccessfulSyncAtUtc < now.AddHours(-options.Value.SyncIntervalHours))).ToListAsync(ct);
            foreach (var integration in due)
            {
                var intervalTicks = TimeSpan.FromHours(options.Value.SyncIntervalHours).Ticks;
                var bucket = now.Ticks / intervalTicks; var key = $"schedule:{integration.Id}:{bucket}";
                if (!await db.PluggySyncJobs.AnyAsync(x => x.DeduplicationKey == key, ct)) db.PluggySyncJobs.Add(new PluggySyncJob(integration.Id, "scheduler", key, null));
            }
            await db.SaveChangesAsync(ct);
            var job = await ClaimNextJobAsync(db, now, ct);
            if (job is null) return;
            try { await scope.ServiceProvider.GetRequiredService<PluggyIntegrationService>().SynchronizeAsync(job.IntegrationId, job.Trigger, ct, job.Payload); job.Complete(DateTime.UtcNow); }
            catch (Exception ex)
            {
                db.ChangeTracker.Clear();
                job = await db.PluggySyncJobs.SingleAsync(x => x.Id == job.Id, ct);
                job.Retry("Falha temporária na sincronização.", DateTime.UtcNow.AddMinutes(Math.Min(60, Math.Pow(2, job.Attempts))));
                logger.LogWarning(ex, "Pluggy job {JobId} failed", job.Id);
            }
            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) { logger.LogError(ex, "Pluggy background tick failed"); }
    }

    private static async Task<PluggySyncJob?> ClaimNextJobAsync(AppDbContext db, DateTime now, CancellationToken ct)
    {
        if (!db.Database.IsRelational())
        {
            var memoryJob = await db.PluggySyncJobs
                .Where(x => (x.Status == PluggyJobStatus.Pending || x.Status == PluggyJobStatus.Processing) && x.AvailableAtUtc <= now)
                .OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
            if (memoryJob is not null) { memoryJob.Start(now); await db.SaveChangesAsync(ct); }
            return memoryJob;
        }
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var job = await db.PluggySyncJobs.FromSqlInterpolated($$"""
            SELECT * FROM pluggy_sync_jobs
            WHERE "AvailableAtUtc" <= {{now}}
              AND "Status" IN ('Pending', 'Processing')
            ORDER BY "CreatedAtUtc"
            FOR UPDATE SKIP LOCKED
            LIMIT 1
            """).FirstOrDefaultAsync(ct);
        if (job is not null) { job.Start(now); await db.SaveChangesAsync(ct); }
        await transaction.CommitAsync(ct);
        return job;
    }
}
