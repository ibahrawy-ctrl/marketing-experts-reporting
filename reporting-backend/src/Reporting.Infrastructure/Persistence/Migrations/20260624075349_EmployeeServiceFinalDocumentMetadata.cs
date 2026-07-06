using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeServiceFinalDocumentMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HrAttachmentContentType",
                table: "employee_service_requests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HrAttachmentOriginalFileName",
                table: "employee_service_requests",
                type: "character varying(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "HrAttachmentSizeBytes",
                table: "employee_service_requests",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HrAttachmentUploadedAtUtc",
                table: "employee_service_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HrAttachmentUploadedByUserId",
                table: "employee_service_requests",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HrAttachmentContentType",
                table: "employee_service_requests");

            migrationBuilder.DropColumn(
                name: "HrAttachmentOriginalFileName",
                table: "employee_service_requests");

            migrationBuilder.DropColumn(
                name: "HrAttachmentSizeBytes",
                table: "employee_service_requests");

            migrationBuilder.DropColumn(
                name: "HrAttachmentUploadedAtUtc",
                table: "employee_service_requests");

            migrationBuilder.DropColumn(
                name: "HrAttachmentUploadedByUserId",
                table: "employee_service_requests");
        }
    }
}
