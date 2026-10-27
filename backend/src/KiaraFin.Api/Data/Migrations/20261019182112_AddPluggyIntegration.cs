using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KiaraFin.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPluggyIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateOnly>(
                name: "CycleStartDate",
                table: "invoices",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "ClosingDate",
                table: "invoices",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "invoices",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "InstitutionControlled",
                table: "invoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SourceProvider",
                table: "invoices",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "financial_transactions",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "InstitutionControlled",
                table: "financial_transactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRefund",
                table: "financial_transactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SourceProvider",
                table: "financial_transactions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "financial_accounts",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "InstitutionControlled",
                table: "financial_accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SourceProvider",
                table: "financial_accounts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalLimit",
                table: "credit_cards",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<int>(
                name: "DueDay",
                table: "credit_cards",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<Guid>(
                name: "DefaultPaymentAccountId",
                table: "credit_cards",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "ClosingDay",
                table: "credit_cards",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<decimal>(
                name: "AvailableLimit",
                table: "credit_cards",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentBalance",
                table: "credit_cards",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "credit_cards",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "InstitutionControlled",
                table: "credit_cards",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SourceProvider",
                table: "credit_cards",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "financial_integrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ItemId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ActivatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSuccessfulSyncAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AccountCount = table.Column<int>(type: "integer", nullable: false),
                    TransactionCount = table.Column<int>(type: "integer", nullable: false),
                    ConsentExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    InitialImportCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_integrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_financial_integrations_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_financial_integrations_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "external_financial_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IntegrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancialAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreditCardId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalAccountId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    AccountType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    InstitutionName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_financial_accounts", x => x.Id);
                    table.CheckConstraint("CK_external_financial_accounts_single_target", "(\"FinancialAccountId\" IS NOT NULL AND \"CreditCardId\" IS NULL) OR (\"FinancialAccountId\" IS NULL AND \"CreditCardId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_external_financial_accounts_credit_cards_CreditCardId",
                        column: x => x.CreditCardId,
                        principalTable: "credit_cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_external_financial_accounts_financial_accounts_FinancialAcc~",
                        column: x => x.FinancialAccountId,
                        principalTable: "financial_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_external_financial_accounts_financial_integrations_Integrat~",
                        column: x => x.IntegrationId,
                        principalTable: "financial_integrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "financial_integration_syncs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IntegrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Trigger = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: true),
                    InsertedCount = table.Column<int>(type: "integer", nullable: false),
                    UpdatedCount = table.Column<int>(type: "integer", nullable: false),
                    DeletedCount = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_integration_syncs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_financial_integration_syncs_financial_integrations_Integrat~",
                        column: x => x.IntegrationId,
                        principalTable: "financial_integrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pluggy_sync_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IntegrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Trigger = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DeduplicationKey = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AvailableAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pluggy_sync_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pluggy_sync_jobs_financial_integrations_IntegrationId",
                        column: x => x.IntegrationId,
                        principalTable: "financial_integrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "external_transaction_mappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IntegrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalTransactionId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    FinancialTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceUpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProviderId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ProviderCode = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    TransactionDateTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_transaction_mappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_external_transaction_mappings_external_financial_accounts_E~",
                        column: x => x.ExternalAccountId,
                        principalTable: "external_financial_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_external_transaction_mappings_financial_integrations_Integr~",
                        column: x => x.IntegrationId,
                        principalTable: "financial_integrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_external_transaction_mappings_financial_transactions_Financ~",
                        column: x => x.FinancialTransactionId,
                        principalTable: "financial_transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_SourceProvider_ExternalId",
                table: "invoices",
                columns: new[] { "SourceProvider", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_SourceProvider_ExternalId",
                table: "financial_transactions",
                columns: new[] { "SourceProvider", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_accounts_SourceProvider_ExternalId",
                table: "financial_accounts",
                columns: new[] { "SourceProvider", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_credit_cards_SourceProvider_ExternalId",
                table: "credit_cards",
                columns: new[] { "SourceProvider", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_external_financial_accounts_CreditCardId",
                table: "external_financial_accounts",
                column: "CreditCardId",
                unique: true,
                filter: "\"CreditCardId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_external_financial_accounts_FinancialAccountId",
                table: "external_financial_accounts",
                column: "FinancialAccountId",
                unique: true,
                filter: "\"FinancialAccountId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_external_financial_accounts_IntegrationId_ExternalAccountId",
                table: "external_financial_accounts",
                columns: new[] { "IntegrationId", "ExternalAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_external_transaction_mappings_ExternalAccountId",
                table: "external_transaction_mappings",
                column: "ExternalAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_external_transaction_mappings_FinancialTransactionId",
                table: "external_transaction_mappings",
                column: "FinancialTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_external_transaction_mappings_IntegrationId_ExternalTransac~",
                table: "external_transaction_mappings",
                columns: new[] { "IntegrationId", "ExternalTransactionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_external_transaction_mappings_IntegrationId_ProviderId",
                table: "external_transaction_mappings",
                columns: new[] { "IntegrationId", "ProviderId" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_integration_syncs_IntegrationId_StartedAtUtc",
                table: "financial_integration_syncs",
                columns: new[] { "IntegrationId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_integrations_HouseholdId",
                table: "financial_integrations",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_financial_integrations_Provider_ItemId",
                table: "financial_integrations",
                columns: new[] { "Provider", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_integrations_UserId_Provider",
                table: "financial_integrations",
                columns: new[] { "UserId", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pluggy_sync_jobs_DeduplicationKey",
                table: "pluggy_sync_jobs",
                column: "DeduplicationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pluggy_sync_jobs_IntegrationId",
                table: "pluggy_sync_jobs",
                column: "IntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_pluggy_sync_jobs_Status_AvailableAtUtc",
                table: "pluggy_sync_jobs",
                columns: new[] { "Status", "AvailableAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM credit_cards WHERE "DefaultPaymentAccountId" IS NULL) THEN
                        RAISE EXCEPTION 'Não é possível reverter AddPluggyIntegration enquanto existirem cartões sem conta padrão.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropTable(
                name: "external_transaction_mappings");

            migrationBuilder.DropTable(
                name: "financial_integration_syncs");

            migrationBuilder.DropTable(
                name: "pluggy_sync_jobs");

            migrationBuilder.DropTable(
                name: "external_financial_accounts");

            migrationBuilder.DropTable(
                name: "financial_integrations");

            migrationBuilder.DropIndex(
                name: "IX_invoices_SourceProvider_ExternalId",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_financial_transactions_SourceProvider_ExternalId",
                table: "financial_transactions");

            migrationBuilder.DropIndex(
                name: "IX_financial_accounts_SourceProvider_ExternalId",
                table: "financial_accounts");

            migrationBuilder.DropIndex(
                name: "IX_credit_cards_SourceProvider_ExternalId",
                table: "credit_cards");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "InstitutionControlled",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "SourceProvider",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "financial_transactions");

            migrationBuilder.DropColumn(
                name: "InstitutionControlled",
                table: "financial_transactions");

            migrationBuilder.DropColumn(
                name: "IsRefund",
                table: "financial_transactions");

            migrationBuilder.DropColumn(
                name: "SourceProvider",
                table: "financial_transactions");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "financial_accounts");

            migrationBuilder.DropColumn(
                name: "InstitutionControlled",
                table: "financial_accounts");

            migrationBuilder.DropColumn(
                name: "SourceProvider",
                table: "financial_accounts");

            migrationBuilder.DropColumn(
                name: "AvailableLimit",
                table: "credit_cards");

            migrationBuilder.DropColumn(
                name: "CurrentBalance",
                table: "credit_cards");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "credit_cards");

            migrationBuilder.DropColumn(
                name: "InstitutionControlled",
                table: "credit_cards");

            migrationBuilder.DropColumn(
                name: "SourceProvider",
                table: "credit_cards");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "CycleStartDate",
                table: "invoices",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "ClosingDate",
                table: "invoices",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalLimit",
                table: "credit_cards",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "DueDay",
                table: "credit_cards",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "DefaultPaymentAccountId",
                table: "credit_cards",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ClosingDay",
                table: "credit_cards",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
