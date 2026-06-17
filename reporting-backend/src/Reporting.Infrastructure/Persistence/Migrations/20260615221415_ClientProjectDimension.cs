using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClientProjectDimension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "risks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "risks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "report_submissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "report_submissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AccountManagerId = table.Column<Guid>(type: "uuid", nullable: true),
                    MainContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MainContactInfo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ServiceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OwnerTeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccountManagerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_projects_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_risks_ClientId",
                table: "risks",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_risks_ProjectId",
                table: "risks",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_report_submissions_ClientId",
                table: "report_submissions",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_report_submissions_ProjectId",
                table: "report_submissions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_clients_AccountManagerId",
                table: "clients",
                column: "AccountManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_clients_Status",
                table: "clients",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_projects_AccountManagerId",
                table: "projects",
                column: "AccountManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_projects_ClientId",
                table: "projects",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_projects_OwnerTeamId",
                table: "projects",
                column: "OwnerTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_projects_Status",
                table: "projects",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "clients");

            migrationBuilder.DropIndex(
                name: "IX_risks_ClientId",
                table: "risks");

            migrationBuilder.DropIndex(
                name: "IX_risks_ProjectId",
                table: "risks");

            migrationBuilder.DropIndex(
                name: "IX_report_submissions_ClientId",
                table: "report_submissions");

            migrationBuilder.DropIndex(
                name: "IX_report_submissions_ProjectId",
                table: "report_submissions");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "risks");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "risks");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "report_submissions");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "report_submissions");
        }
    }
}
