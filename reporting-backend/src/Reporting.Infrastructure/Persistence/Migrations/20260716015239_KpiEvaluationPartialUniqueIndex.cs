using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KpiEvaluationPartialUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_kpi_evaluations_KpiTemplateVersionId_SubjectUserId_PeriodKey",
                table: "kpi_evaluations");

            migrationBuilder.CreateIndex(
                name: "IX_kpi_evaluations_KpiTemplateVersionId_SubjectUserId_PeriodKey",
                table: "kpi_evaluations",
                columns: new[] { "KpiTemplateVersionId", "SubjectUserId", "PeriodKey" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_kpi_evaluations_KpiTemplateVersionId_SubjectUserId_PeriodKey",
                table: "kpi_evaluations");

            migrationBuilder.CreateIndex(
                name: "IX_kpi_evaluations_KpiTemplateVersionId_SubjectUserId_PeriodKey",
                table: "kpi_evaluations",
                columns: new[] { "KpiTemplateVersionId", "SubjectUserId", "PeriodKey" },
                unique: true);
        }
    }
}
