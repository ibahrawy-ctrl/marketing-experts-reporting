using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GovernanceIndividualEscalations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "governance_escalations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    EscalationType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RaisedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetDepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetTeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedSubmissionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedGovernanceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Resolution = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_governance_escalations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "governance_escalation_updates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EscalationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdateType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OldStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    NewStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_governance_escalation_updates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_governance_escalation_updates_governance_escalations_Escala~",
                        column: x => x.EscalationId,
                        principalTable: "governance_escalations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_governance_escalation_updates_AuthorId",
                table: "governance_escalation_updates",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_governance_escalation_updates_EscalationId",
                table: "governance_escalation_updates",
                column: "EscalationId");

            migrationBuilder.CreateIndex(
                name: "IX_governance_escalations_AssignedToUserId",
                table: "governance_escalations",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_governance_escalations_CreatedAtUtc",
                table: "governance_escalations",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_governance_escalations_EscalationType",
                table: "governance_escalations",
                column: "EscalationType");

            migrationBuilder.CreateIndex(
                name: "IX_governance_escalations_RaisedByUserId",
                table: "governance_escalations",
                column: "RaisedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_governance_escalations_RelatedGovernanceItemId",
                table: "governance_escalations",
                column: "RelatedGovernanceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_governance_escalations_RelatedSubmissionId",
                table: "governance_escalations",
                column: "RelatedSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_governance_escalations_Severity",
                table: "governance_escalations",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_governance_escalations_Status",
                table: "governance_escalations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_governance_escalations_TargetDepartmentId",
                table: "governance_escalations",
                column: "TargetDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_governance_escalations_TargetTeamId",
                table: "governance_escalations",
                column: "TargetTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_governance_escalations_TargetUserId",
                table: "governance_escalations",
                column: "TargetUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "governance_escalation_updates");

            migrationBuilder.DropTable(
                name: "governance_escalations");
        }
    }
}
