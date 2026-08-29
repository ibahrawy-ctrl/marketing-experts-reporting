using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class R5_DecOneCadenceEffectivityAndEmploymentWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "EffectiveFrom",
                table: "kpi_template_assignments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EffectiveTo",
                table: "kpi_template_assignments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ExitDate",
                table: "AspNetUsers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "HireDate",
                table: "AspNetUsers",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_kpi_template_assignments_ScopeType_ScopeId_EffectiveFrom_Ef~",
                table: "kpi_template_assignments",
                columns: new[] { "ScopeType", "ScopeId", "EffectiveFrom", "EffectiveTo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_kpi_template_assignments_ScopeType_ScopeId_EffectiveFrom_Ef~",
                table: "kpi_template_assignments");

            migrationBuilder.DropColumn(
                name: "EffectiveFrom",
                table: "kpi_template_assignments");

            migrationBuilder.DropColumn(
                name: "EffectiveTo",
                table: "kpi_template_assignments");

            migrationBuilder.DropColumn(
                name: "ExitDate",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "HireDate",
                table: "AspNetUsers");
        }
    }
}
