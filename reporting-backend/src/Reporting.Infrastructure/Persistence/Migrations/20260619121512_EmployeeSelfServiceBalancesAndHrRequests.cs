using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeSelfServiceBalancesAndHrRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "balance_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    JobRoleId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnnualLeaveDefaultDays = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PermissionUnit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PermissionMonthlyLimit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PermissionAnnualLimit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AllowNegativeBalance = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_balance_policies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "employee_balance_ledger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    BalanceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RelatedRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_balance_ledger", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "employee_service_request_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeServiceRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ToStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_service_request_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "employee_service_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PreferredLanguage = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DestinationEntity = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AttachmentPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    HrComment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    HrAttachmentPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AssignedToHrUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_service_requests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_balance_policies_Year_JobRoleId",
                table: "balance_policies",
                columns: new[] { "Year", "JobRoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employee_balance_ledger_EmployeeId_BalanceType_Year",
                table: "employee_balance_ledger",
                columns: new[] { "EmployeeId", "BalanceType", "Year" });

            migrationBuilder.CreateIndex(
                name: "IX_employee_balance_ledger_RelatedRequestId_Source",
                table: "employee_balance_ledger",
                columns: new[] { "RelatedRequestId", "Source" },
                unique: true,
                filter: "\"RelatedRequestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_employee_service_request_events_EmployeeServiceRequestId",
                table: "employee_service_request_events",
                column: "EmployeeServiceRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_employee_service_requests_RequesterUserId",
                table: "employee_service_requests",
                column: "RequesterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_employee_service_requests_Status",
                table: "employee_service_requests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "balance_policies");

            migrationBuilder.DropTable(
                name: "employee_balance_ledger");

            migrationBuilder.DropTable(
                name: "employee_service_request_events");

            migrationBuilder.DropTable(
                name: "employee_service_requests");
        }
    }
}
