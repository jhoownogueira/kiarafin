using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KiaraFin.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditCardSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CardSubscriptionId",
                table: "financial_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "credit_card_subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreditCardId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ChargeDay = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponsibleUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_CardSubscriptionId_CompetenceDate",
                table: "financial_transactions",
                columns: new[] { "CardSubscriptionId", "CompetenceDate" },
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

            migrationBuilder.AddForeignKey(
                name: "FK_financial_transactions_credit_card_subscriptions_CardSubscr~",
                table: "financial_transactions",
                column: "CardSubscriptionId",
                principalTable: "credit_card_subscriptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_financial_transactions_credit_card_subscriptions_CardSubscr~",
                table: "financial_transactions");

            migrationBuilder.DropTable(
                name: "credit_card_subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_financial_transactions_CardSubscriptionId_CompetenceDate",
                table: "financial_transactions");

            migrationBuilder.DropColumn(
                name: "CardSubscriptionId",
                table: "financial_transactions");
        }
    }
}
