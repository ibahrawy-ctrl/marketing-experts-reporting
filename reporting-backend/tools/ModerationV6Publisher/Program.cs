using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModerationV6Publisher;
using Reporting.Infrastructure.Persistence;

// ملاحظة: أداة آمنة لمرة واحدة خارج الحل (Reporting.sln). لا تُنشر مع الـ API.
// افتراضيًّا Dry-Run (لا كتابة). --apply يطبّق النشر داخل معاملة واحدة عند اجتياز كل التحقّقات.
// المتغيّر البيئي المطلوب: ConnectionStrings__Default.

var apply = args.Contains("--apply");

var conn = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
if (string.IsNullOrWhiteSpace(conn))
{
    Console.Error.WriteLine("✗ المتغيّر البيئي ConnectionStrings__Default مطلوب.");
    return 2;
}

var services = new ServiceCollection();
services.AddLogging(b => b.AddSimpleConsole(o => o.SingleLine = true).SetMinimumLevel(LogLevel.Warning));
services.AddDbContext<AppDbContext>(o => o.UseNpgsql(conn));
await using var sp = services.BuildServiceProvider();

using var scope = sp.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

var mode = apply ? "وضع التطبيق (--apply)" : "وضع المعاينة (Dry-Run)";
Console.WriteLine($"=== ModerationV6Publisher — نشر إصدار V6 لقالب المديرشن — {mode} ===");
Console.WriteLine();

var report = await Publisher.RunAsync(db, Publisher.ModerationTemplateId, apply);

PrintReport(report, apply);

// رمز الخروج: 0 نجاح خطة/تطبيق أو idempotent؛ 3 حظر/عدم تطابق/غياب V5.
return report.Outcome switch
{
    PublishOutcome.Planned => 0,
    PublishOutcome.Applied => 0,
    PublishOutcome.AlreadyApplied => 0,
    PublishOutcome.ContractMismatch => 3,
    PublishOutcome.V5NotFound => 3,
    _ => 3,
};

static void PrintReport(PublisherReport r, bool apply)
{
    Console.WriteLine("— النتيجة —");
    Console.WriteLine($"  الحصيلة (Outcome)            : {OutcomeAr(r.Outcome)}");
    Console.WriteLine();

    Console.WriteLine("— القالب والإصدارات —");
    Console.WriteLine($"  Template ID                 : {r.TemplateId}");
    Console.WriteLine($"  V5 Version ID               : {(r.V5VersionId?.ToString() ?? "—")}");
    Console.WriteLine($"  V5 Version Number           : {(r.V5VersionNumber?.ToString() ?? "—")}");
    Console.WriteLine($"  V5 field count              : {r.V5FieldCount}");
    Console.WriteLine($"  Proposed V6 field count     : {r.ProposedV6FieldCount}");
    Console.WriteLine($"  V6 Version ID (مقترح)        : {(r.V6VersionId?.ToString() ?? "—")}");
    Console.WriteLine($"  V6 Version Number (مقترح)    : {(r.V6VersionNumber?.ToString() ?? "—")}");
    Console.WriteLine($"  Added keys ({r.AddedKeys.Count})              : {(r.AddedKeys.Count > 0 ? string.Join(", ", r.AddedKeys) : "—")}");
    Console.WriteLine();

    Console.WriteLine("— المسودّات المرشّحة لإعادة التوجيه (W30 وأمثالها) —");
    if (r.Repoints.Count == 0)
    {
        Console.WriteLine("  (لا مسودّات مرشّحة على هذا القالب.)");
    }
    else
    {
        foreach (var d in r.Repoints)
        {
            Console.WriteLine($"  • Submission ID            : {d.SubmissionId}");
            Console.WriteLine($"    PeriodKey / Submitter    : {d.PeriodKey} / {d.SubmitterId}");
            Console.WriteLine($"    من إصدار                 : v{d.FromVersionNumber}");
            Console.WriteLine($"    W30 value count          : {d.ValueCount}");
            Console.WriteLine($"    W30 non-empty value count: {d.NonEmptyValueCount}");
            Console.WriteLine($"    Attachment count         : {d.AttachmentCount}");
            Console.WriteLine($"    Reference count          : {d.ReferenceCount}");
            Console.WriteLine($"    Unique-conflict check    : {(d.UniqueConflict ? "يوجد تعارض" : "لا تعارض")}");
            Console.WriteLine($"    مؤهَّلة؟                  : {(d.Eligible ? "نعم" : "لا")}");
            Console.WriteLine($"    أُعيد التوجيه فعليًّا؟      : {(d.Repointed ? "نعم" : "لا")}");
            Console.WriteLine($"    السبب                    : {d.Reason}");
        }
    }
    Console.WriteLine();

    Console.WriteLine("— الإجراءات المخطَّطة —");
    foreach (var line in PlannedActions(r, apply)) Console.WriteLine($"  {line}");
    Console.WriteLine();

    Console.WriteLine("— الهجرات (يجب أن تبقى ثابتة — الأداة لا تشغّل أي Migration) —");
    Console.WriteLine($"  Migration count before      : {r.MigrationCountBefore}");
    Console.WriteLine($"  Migration count after       : {r.MigrationCountAfter}");
    Console.WriteLine($"  ثابتة؟                       : {(r.MigrationCountBefore == r.MigrationCountAfter ? "نعم (unchanged)" : "⚠ تغيّرت!")}");
    Console.WriteLine();

    if (r.Notes.Count > 0)
    {
        Console.WriteLine("— ملاحظات —");
        foreach (var n in r.Notes) Console.WriteLine($"  • {n}");
        Console.WriteLine();
    }

    if (!string.IsNullOrEmpty(r.BlockReason))
    {
        Console.WriteLine("— سبب التوقّف/الحظر —");
        Console.WriteLine($"  ✗ {r.BlockReason}");
        Console.WriteLine();
    }

    switch (r.Outcome)
    {
        case PublishOutcome.Planned:
            Console.WriteLine("ℹ Dry-Run — لم يُكتب شيء (Rollback). للتطبيق مرّر --apply.");
            break;
        case PublishOutcome.Applied:
            Console.WriteLine("✓ تم اعتماد المعاملة (Commit). نُشِر V6 وأُعيد توجيه المسودّات المؤهَّلة فقط.");
            break;
        case PublishOutcome.AlreadyApplied:
            Console.WriteLine("✓ V6 مطابق موجود مسبقًا — لا تغيير (idempotent).");
            break;
        case PublishOutcome.ContractMismatch:
            Console.WriteLine("✗ ContractMismatch — توقّف بلا كتابة.");
            break;
        case PublishOutcome.V5NotFound:
            Console.WriteLine("✗ V5NotFound — توقّف بلا كتابة.");
            break;
    }
}

static IEnumerable<string> PlannedActions(PublisherReport r, bool apply)
{
    switch (r.Outcome)
    {
        case PublishOutcome.AlreadyApplied:
            yield return "لا إجراء — V6 مطابق للعقد موجود مسبقًا.";
            yield break;
        case PublishOutcome.ContractMismatch:
            yield return "لا إجراء — إصدار غير مطابق يحمل content_highlights (توقّف).";
            yield break;
        case PublishOutcome.V5NotFound:
            yield return "لا إجراء — V5 الصحيح غير موجود (توقّف).";
            yield break;
    }

    yield return $"إنشاء إصدار V6 (v{r.V6VersionNumber}) بنسخ حقول V5 حرفيًّا + إضافة 6 حقول تحليل محتوى داخل ProjectRepeatableSection.";
    yield return "الحفاظ على الـ15 حقلًا القائمة كما هي (Keys/Labels/Types/Options) — بلا حذف/تعديل/إعادة تسمية.";
    var eligible = r.Repoints.Count(d => d.Eligible);
    var blocked = r.Repoints.Count - eligible;
    yield return $"إعادة توجيه المسودّات المؤهَّلة فقط: {eligible} مؤهَّلة، {blocked} محظورة (تُترك كما هي).";
    yield return apply
        ? "نشر V6 (IsPublished=true) + Commit."
        : "Dry-Run: لن يُكتب شيء (Rollback).";
}

static string OutcomeAr(PublishOutcome o) => o switch
{
    PublishOutcome.Planned => "معاينة (Planned — Dry-Run، لا كتابة)",
    PublishOutcome.Applied => "مُطبَّق (Applied — Commit)",
    PublishOutcome.AlreadyApplied => "مطابق مسبقًا (AlreadyApplied — idempotent)",
    PublishOutcome.ContractMismatch => "عدم تطابق العقد (ContractMismatch — توقّف)",
    PublishOutcome.V5NotFound => "V5 غير موجود (V5NotFound — توقّف)",
    _ => o.ToString(),
};
