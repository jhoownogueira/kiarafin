using KiaraFin.Api.Features.Identity;
using KiaraFin.Api.Features.Finance;
using KiaraFin.Api.Features.Pluggy;
using KiaraFin.Api.Features.Forecasts;
using Microsoft.EntityFrameworkCore;

namespace KiaraFin.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMembership> HouseholdMemberships => Set<HouseholdMembership>();
    public DbSet<HouseholdInvitation> HouseholdInvitations => Set<HouseholdInvitation>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<FinancialAccount> FinancialAccounts => Set<FinancialAccount>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<FinancialTransaction> FinancialTransactions => Set<FinancialTransaction>();
    public DbSet<AccountMovement> AccountMovements => Set<AccountMovement>();
    public DbSet<RecurringTransaction> RecurringTransactions => Set<RecurringTransaction>();
    public DbSet<CreditCard> CreditCards => Set<CreditCard>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InstallmentPlan> InstallmentPlans => Set<InstallmentPlan>();
    public DbSet<CreditCardSubscription> CreditCardSubscriptions => Set<CreditCardSubscription>();
    public DbSet<FinancialIntegration> FinancialIntegrations => Set<FinancialIntegration>();
    public DbSet<ExternalFinancialAccount> ExternalFinancialAccounts => Set<ExternalFinancialAccount>();
    public DbSet<ExternalTransactionMapping> ExternalTransactionMappings => Set<ExternalTransactionMapping>();
    public DbSet<FinancialIntegrationSync> FinancialIntegrationSyncs => Set<FinancialIntegrationSync>();
    public DbSet<PluggySyncJob> PluggySyncJobs => Set<PluggySyncJob>();
    public DbSet<ForecastEntry> ForecastEntries => Set<ForecastEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
