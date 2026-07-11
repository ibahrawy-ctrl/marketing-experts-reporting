using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Domain.Entities.Submissions;
using Reporting.Domain.Entities.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.IntegrationTests;

/// <summary>
/// ============================================================================
///  LEGACY HISTORICAL FIXTURE — بيانات تاريخية (Legacy) قبل الأرشفة — للاختبارات فقط
/// ============================================================================
/// الغرض (RC-4 · المسار الثاني المعتمَد من المالك):
///   قوالب الإنتاج القديمة الستة (Content/Design/Video/Social/MediaBuyer/Projects)
///   أصبحت <b>مؤرشفة</b> (Status=Archived, IsActive=false) فلا تسمح بإنشاء تسليم جديد
///   عبر الـAPI (الحارس يعيد <c>report.template_not_assigned</c> / HTTP 403).
///
///   هذا الـFixture <b>يحاكي بيانات تاريخية أُنشئت قبل الأرشفة</b> بزرعها مباشرةً في
///   قاعدة الاختبار المعزولة (<c>reporting_test</c>) عبر <see cref="AppDbContext"/>،
///   لإثبات أن التقارير القديمة تظلّ <b>قابلة للقراءة والتجميع</b> في محرّك ERDS Phase 5/5.5/6.
///
/// قيود صارمة (مطابقة لتوجيه المالك):
///   • يُستخدم داخل اختبارات التكامل <b>حصرًا</b> — ليس كودَ تشغيل، ولا يُعطِّل/يتجاوز أي حارس داخل التطبيق.
///   • لا يُنشئ تسليمًا عبر الـAPI من قالب مؤرشف؛ يكتب صفوف Legacy مباشرةً في قاعدة اختبار نظيفة معزولة.
///   • لا يمسّ Production/RC/TEST ولا القاعدة المشتركة خارج بيئة الاختبار.
///   • الحالة المزروعة دائمًا ليست Draft (Submitted/Closed) — كما كانت البيانات التاريخية فعليًّا.
/// </summary>
public static class LegacyExecutionFixture
{
    /// <summary>
    /// يزرع تسليمًا تاريخيًّا (Legacy — قبل الأرشفة) لقالب إنتاج قديم مباشرةً في قاعدة الاختبار،
    /// بربطه بإصدار القالب الذي يحمل جدول <paramref name="mainTableLabel"/> وكتابة الصفوف كـ string[][] في ValueJson.
    /// يعيد معرّف التسليم المزروع. الحالة الافتراضية Submitted (يقرؤها المحرّك؛ Draft فقط هو المُستبعَد).
    /// </summary>
    public static async Task<Guid> SeedLegacyHistoricalGridAsync(
        CustomWebApplicationFactory factory,
        string templateTitle,
        string mainTableLabel,
        Guid submitterId,
        Guid? teamId,
        string periodKey,
        string[][] rows,
        SubmissionStatus status = SubmissionStatus.Submitted,
        PeriodType periodType = PeriodType.Weekly,
        Guid? departmentId = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // القالب المؤرشف يبقى مزروعًا مع إصداراته وحقوله؛ نجده بالعنوان فقط (كما يفعل محرّك التجميع).
        var template = await db.ReportTemplates
            .Include(t => t.Versions).ThenInclude(v => v.Fields)
            .FirstOrDefaultAsync(t => t.Title == templateTitle)
            ?? throw new InvalidOperationException($"Legacy template not found by title: '{templateTitle}'.");

        var version = template.Versions
            .FirstOrDefault(v => v.Fields.Any(f => f.FieldType == FieldType.TableGrid && f.Label == mainTableLabel))
            ?? throw new InvalidOperationException(
                $"Legacy template '{templateTitle}' has no TableGrid field labelled '{mainTableLabel}'.");
        var gridField = version.Fields.First(f => f.FieldType == FieldType.TableGrid && f.Label == mainTableLabel);

        // تسليم تاريخيّ (Legacy) — أُنشئ افتراضًا قبل الأرشفة؛ حالته ليست Draft.
        var submission = new ReportSubmission
        {
            ReportTemplateVersionId = version.Id,
            SubmitterId = submitterId,
            TeamId = teamId,
            DepartmentId = departmentId,
            PeriodType = periodType,
            PeriodKey = periodKey,
            Status = status,
            SubmittedAtUtc = DateTime.UtcNow,
            ClosedAtUtc = status == SubmissionStatus.Closed ? DateTime.UtcNow : null,
        };
        submission.FieldValues.Add(new SubmissionFieldValue
        {
            TemplateFieldId = gridField.Id,
            ValueJson = JsonSerializer.Serialize(rows),
        });

        db.ReportSubmissions.Add(submission);
        await db.SaveChangesAsync();
        return submission.Id;
    }

    /// <summary>
    /// يقرأ حالة القالب (Status/IsActive) مباشرةً من قاعدة الاختبار — لإثبات بقاء القوالب القديمة مؤرشفة/غير نشطة.
    /// </summary>
    public static async Task<(TemplateStatus Status, bool IsActive)> GetTemplateStatusAsync(
        CustomWebApplicationFactory factory, string templateTitle)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var template = await db.ReportTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Title == templateTitle)
            ?? throw new InvalidOperationException($"Template not found by title: '{templateTitle}'.");
        return (template.Status, template.IsActive);
    }
}
