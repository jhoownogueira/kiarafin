using KiaraFin.Api.Data;
using KiaraFin.Api.Features.Dashboard;
using KiaraFin.Api.Features.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KiaraFin.Api.Features.Notifications;

public sealed class ApplicationStartupNotificationService(
    IServiceScopeFactory scopeFactory,
    IAccountNotificationService notifications,
    IOptions<TelegramNotificationOptions> options,
    ILogger<ApplicationStartupNotificationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return;

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dashboard = scope.ServiceProvider.GetRequiredService<DashboardService>();
            var normalizedEmail = options.Value.AuthorizedEmail.Trim().ToUpperInvariant();
            var user = await db.Users.AsNoTracking()
                .SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);
            if (user is null)
            {
                logger.LogWarning("Startup notification skipped because the authorized user was not found");
                return;
            }

            var householdId = await db.HouseholdMemberships.AsNoTracking()
                .Where(x => x.UserId == user.Id && x.Role == HouseholdRole.Owner)
                .Select(x => x.HouseholdId)
                .SingleOrDefaultAsync(cancellationToken);
            if (householdId == Guid.Empty)
            {
                logger.LogWarning("Startup notification skipped because the authorized household was not found");
                return;
            }

            var analysis = await dashboard.GetBalanceForecastAsync(householdId, cancellationToken);
            var current = analysis.Months[0];
            await notifications.NotifyForecastAsync(new BalanceForecastNotification(
                user.Email, BalanceForecastNotificationReason.ApplicationStarted,
                current.Salary, current.ExtraIncome, current.FixedExpenses,
                current.CardInvoices + current.EstimatedCardInvoices,
                current.Net, null, DateTime.UtcNow), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "KiaraFin started, but the Telegram startup notification failed");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
