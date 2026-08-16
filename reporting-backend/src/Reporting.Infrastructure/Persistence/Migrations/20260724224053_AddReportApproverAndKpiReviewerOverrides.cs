using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportApproverAndKpiReviewerOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "KpiReviewerOverrideUserId",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReportApproverOverrideUserId",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_KpiReviewerOverrideUserId",
                table: "AspNetUsers",
                column: "KpiReviewerOverrideUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_ReportApproverOverrideUserId",
                table: "AspNetUsers",
                column: "ReportApproverOverrideUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_KpiReviewerOverrideUserId",
                table: "AspNetUsers",
                column: "KpiReviewerOverrideUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_ReportApproverOverrideUserId",
                table: "AspNetUsers",
                column: "ReportApproverOverrideUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_KpiReviewerOverrideUserId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_ReportApproverOverrideUserId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_KpiReviewerOverrideUserId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_ReportApproverOverrideUserId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "KpiReviewerOverrideUserId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ReportApproverOverrideUserId",
                table: "AspNetUsers");
        }
    }
}
