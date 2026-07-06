using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GovernanceWorkspaceItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "governance_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedSubmissionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ResolutionSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_governance_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "governance_item_updates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GovernanceItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdateType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OldStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    NewStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_governance_item_updates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_governance_item_updates_governance_items_GovernanceItemId",
                        column: x => x.GovernanceItemId,
                        principalTable: "governance_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_governance_item_updates_AuthorId",
                table: "governance_item_updates",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_governance_item_updates_GovernanceItemId",
                table: "governance_item_updates",
                column: "GovernanceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_governance_items_AssignedToUserId",
                table: "governance_items",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_governance_items_Category",
                table: "governance_items",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_governance_items_CreatedById",
                table: "governance_items",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_governance_items_DepartmentId",
                table: "governance_items",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_governance_items_RelatedSubmissionId",
                table: "governance_items",
                column: "RelatedSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_governance_items_RelatedUserId",
                table: "governance_items",
                column: "RelatedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_governance_items_Status",
                table: "governance_items",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_governance_items_TeamId",
                table: "governance_items",
                column: "TeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "governance_item_updates");

            migrationBuilder.DropTable(
                name: "governance_items");
        }
    }
}
