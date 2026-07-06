using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGovernanceActionItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "governance_action_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SourceType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CompletionNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsSensitive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_governance_action_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "governance_action_item_updates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdateType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OldStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    NewStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_governance_action_item_updates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_governance_action_item_updates_governance_action_items_Acti~",
                        column: x => x.ActionItemId,
                        principalTable: "governance_action_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_governance_action_item_updates_ActionItemId",
                table: "governance_action_item_updates",
                column: "ActionItemId");

            migrationBuilder.CreateIndex(
                name: "IX_governance_action_item_updates_AuthorId",
                table: "governance_action_item_updates",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_governance_action_items_AssignedToUserId",
                table: "governance_action_items",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_governance_action_items_CreatedAtUtc",
                table: "governance_action_items",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_governance_action_items_CreatedByUserId",
                table: "governance_action_items",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_governance_action_items_DueDate",
                table: "governance_action_items",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_governance_action_items_Priority",
                table: "governance_action_items",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_governance_action_items_SourceType_SourceId",
                table: "governance_action_items",
                columns: new[] { "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_governance_action_items_Status",
                table: "governance_action_items",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "governance_action_item_updates");

            migrationBuilder.DropTable(
                name: "governance_action_items");
        }
    }
}
