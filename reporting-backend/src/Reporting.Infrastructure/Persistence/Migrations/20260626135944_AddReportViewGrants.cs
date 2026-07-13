using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportViewGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "report_view_grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GranteeUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeKind = table.Column<int>(type: "integer", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetTeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_view_grants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_report_view_grants_GranteeUserId",
                table: "report_view_grants",
                column: "GranteeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_report_view_grants_GranteeUserId_TargetTeamId",
                table: "report_view_grants",
                columns: new[] { "GranteeUserId", "TargetTeamId" },
                unique: true,
                filter: "\"IsActive\" AND \"ScopeKind\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_report_view_grants_GranteeUserId_TargetUserId",
                table: "report_view_grants",
                columns: new[] { "GranteeUserId", "TargetUserId" },
                unique: true,
                filter: "\"IsActive\" AND \"ScopeKind\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_report_view_grants_IsActive",
                table: "report_view_grants",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_report_view_grants_TargetTeamId",
                table: "report_view_grants",
                column: "TargetTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_report_view_grants_TargetUserId",
                table: "report_view_grants",
                column: "TargetUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "report_view_grants");
        }
    }
}
