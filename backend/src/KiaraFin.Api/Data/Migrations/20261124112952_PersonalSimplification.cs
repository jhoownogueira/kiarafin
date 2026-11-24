using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KiaraFin.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PersonalSimplification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_financial_accounts_users_ResponsibleUserId",
                table: "financial_accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_invoices_financial_transactions_PaymentTransactionId",
                table: "invoices");

            migrationBuilder.DropTable(
                name: "account_movements");

            migrationBuilder.DropTable(
                name: "external_transaction_mappings");

            migrationBuilder.DropTable(
                name: "household_invitations");

            migrationBuilder.DropTable(
                name: "external_financial_accounts");

            migrationBuilder.DropTable(
                name: "financial_transactions");

            migrationBuilder.DropTable(
                name: "credit_card_subscriptions");

            migrationBuilder.DropTable(
                name: "installment_plans");

            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropTable(
                name: "recurring_transactions");

            migrationBuilder.DropTable(
                name: "credit_cards");

            migrationBuilder.DropIndex(
                name: "IX_financial_accounts_ResponsibleUserId",
                table: "financial_accounts");

            migrationBuilder.DropIndex(
                name: "IX_financial_accounts_SourceProvider_ExternalId",
                table: "financial_accounts");

            migrationBuilder.DropColumn(
                name: "InitialBalanceDate",
                table: "financial_accounts");

            migrationBuilder.DropColumn(
                name: "InstitutionControlled",
                table: "financial_accounts");

            migrationBuilder.DropColumn(
                name: "ResponsibleUserId",
                table: "financial_accounts");

            migrationBuilder.RenameColumn(
                name: "InitialBalance",
                table: "financial_accounts",
                newName: "Balance");

            migrationBuilder.CreateTable(
                name: "bank_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    IsPending = table.Column<bool>(type: "boolean", nullable: false),
                    Category = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bank_transactions_financial_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "financial_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bank_transactions_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Institution = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Brand = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CreditLimit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AvailableLimit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ReportedBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MinimumPayment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    BalanceCloseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    BalanceDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cards_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "card_bills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CardId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ClosingDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MinimumPayment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PaidAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LastPaymentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_card_bills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_card_bills_cards_CardId",
                        column: x => x.CardId,
                        principalTable: "cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "card_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CardId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    PurchaseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsPending = table.Column<bool>(type: "boolean", nullable: false),
                    BillExternalId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    InstallmentNumber = table.Column<int>(type: "integer", nullable: true),
                    TotalInstallments = table.Column<int>(type: "integer", nullable: true),
                    Category = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    OriginalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_card_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_card_transactions_cards_CardId",
                        column: x => x.CardId,
                        principalTable: "cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_financial_accounts_HouseholdId_ExternalId",
                table: "financial_accounts",
                columns: new[] { "HouseholdId", "ExternalId" },
                unique: true,
                filter: "\"ExternalId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_bank_transactions_AccountId_Date",
                table: "bank_transactions",
                columns: new[] { "AccountId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_bank_transactions_AccountId_ExternalId",
                table: "bank_transactions",
                columns: new[] { "AccountId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bank_transactions_HouseholdId",
                table: "bank_transactions",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_card_bills_CardId_ExternalId",
                table: "card_bills",
                columns: new[] { "CardId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_card_transactions_CardId_BillExternalId",
                table: "card_transactions",
                columns: new[] { "CardId", "BillExternalId" });

            migrationBuilder.CreateIndex(
                name: "IX_card_transactions_CardId_ExternalId",
                table: "card_transactions",
                columns: new[] { "CardId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cards_HouseholdId_ExternalId",
                table: "cards",
                columns: new[] { "HouseholdId", "ExternalId" },
                unique: true);

            // Uso pessoal: os dados financeiros são reconstruídos do zero pelo Open Finance.
            // Mantém apenas o usuário dono da integração Pluggy, seu núcleo, categorias e previsões.
            migrationBuilder.Sql("""
                DELETE FROM financial_accounts;
                DELETE FROM pluggy_sync_jobs;
                DELETE FROM financial_integration_syncs;
                UPDATE financial_integrations
                   SET "LastSuccessfulSyncAtUtc" = NULL, "AccountCount" = 0, "TransactionCount" = 0,
                       "LastError" = NULL, "Status" = 'Connected'
                 WHERE "InitialImportCompleted";

                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM financial_integrations) THEN
                        DELETE FROM refresh_tokens WHERE "UserId" NOT IN (SELECT "UserId" FROM financial_integrations);
                        DELETE FROM household_memberships
                         WHERE "UserId" NOT IN (SELECT "UserId" FROM financial_integrations)
                            OR "HouseholdId" NOT IN (SELECT "HouseholdId" FROM financial_integrations);
                        DELETE FROM forecast_entries WHERE "HouseholdId" NOT IN (SELECT "HouseholdId" FROM financial_integrations);
                        DELETE FROM categories WHERE "HouseholdId" NOT IN (SELECT "HouseholdId" FROM financial_integrations);
                        DELETE FROM households WHERE "Id" NOT IN (SELECT "HouseholdId" FROM financial_integrations);
                        DELETE FROM users WHERE "Id" NOT IN (SELECT "UserId" FROM financial_integrations);
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bank_transactions");

            migrationBuilder.DropTable(
                name: "card_bills");

            migrationBuilder.DropTable(
                name: "card_transactions");

            migrationBuilder.DropTable(
                name: "cards");

            migrationBuilder.DropIndex(
                name: "IX_financial_accounts_HouseholdId_ExternalId",
                table: "financial_accounts");

            migrationBuilder.RenameColumn(
                name: "Balance",
                table: "financial_accounts",
                newName: "InitialBalance");

            migrationBuilder.AddColumn<DateOnly>(
                name: "InitialBalanceDate",
                table: "financial_accounts",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<bool>(
                name: "InstitutionControlled",
                table: "financial_accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ResponsibleUserId",
                table: "financial_accounts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "credit_cards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AvailableLimit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Brand = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ClosingDay = table.Column<int>(type: "integer", nullable: true),
                    Color = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    DefaultPaymentAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    DueDay = table.Column<int>(type: "integer", nullable: true),
                    ExternalId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Institution = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    InstitutionControlled = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ResponsibleUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceProvider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TotalLimit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_cards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_credit_cards_financial_accounts_DefaultPaymentAccountId",
                        column: x => x.DefaultPaymentAccountId,
                        principalTable: "financial_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_credit_cards_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_credit_cards_users_ResponsibleUserId",
                        column: x => x.ResponsibleUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "household_invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcceptedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitedEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_household_invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_household_invitations_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_household_invitations_users_AcceptedByUserId",
                        column: x => x.AcceptedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_household_invitations_users_InvitedByUserId",
                        column: x => x.InvitedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "installment_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FirstInstallmentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstallmentCount = table.Column<int>(type: "integer", nullable: false),
                    PurchaseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ResponsibleUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_installment_plans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_installment_plans_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_installment_plans_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_installment_plans_users_ResponsibleUserId",
                        column: x => x.ResponsibleUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recurring_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDay = table.Column<int>(type: "integer", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpectedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ResponsibleUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recurring_transactions_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurring_transactions_financial_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "financial_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurring_transactions_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurring_transactions_users_ResponsibleUserId",
                        column: x => x.ResponsibleUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "credit_card_subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargeDay = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreditCardId = table.Column<Guid>(type: "uuid", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ResponsibleUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_card_subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_credit_card_subscriptions_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_credit_card_subscriptions_credit_cards_CreditCardId",
                        column: x => x.CreditCardId,
                        principalTable: "credit_cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_credit_card_subscriptions_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_credit_card_subscriptions_users_ResponsibleUserId",
                        column: x => x.ResponsibleUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "external_financial_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreditCardId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalAccountId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    FinancialAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    InstitutionName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    IntegrationId = table.Column<Guid>(type: "uuid", nullable: false),
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
                name: "account_movements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    MovementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    SignedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_movements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_account_movements_financial_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "financial_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_account_movements_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "external_transaction_mappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExternalAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalTransactionId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    FinancialTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    IntegrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ProviderId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    SourceUpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TransactionDateTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                });

            migrationBuilder.CreateTable(
                name: "financial_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CanceledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CardSubscriptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientRequestId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CompetenceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreditCardId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DestinationAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstallmentCount = table.Column<int>(type: "integer", nullable: true),
                    InstallmentNumber = table.Column<int>(type: "integer", nullable: true),
                    InstallmentPlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    InstitutionControlled = table.Column<bool>(type: "boolean", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsRefund = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RecurringTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResponsibleUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SettledDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SourceProvider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_financial_transactions_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_financial_transactions_credit_card_subscriptions_CardSubscr~",
                        column: x => x.CardSubscriptionId,
                        principalTable: "credit_card_subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_financial_transactions_credit_cards_CreditCardId",
                        column: x => x.CreditCardId,
                        principalTable: "credit_cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_financial_transactions_financial_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "financial_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_financial_transactions_financial_accounts_DestinationAccoun~",
                        column: x => x.DestinationAccountId,
                        principalTable: "financial_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_financial_transactions_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_financial_transactions_installment_plans_InstallmentPlanId",
                        column: x => x.InstallmentPlanId,
                        principalTable: "installment_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_financial_transactions_recurring_transactions_RecurringTran~",
                        column: x => x.RecurringTransactionId,
                        principalTable: "recurring_transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_financial_transactions_users_ResponsibleUserId",
                        column: x => x.ResponsibleUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClosingDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreditCardId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionControlled = table.Column<bool>(type: "boolean", nullable: false),
                    PaidDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PaymentTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceProvider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_invoices_credit_cards_CreditCardId",
                        column: x => x.CreditCardId,
                        principalTable: "credit_cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoices_financial_transactions_PaymentTransactionId",
                        column: x => x.PaymentTransactionId,
                        principalTable: "financial_transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoices_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_financial_accounts_ResponsibleUserId",
                table: "financial_accounts",
                column: "ResponsibleUserId");

            migrationBuilder.CreateIndex(
                name: "IX_financial_accounts_SourceProvider_ExternalId",
                table: "financial_accounts",
                columns: new[] { "SourceProvider", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_account_movements_AccountId_MovementDate",
                table: "account_movements",
                columns: new[] { "AccountId", "MovementDate" });

            migrationBuilder.CreateIndex(
                name: "IX_account_movements_HouseholdId",
                table: "account_movements",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_account_movements_TransactionId_AccountId_Sequence",
                table: "account_movements",
                columns: new[] { "TransactionId", "AccountId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_credit_card_subscriptions_CategoryId",
                table: "credit_card_subscriptions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_credit_card_subscriptions_CreditCardId_Status",
                table: "credit_card_subscriptions",
                columns: new[] { "CreditCardId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_credit_card_subscriptions_HouseholdId",
                table: "credit_card_subscriptions",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_credit_card_subscriptions_ResponsibleUserId",
                table: "credit_card_subscriptions",
                column: "ResponsibleUserId");

            migrationBuilder.CreateIndex(
                name: "IX_credit_cards_DefaultPaymentAccountId",
                table: "credit_cards",
                column: "DefaultPaymentAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_credit_cards_HouseholdId_Status",
                table: "credit_cards",
                columns: new[] { "HouseholdId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_credit_cards_ResponsibleUserId",
                table: "credit_cards",
                column: "ResponsibleUserId");

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
                name: "IX_financial_transactions_AccountId_CompetenceDate",
                table: "financial_transactions",
                columns: new[] { "AccountId", "CompetenceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_CardSubscriptionId_CompetenceDate",
                table: "financial_transactions",
                columns: new[] { "CardSubscriptionId", "CompetenceDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_CategoryId",
                table: "financial_transactions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_CreditCardId",
                table: "financial_transactions",
                column: "CreditCardId");

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_DestinationAccountId",
                table: "financial_transactions",
                column: "DestinationAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_HouseholdId_ClientRequestId",
                table: "financial_transactions",
                columns: new[] { "HouseholdId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_HouseholdId_DueDate_Status",
                table: "financial_transactions",
                columns: new[] { "HouseholdId", "DueDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_InstallmentPlanId_InstallmentNumber",
                table: "financial_transactions",
                columns: new[] { "InstallmentPlanId", "InstallmentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_InvoiceId_Status",
                table: "financial_transactions",
                columns: new[] { "InvoiceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_RecurringTransactionId_DueDate",
                table: "financial_transactions",
                columns: new[] { "RecurringTransactionId", "DueDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_ResponsibleUserId",
                table: "financial_transactions",
                column: "ResponsibleUserId");

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_SourceProvider_ExternalId",
                table: "financial_transactions",
                columns: new[] { "SourceProvider", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_household_invitations_AcceptedByUserId",
                table: "household_invitations",
                column: "AcceptedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_household_invitations_HouseholdId_ExpiresAtUtc",
                table: "household_invitations",
                columns: new[] { "HouseholdId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_household_invitations_InvitedByUserId",
                table: "household_invitations",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_household_invitations_TokenHash",
                table: "household_invitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_installment_plans_CategoryId",
                table: "installment_plans",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_installment_plans_HouseholdId_PurchaseDate",
                table: "installment_plans",
                columns: new[] { "HouseholdId", "PurchaseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_installment_plans_ResponsibleUserId",
                table: "installment_plans",
                column: "ResponsibleUserId");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_CreditCardId_ClosingDate",
                table: "invoices",
                columns: new[] { "CreditCardId", "ClosingDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_HouseholdId",
                table: "invoices",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_PaymentTransactionId",
                table: "invoices",
                column: "PaymentTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_SourceProvider_ExternalId",
                table: "invoices",
                columns: new[] { "SourceProvider", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recurring_transactions_AccountId",
                table: "recurring_transactions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_transactions_CategoryId",
                table: "recurring_transactions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_transactions_HouseholdId_Status",
                table: "recurring_transactions",
                columns: new[] { "HouseholdId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_transactions_ResponsibleUserId",
                table: "recurring_transactions",
                column: "ResponsibleUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_financial_accounts_users_ResponsibleUserId",
                table: "financial_accounts",
                column: "ResponsibleUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_account_movements_financial_transactions_TransactionId",
                table: "account_movements",
                column: "TransactionId",
                principalTable: "financial_transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_external_transaction_mappings_financial_transactions_Financ~",
                table: "external_transaction_mappings",
                column: "FinancialTransactionId",
                principalTable: "financial_transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_financial_transactions_invoices_InvoiceId",
                table: "financial_transactions",
                column: "InvoiceId",
                principalTable: "invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
