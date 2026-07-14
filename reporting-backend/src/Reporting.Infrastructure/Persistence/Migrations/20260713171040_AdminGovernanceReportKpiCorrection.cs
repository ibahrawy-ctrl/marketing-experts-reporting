using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminGovernanceReportKpiCorrection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_report_submissions_ReportTemplateVersionId_SubmitterId_Peri~",
                table: "report_submissions");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "report_submissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByUserId",
                table: "report_submissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "report_submissions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "report_submissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "kpi_evaluations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByUserId",
                table: "kpi_evaluations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "kpi_evaluations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "kpi_evaluations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "kpi_evaluations",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAtUtc",
                table: "kpi_evaluations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewerId",
                table: "kpi_evaluations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "approval_steps",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.CreateTable(
                name: "kpi_evaluation_review_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KpiEvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    PreviousValuesJson = table.Column<string>(type: "jsonb", nullable: true),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kpi_evaluation_review_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_kpi_evaluation_review_events_kpi_evaluations_KpiEvaluationId",
                        column: x => x.KpiEvaluationId,
                        principalTable: "kpi_evaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_report_submissions_ReportTemplateVersionId_SubmitterId_Peri~",
                table: "report_submissions",
                columns: new[] { "ReportTemplateVersionId", "SubmitterId", "PeriodKey" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_kpi_evaluation_review_events_KpiEvaluationId_CreatedAtUtc",
                table: "kpi_evaluation_review_events",
                columns: new[] { "KpiEvaluationId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        // ملاحظة توثيقيّة (ADMIN-GOVERNANCE-R1، تصحيح #3):
        // بعد أيّ حذف إداريّ ناعم (IsDeleted=true) تلته إعادة تسليم لنفس الفترة، ستوجد صفوف متعدّدة
        // بنفس (ReportTemplateVersionId, SubmitterId, PeriodKey) — بعضها محذوف. إعادة إنشاء الفهرس
        // الفريد الكامل (بلا فلتر) في Down ستفشل حينها. لا حذف/دمج تلقائيّ للبيانات هنا.
        // التراجع الآمن بعد استخدام الحذف الناعم فعليًّا يكون عبر استعادة نسخة احتياطية (Backup Restore)،
        // لا عبر تشغيل Down الأعمى. أُضيف Fail-fast صريح أدناه يوقف الهجرة العكسية برسالة واضحة إن وُجد تعارض.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "kpi_evaluation_review_events");

            // Fail-fast: امنع إعادة إنشاء الفهرس الفريد الكامل إن وُجدت مجموعات مكرّرة
            // (ReportTemplateVersionId, SubmitterId, PeriodKey) نتيجة إعادة تسليم بعد حذف ناعم.
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM report_submissions
        GROUP BY ""ReportTemplateVersionId"", ""SubmitterId"", ""PeriodKey""
        HAVING COUNT(*) > 1
    ) THEN
        RAISE EXCEPTION 'ADMIN-GOVERNANCE-R1 Down aborted: duplicate (ReportTemplateVersionId, SubmitterId, PeriodKey) rows exist (soft-deleted + re-submitted). Do NOT run this Down blindly; restore from backup instead.';
    END IF;
END $$;");

            migrationBuilder.DropIndex(
                name: "IX_report_submissions_ReportTemplateVersionId_SubmitterId_Peri~",
                table: "report_submissions");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "report_submissions");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "report_submissions");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "report_submissions");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "report_submissions");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "kpi_evaluations");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "kpi_evaluations");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "kpi_evaluations");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "kpi_evaluations");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "kpi_evaluations");

            migrationBuilder.DropColumn(
                name: "ReviewedAtUtc",
                table: "kpi_evaluations");

            migrationBuilder.DropColumn(
                name: "ReviewerId",
                table: "kpi_evaluations");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "approval_steps",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.CreateIndex(
                name: "IX_report_submissions_ReportTemplateVersionId_SubmitterId_Peri~",
                table: "report_submissions",
                columns: new[] { "ReportTemplateVersionId", "SubmitterId", "PeriodKey" },
                unique: true);
        }
    }
}
