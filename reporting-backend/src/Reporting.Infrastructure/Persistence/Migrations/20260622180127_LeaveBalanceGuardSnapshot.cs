using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LeaveBalanceGuardSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BalanceAtRequest",
                table: "leave_requests",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmployeeAcknowledgedAtUtc",
                table: "leave_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmployeeAcknowledgedUnpaidDeduction",
                table: "leave_requests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPotentialUnpaidLeave",
                table: "leave_requests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RequestedLeaveDays",
                table: "leave_requests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UncoveredLeaveDays",
                table: "leave_requests",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BalanceAtRequest",
                table: "leave_requests");

            migrationBuilder.DropColumn(
                name: "EmployeeAcknowledgedAtUtc",
                table: "leave_requests");

            migrationBuilder.DropColumn(
                name: "EmployeeAcknowledgedUnpaidDeduction",
                table: "leave_requests");

            migrationBuilder.DropColumn(
                name: "IsPotentialUnpaidLeave",
                table: "leave_requests");

            migrationBuilder.DropColumn(
                name: "RequestedLeaveDays",
                table: "leave_requests");

            migrationBuilder.DropColumn(
                name: "UncoveredLeaveDays",
                table: "leave_requests");
        }
    }
}
