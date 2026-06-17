using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ManagementNotesAndGovernanceLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NextAction",
                table: "risks",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedKpiEvaluationId",
                table: "risks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedSubmissionId",
                table: "risks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubjectUserId",
                table: "risks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TeamId",
                table: "risks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "KpiEvaluationId",
                table: "escalations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NextAction",
                table: "escalations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NextAction",
                table: "decisions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedKpiEvaluationId",
                table: "decisions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "management_notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    NoteType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RequiresAction = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ResolvedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_management_notes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_risks_RelatedKpiEvaluationId",
                table: "risks",
                column: "RelatedKpiEvaluationId");

            migrationBuilder.CreateIndex(
                name: "IX_risks_RelatedSubmissionId",
                table: "risks",
                column: "RelatedSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_risks_SubjectUserId",
                table: "risks",
                column: "SubjectUserId");

            migrationBuilder.CreateIndex(
                name: "IX_escalations_KpiEvaluationId",
                table: "escalations",
                column: "KpiEvaluationId");

            migrationBuilder.CreateIndex(
                name: "IX_escalations_RiskId",
                table: "escalations",
                column: "RiskId");

            migrationBuilder.CreateIndex(
                name: "IX_decisions_RelatedEscalationId",
                table: "decisions",
                column: "RelatedEscalationId");

            migrationBuilder.CreateIndex(
                name: "IX_decisions_RelatedRiskId",
                table: "decisions",
                column: "RelatedRiskId");

            migrationBuilder.CreateIndex(
                name: "IX_decisions_RelatedSubmissionId",
                table: "decisions",
                column: "RelatedSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_management_notes_AuthorId",
                table: "management_notes",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_management_notes_EntityType_EntityId",
                table: "management_notes",
                columns: new[] { "EntityType", "EntityId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "management_notes");

            migrationBuilder.DropIndex(
                name: "IX_risks_RelatedKpiEvaluationId",
                table: "risks");

            migrationBuilder.DropIndex(
                name: "IX_risks_RelatedSubmissionId",
                table: "risks");

            migrationBuilder.DropIndex(
                name: "IX_risks_SubjectUserId",
                table: "risks");

            migrationBuilder.DropIndex(
                name: "IX_escalations_KpiEvaluationId",
                table: "escalations");

            migrationBuilder.DropIndex(
                name: "IX_escalations_RiskId",
                table: "escalations");

            migrationBuilder.DropIndex(
                name: "IX_decisions_RelatedEscalationId",
                table: "decisions");

            migrationBuilder.DropIndex(
                name: "IX_decisions_RelatedRiskId",
                table: "decisions");

            migrationBuilder.DropIndex(
                name: "IX_decisions_RelatedSubmissionId",
                table: "decisions");

            migrationBuilder.DropColumn(
                name: "NextAction",
                table: "risks");

            migrationBuilder.DropColumn(
                name: "RelatedKpiEvaluationId",
                table: "risks");

            migrationBuilder.DropColumn(
                name: "RelatedSubmissionId",
                table: "risks");

            migrationBuilder.DropColumn(
                name: "SubjectUserId",
                table: "risks");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "risks");

            migrationBuilder.DropColumn(
                name: "KpiEvaluationId",
                table: "escalations");

            migrationBuilder.DropColumn(
                name: "NextAction",
                table: "escalations");

            migrationBuilder.DropColumn(
                name: "NextAction",
                table: "decisions");

            migrationBuilder.DropColumn(
                name: "RelatedKpiEvaluationId",
                table: "decisions");
        }
    }
}
