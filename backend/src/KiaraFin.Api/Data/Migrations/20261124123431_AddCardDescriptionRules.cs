using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KiaraFin.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCardDescriptionRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "card_description_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Alias = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_card_description_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_card_description_rules_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_card_description_rules_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_card_description_rules_CategoryId",
                table: "card_description_rules",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_card_description_rules_HouseholdId_Key",
                table: "card_description_rules",
                columns: new[] { "HouseholdId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "card_description_rules");
        }
    }
}
