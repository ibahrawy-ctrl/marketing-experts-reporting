using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// P360-WF-R2 — تقدّم المشروع المشتقّ (GAP-02/03/21) + الحالات الأربع للصحّة (GAP-04/05).
    ///
    /// <para>
    /// **أربعة أعمدة إضافيّة + إعادة كتابة قيم نصّيّة قائمة.** الأعمدة كلّها بقيم افتراضيّة
    /// ⟹ صفر Backfill. أمّا إعادة الكتابة فليست تجميلًا: <c>projects."HealthStatus"</c> مخزَّن
    /// **نصًّا** (<c>HasConversion&lt;string&gt;</c>)، فإعادة تسمية قيم التعداد في الكود تجعل كلّ
    /// صفّ يحمل <c>'Yellow'</c> أو <c>'Red'</c> **غير قابل للقراءة** ويرمي عند التحويل.
    /// هذه الهجرة هي ما يمنع ذلك.
    /// </para>
    ///
    /// <para>
    /// **صفر إسقاط وصفر تغيير نوع وصفر فهرس محذوف** — إضافيّة بحتة بمعنى قاعدة الحوكمة.
    /// </para>
    /// </summary>
    public partial class AddProjectProgressAndHealthStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ProgressCalculatedAtUtc",
                table: "projects",
                type: "timestamp with time zone",
                nullable: true);

            // الافتراضيّ **NotCalculated** لا سلسلة فارغة: الفراغ لا يقابل أيّ قيمة في التعداد
            // فيرمي عند القراءة، و«لم يُحتسَب بعد» هو الوصف الصادق لكلّ صفّ قائم اليوم.
            migrationBuilder.AddColumn<string>(
                name: "ProgressMode",
                table: "projects",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "NotCalculated");

            migrationBuilder.AddColumn<int>(
                name: "ProgressSourceDeliverableCount",
                table: "projects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightPercentage",
                table: "project_deliverables",
                type: "numeric(9,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // ===== إعادة تسمية الحالات المخزَّنة نصًّا (P360-WF-R2 §7) =====
            // الترتيب مقصود: التسمية أوّلًا ثمّ تصنيف «لم تُقيَّم»، وإلّا صنّفنا صفوفًا ثمّ أعدنا تسميتها.
            migrationBuilder.Sql(@"UPDATE projects SET ""HealthStatus"" = 'AtRisk'  WHERE ""HealthStatus"" = 'Yellow';");
            migrationBuilder.Sql(@"UPDATE projects SET ""HealthStatus"" = 'Delayed' WHERE ""HealthStatus"" = 'Red';");

            // GAP-05 — مشروع بلا ختم احتساب لم تُقَس صحّته قطّ. كان يُخزَّن 'Green' لأنّه الافتراضيّ،
            // فكان **المشروع المهمَل هو الأكثر ظهورًا بالأخضر**. يُصنَّف الآن «لم تُقيَّم» صراحةً.
            migrationBuilder.Sql(@"UPDATE projects SET ""HealthStatus"" = 'NotEvaluated' WHERE ""HealthComputedAtUtc"" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // **تراجع مُعلَن الفقد**: `Blocked` و`NotEvaluated` لا مقابل لهما في التعداد القديم.
            // `NotEvaluated` تعود 'Green' — وهو بالضبط العيب الذي أصلحته هذه الهجرة، فالتراجع
            // يعيد السلوك القديم كما كان لا يخترع سلوكًا ثالثًا. و`Blocked` تعود 'Red' لأنّها
            // أشدّ ما يعبّر عنه التعداد القديم.
            migrationBuilder.Sql(@"UPDATE projects SET ""HealthStatus"" = 'Green' WHERE ""HealthStatus"" = 'NotEvaluated';");
            migrationBuilder.Sql(@"UPDATE projects SET ""HealthStatus"" = 'Red'   WHERE ""HealthStatus"" = 'Blocked';");
            migrationBuilder.Sql(@"UPDATE projects SET ""HealthStatus"" = 'Red'   WHERE ""HealthStatus"" = 'Delayed';");
            migrationBuilder.Sql(@"UPDATE projects SET ""HealthStatus"" = 'Yellow' WHERE ""HealthStatus"" = 'AtRisk';");

            migrationBuilder.DropColumn(
                name: "ProgressCalculatedAtUtc",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "ProgressMode",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "ProgressSourceDeliverableCount",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "WeightPercentage",
                table: "project_deliverables");
        }
    }
}
