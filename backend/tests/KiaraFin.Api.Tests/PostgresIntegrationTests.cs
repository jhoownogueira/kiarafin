using KiaraFin.Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace KiaraFin.Api.Tests;

public sealed class PostgresIntegrationTests
{
    [Fact]
    public async Task InitialMigration_IsApplicableToIsolatedPostgresAndUsesMoneyPrecision()
    {
        var connectionString = Environment.GetEnvironmentVariable("KIARAFIN_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var parsed = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Contains("test", parsed.Database, StringComparison.OrdinalIgnoreCase);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using (var db = new AppDbContext(options))
            await db.Database.MigrateAsync();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select numeric_precision, numeric_scale
            from information_schema.columns
            where table_schema = 'public'
              and table_name = 'financial_transactions'
              and column_name = 'Amount'
            """;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(18, reader.GetInt32(0));
        Assert.Equal(2, reader.GetInt32(1));
    }
}
