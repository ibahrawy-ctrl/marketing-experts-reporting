using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProject360Foundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Background",
                table: "projects",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessContext",
                table: "projects",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HealthComputedAtUtc",
                table: "projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HealthPercent",
                table: "projects",
                type: "numeric(9,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "HealthStatus",
                table: "projects",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                // CPW-R3 · W3 · §14 — تصحيح أدنى داخل AddColumn المسموح بها: القيمة الافتراضيّة
                // المولَّدة آليًّا كانت "" وهي ليست عضوًا صالحًا في ProjectHealthStatus
                // (Green/Yellow/Red) ⟹ صفوف projects القائمة كانت ستحمل قيمة لا تُحوَّل عند القراءة.
                // "Green" = العضو ذو القيمة 0 وهو نفسه الافتراضيّ في الكيان ⟹ صفر إعادة كتابة بيانات.
                defaultValue: "Green");

            migrationBuilder.AddColumn<string>(
                name: "OutOfScope",
                table: "projects",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProgressPercent",
                table: "projects",
                type: "numeric(9,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectOwnerId",
                table: "projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScopeText",
                table: "projects",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuccessDefinition",
                table: "projects",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "projects",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TeamLeaderId",
                table: "projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "decisions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "project_objectives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkstreamId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(9,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_objectives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_objectives_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_strategies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Vision = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    StrategySummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    TargetAudience = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CustomerPersona = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Positioning = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ValueProposition = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Competitors = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ToneOfVoice = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Messaging = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    MarketingApproach = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SuccessFactors = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_strategies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_strategies_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_deliverables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectiveId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkstreamId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliverableTypeCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PlannedQuantity = table.Column<int>(type: "integer", nullable: false),
                    CompletedQuantity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProgressPercent = table.Column<decimal>(type: "numeric(9,2)", precision: 18, scale: 2, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DeliveredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_deliverables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_deliverables_project_objectives_ObjectiveId",
                        column: x => x.ObjectiveId,
                        principalTable: "project_objectives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_project_deliverables_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_kpis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectiveId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CustomUnitLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BaselineValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    TargetValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrentValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    LastReadingDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Weight = table.Column<decimal>(type: "numeric(9,2)", precision: 18, scale: 2, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExternalSourceKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExternalMetricCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LastSyncedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_kpis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_kpis_project_objectives_ObjectiveId",
                        column: x => x.ObjectiveId,
                        principalTable: "project_objectives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_kpis_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_strategy_attributes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectStrategyId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SectionCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ValueText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_strategy_attributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_strategy_attributes_project_strategies_ProjectStrat~",
                        column: x => x.ProjectStrategyId,
                        principalTable: "project_strategies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_kpi_readings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectKpiId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReadingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TargetSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AchievementSnapshot = table.Column<decimal>(type: "numeric(9,2)", precision: 18, scale: 2, nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_kpi_readings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_kpi_readings_project_kpis_ProjectKpiId",
                        column: x => x.ProjectKpiId,
                        principalTable: "project_kpis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_projects_HealthStatus",
                table: "projects",
                column: "HealthStatus");

            migrationBuilder.CreateIndex(
                name: "IX_projects_ProjectOwnerId",
                table: "projects",
                column: "ProjectOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_projects_TeamLeaderId",
                table: "projects",
                column: "TeamLeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_decisions_ProjectId",
                table: "decisions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_project_deliverables_DeliverableTypeCode",
                table: "project_deliverables",
                column: "DeliverableTypeCode");

            migrationBuilder.CreateIndex(
                name: "IX_project_deliverables_ObjectiveId",
                table: "project_deliverables",
                column: "ObjectiveId");

            migrationBuilder.CreateIndex(
                name: "IX_project_deliverables_OwnerUserId",
                table: "project_deliverables",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_project_deliverables_ProjectId",
                table: "project_deliverables",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_project_deliverables_Status",
                table: "project_deliverables",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_project_deliverables_WorkstreamId",
                table: "project_deliverables",
                column: "WorkstreamId");

            migrationBuilder.CreateIndex(
                name: "IX_project_kpi_readings_ProjectKpiId_ReadingDate",
                table: "project_kpi_readings",
                columns: new[] { "ProjectKpiId", "ReadingDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_kpi_readings_ReadingDate",
                table: "project_kpi_readings",
                column: "ReadingDate");

            migrationBuilder.CreateIndex(
                name: "IX_project_kpis_ObjectiveId",
                table: "project_kpis",
                column: "ObjectiveId");

            migrationBuilder.CreateIndex(
                name: "IX_project_kpis_ProjectId",
                table: "project_kpis",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_project_kpis_SourceType",
                table: "project_kpis",
                column: "SourceType");

            migrationBuilder.CreateIndex(
                name: "IX_project_objectives_OwnerUserId",
                table: "project_objectives",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_project_objectives_ProjectId",
                table: "project_objectives",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_project_objectives_Status",
                table: "project_objectives",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_project_objectives_WorkstreamId",
                table: "project_objectives",
                column: "WorkstreamId");

            migrationBuilder.CreateIndex(
                name: "IX_project_strategies_ProjectId_Active",
                table: "project_strategies",
                column: "ProjectId",
                unique: true,
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_project_strategy_attributes_SectionCode",
                table: "project_strategy_attributes",
                column: "SectionCode");

            migrationBuilder.CreateIndex(
                name: "IX_project_strategy_attributes_StrategyId_FieldCode",
                table: "project_strategy_attributes",
                columns: new[] { "ProjectStrategyId", "FieldCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_deliverables");

            migrationBuilder.DropTable(
                name: "project_kpi_readings");

            migrationBuilder.DropTable(
                name: "project_strategy_attributes");

            migrationBuilder.DropTable(
                name: "project_kpis");

            migrationBuilder.DropTable(
                name: "project_strategies");

            migrationBuilder.DropTable(
                name: "project_objectives");

            migrationBuilder.DropIndex(
                name: "IX_projects_HealthStatus",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "IX_projects_ProjectOwnerId",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "IX_projects_TeamLeaderId",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "IX_decisions_ProjectId",
                table: "decisions");

            migrationBuilder.DropColumn(
                name: "Background",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "BusinessContext",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "HealthComputedAtUtc",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "HealthPercent",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "HealthStatus",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "OutOfScope",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "ProgressPercent",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "ProjectOwnerId",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "ScopeText",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "SuccessDefinition",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "TeamLeaderId",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "decisions");
        }
    }
}
