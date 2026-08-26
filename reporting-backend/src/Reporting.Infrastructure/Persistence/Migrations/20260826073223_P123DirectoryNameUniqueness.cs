using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P123DirectoryNameUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DEF-P123-001 — حارس Preflight داخل الهجرة نفسها.
            // الهجرات تُطبَّق تلقائيًّا عند الإقلاع (MigrateAsync)، ولو وُجد تكرار قائم لفشل
            // إنشاء الفهرس بخطأ Postgres غامض يوقف بدء الخدمة بلا دلالة. الحارس يستبدل ذلك
            // برسالة قابلة للتنفيذ تذكر العدد والاسم المتضارب، ولا يحذف ولا يدمج أيّ صفّ:
            // معالجة البيانات قرار تشغيليّ صريح خارج الهجرة.
            migrationBuilder.Sql(@"
DO $$
DECLARE dup_count int; sample text;
BEGIN
    SELECT count(*), coalesce(min(""NameAr""), '') INTO dup_count, sample
    FROM (SELECT ""NameAr"" FROM departments GROUP BY ""NameAr"" HAVING count(*) > 1) d;
    IF dup_count > 0 THEN
        RAISE EXCEPTION 'P123-PREFLIGHT: % duplicate department NameAr group(s) block IX_departments_NameAr (e.g. %). Resolve the duplicates before applying this migration.', dup_count, sample;
    END IF;

    SELECT count(*), coalesce(min(""NameAr""), '') INTO dup_count, sample
    FROM (SELECT ""DepartmentId"", ""NameAr"" FROM teams GROUP BY ""DepartmentId"", ""NameAr"" HAVING count(*) > 1) t;
    IF dup_count > 0 THEN
        RAISE EXCEPTION 'P123-PREFLIGHT: % duplicate team (DepartmentId, NameAr) group(s) block IX_teams_DepartmentId_NameAr (e.g. %). Resolve the duplicates before applying this migration.', dup_count, sample;
    END IF;
END $$;");

            migrationBuilder.CreateIndex(
                name: "IX_teams_DepartmentId_NameAr",
                table: "teams",
                columns: new[] { "DepartmentId", "NameAr" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_departments_NameAr",
                table: "departments",
                column: "NameAr",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_teams_DepartmentId_NameAr",
                table: "teams");

            migrationBuilder.DropIndex(
                name: "IX_departments_NameAr",
                table: "departments");
        }
    }
}
