using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KiaraFin.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBalanceForecasts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "forecast_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpectedDay = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_forecast_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_forecast_entries_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_forecast_entries_HouseholdId_Type",
                table: "forecast_entries",
                columns: new[] { "HouseholdId", "Type" });

            migrationBuilder.Sql("""
                INSERT INTO forecast_entries
                    ("Id", "HouseholdId", "Name", "Type", "Amount", "ExpectedDay", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT
                    "Id", "HouseholdId", "Name",
                    CASE WHEN "Type" = 'Income' THEN 'ExtraIncome' ELSE 'FixedExpense' END,
                    "ExpectedAmount", "DueDay", "CreatedAtUtc", "UpdatedAtUtc"
                FROM recurring_transactions
                WHERE "Status" = 'Active';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "forecast_entries");
        }
    }
}
