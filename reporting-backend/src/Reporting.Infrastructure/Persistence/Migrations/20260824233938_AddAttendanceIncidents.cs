using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceIncidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attendance_incident_attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StoredPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_incident_attachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "attendance_incident_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ToStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ChangesJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_incident_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "attendance_incident_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RequiresTimes = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresPolicyReference = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsMultiplePerDay = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_incident_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "attendance_incidents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IncidentTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IncidentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ReturnTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DetectionSource = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReportedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    PolicyRefId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EmployeeResponse = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RespondedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HrDecision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    HrNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReconciledWithLeaveId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReconciledWithPermissionId = table.Column<Guid>(type: "uuid", nullable: true),
                    DuplicateOfId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ConcurrencyStamp = table.Column<int>(type: "integer", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_incidents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_incident_attachments_IncidentId",
                table: "attendance_incident_attachments",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_incident_events_IncidentId",
                table: "attendance_incident_events",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_incident_types_Code",
                table: "attendance_incident_types",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_attendance_incidents_IncidentTypeId_IncidentDate",
                table: "attendance_incidents",
                columns: new[] { "IncidentTypeId", "IncidentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_incidents_ReportedByUserId_IdempotencyKey",
                table: "attendance_incidents",
                columns: new[] { "ReportedByUserId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_incidents_Status_DepartmentId",
                table: "attendance_incidents",
                columns: new[] { "Status", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_incidents_Status_TeamId",
                table: "attendance_incidents",
                columns: new[] { "Status", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_incidents_SubjectUserId_IncidentDate",
                table: "attendance_incidents",
                columns: new[] { "SubjectUserId", "IncidentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_incidents_SubjectUserId_IncidentDate_IncidentTyp~",
                table: "attendance_incidents",
                columns: new[] { "SubjectUserId", "IncidentDate", "IncidentTypeId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attendance_incident_attachments");

            migrationBuilder.DropTable(
                name: "attendance_incident_events");

            migrationBuilder.DropTable(
                name: "attendance_incident_types");

            migrationBuilder.DropTable(
                name: "attendance_incidents");
        }
    }
}
