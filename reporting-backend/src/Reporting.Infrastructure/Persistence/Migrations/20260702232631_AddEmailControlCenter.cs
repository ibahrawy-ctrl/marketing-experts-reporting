using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailControlCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "email_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SendToEmployee = table.Column<bool>(type: "boolean", nullable: false),
                    SendToManager = table.Column<bool>(type: "boolean", nullable: false),
                    SendToTeamLeader = table.Column<bool>(type: "boolean", nullable: false),
                    SendToHr = table.Column<bool>(type: "boolean", nullable: false),
                    SendToGovernance = table.Column<bool>(type: "boolean", nullable: false),
                    SendToAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    CooldownMinutes = table.Column<int>(type: "integer", nullable: true),
                    Mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "email_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SubjectTemplate = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BodyTemplate = table.Column<string>(type: "text", nullable: false),
                    AvailableVariablesJson = table.Column<string>(type: "text", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_templates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_email_rules_TemplateKey_EventType",
                table: "email_rules",
                columns: new[] { "TemplateKey", "EventType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_templates_Category",
                table: "email_templates",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_email_templates_Key",
                table: "email_templates",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_rules");

            migrationBuilder.DropTable(
                name: "email_templates");
        }
    }
}
