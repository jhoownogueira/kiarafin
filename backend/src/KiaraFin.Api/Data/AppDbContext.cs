using KiaraFin.Api.Features.Cards;
using KiaraFin.Api.Features.Finance;
using KiaraFin.Api.Features.Forecasts;
using KiaraFin.Api.Features.Identity;
using KiaraFin.Api.Features.Pluggy;
using Microsoft.EntityFrameworkCore;

namespace KiaraFin.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMembership> HouseholdMemberships => Set<HouseholdMembership>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<FinancialAccount> FinancialAccounts => Set<FinancialAccount>();
    public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<CreditCard> CreditCards => Set<CreditCard>();
    public DbSet<CardBill> CardBills => Set<CardBill>();
    public DbSet<CardTransaction> CardTransactions => Set<CardTransaction>();
    public DbSet<FinancialIntegration> FinancialIntegrations => Set<FinancialIntegration>();
    public DbSet<FinancialIntegrationSync> FinancialIntegrationSyncs => Set<FinancialIntegrationSync>();
    public DbSet<PluggySyncJob> PluggySyncJobs => Set<PluggySyncJob>();
    public DbSet<ForecastEntry> ForecastEntries => Set<ForecastEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
