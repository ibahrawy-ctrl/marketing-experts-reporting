using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectExecutionUpdateProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_execution_update_proposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliverableId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProposedById = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposedProgressPercent = table.Column<decimal>(type: "numeric(9,2)", precision: 18, scale: 2, nullable: false),
                    ProposedStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExecutionNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReviewedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PreviousProgressPercent = table.Column<decimal>(type: "numeric(9,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_execution_update_proposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_execution_update_proposals_project_deliverables_Del~",
                        column: x => x.DeliverableId,
                        principalTable: "project_deliverables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_execution_update_proposals_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_execution_update_proposals_DeliverableId_Status",
                table: "project_execution_update_proposals",
                columns: new[] { "DeliverableId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_project_execution_update_proposals_ProjectId",
                table: "project_execution_update_proposals",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_project_execution_update_proposals_ProposedById",
                table: "project_execution_update_proposals",
                column: "ProposedById");

            migrationBuilder.CreateIndex(
                name: "IX_project_execution_update_proposals_Status",
                table: "project_execution_update_proposals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_project_execution_update_proposals_SubmissionId",
                table: "project_execution_update_proposals",
                column: "SubmissionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_execution_update_proposals");
        }
    }
}
