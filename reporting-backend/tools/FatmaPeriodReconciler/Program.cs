using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Reporting.Domain.Entities.Submissions;
using Reporting.Domain.Entities.System;
using Reporting.Infrastructure.Persistence;

// =====================================================================================
// FatmaPeriodReconciler — أداة تسوية حصريّة وIdempotent لمفاتيح فترات تقارير موظّفة واحدة.
//
// الغرض: تصحيح انزياح الدورة لتقارير موظّفة بعينها فقط، بنقل PeriodKey خطوةً واحدةً للأمام
// وفق خطّة مُرتَّبة إلزاميًّا (الخطوة 1 تُفرِغ الفترة التي ستشغلها الخطوة 2).
//
// ما تفعله حصريًّا: تعديل العمود report_submissions."PeriodKey" (و"UpdatedAtUtc") للسجلّات
// المذكورة صراحةً في متغيّرات البيئة، وإضافة صفّ تدقيق لكلّ سجلّ.
// ما لا تفعله إطلاقًا: لا تلمس kpi_evaluations، ولا أيّ مستخدِم آخر، ولا أيّ تقرير غير المذكورَين،
// ولا السجلّات المحذوفة، ولا المعرّفات، ولا المحتوى (submission_field_values)، ولا الحالة (Status)،
// ولا الاعتمادات (approval_steps)، ولا CurrentApproverId، ولا PeriodType، ولا أيّ جدول آخر.
//
// الأوضاع:
//   (افتراضي)  DryRun — يقرأ ويطبع الخطّة والنتائج المتوقّعة ثم يُلغي المعاملة (لا كتابة إطلاقًا).
//   --apply    تطبيق فعليّ داخل معاملة واحدة، بعد كتابة ملفّ نسخة احتياطية/تراجع إلزاميّ.
//
// نتائج كلّ خطوة: Applied | AlreadyApplied | CollisionSkipped | SourceMismatchSkipped | NotFound.
// الالتزام (Commit) لا يحدث إلّا إذا انتهت كلّ الخطوات إلى Applied أو AlreadyApplied؛ وأيّ تخطٍّ
// (تصادم/عدم تطابق المصدر/غير موجود) يُلغي المعاملة بالكامل ولا يترك حالة نصفيّة.
// إعادة التشغيل بعد نجاح التطبيق ⇒ كلّ الخطوات AlreadyApplied و0 تغيير (Idempotent).
//
// الإعداد عبر متغيّرات البيئة فقط (لا هويّات مضمّنة في المصدر، ولا طباعة أسرار):
//   ConnectionStrings__Default      سلسلة اتصال PostgreSQL (إلزاميّة).
//   RECON_SUBMITTER_EMAIL           بريد الموظّفة المالكة للتقريرَين (حارس نطاق إلزاميّ).
//   RECON_STEP1_SUBMISSION_ID       معرّف التقرير الأوّل (يُنفَّذ أوّلًا).
//   RECON_STEP1_FROM / _TO          مفتاح الفترة المصدر / الهدف للخطوة الأولى.
//   RECON_STEP2_SUBMISSION_ID       معرّف التقرير الثاني (يُنفَّذ ثانيًا).
//   RECON_STEP2_FROM / _TO          مفتاح الفترة المصدر / الهدف للخطوة الثانية.
//   RECON_ACTOR_EMAIL               (اختياريّ) بريد منفّذ العملية لتسجيله في التدقيق.
//   RECON_BACKUP_DIR                (اختياريّ) مجلّد ملفّ التراجع، الافتراضيّ /tmp.
// =====================================================================================

Console.OutputEncoding = Encoding.UTF8;

var apply = args.Contains("--apply", StringComparer.OrdinalIgnoreCase);

var conn = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
if (string.IsNullOrWhiteSpace(conn))
{
    Console.Error.WriteLine("خطأ: متغيّر البيئة ConnectionStrings__Default غير مضبوط.");
    return 2;
}

var submitterEmail = Environment.GetEnvironmentVariable("RECON_SUBMITTER_EMAIL");
if (string.IsNullOrWhiteSpace(submitterEmail))
{
    Console.Error.WriteLine("خطأ: RECON_SUBMITTER_EMAIL غير مضبوط (حارس النطاق إلزاميّ).");
    return 2;
}

var plan = new List<Step>();
for (var i = 1; i <= 2; i++)
{
    var rawId = Environment.GetEnvironmentVariable($"RECON_STEP{i}_SUBMISSION_ID");
    var from = Environment.GetEnvironmentVariable($"RECON_STEP{i}_FROM");
    var to = Environment.GetEnvironmentVariable($"RECON_STEP{i}_TO");
    if (string.IsNullOrWhiteSpace(rawId) || string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
    {
        Console.Error.WriteLine($"خطأ: بيانات الخطوة {i} ناقصة (SUBMISSION_ID/FROM/TO).");
        return 2;
    }
    if (!Guid.TryParse(rawId, out var id))
    {
        Console.Error.WriteLine($"خطأ: معرّف الخطوة {i} ليس GUID صالحًا.");
        return 2;
    }
    plan.Add(new Step(i, id, from.Trim(), to.Trim()));
}

if (plan[0].SubmissionId == plan[1].SubmissionId)
{
    Console.Error.WriteLine("خطأ: الخطوتان تشيران إلى المعرّف نفسه.");
    return 2;
}

var actorEmail = Environment.GetEnvironmentVariable("RECON_ACTOR_EMAIL");
var backupDir = Environment.GetEnvironmentVariable("RECON_BACKUP_DIR");
if (string.IsNullOrWhiteSpace(backupDir)) backupDir = "/tmp";

var services = new ServiceCollection();
services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
services.AddDbContext<AppDbContext>(o => o.UseNpgsql(conn));
await using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

Console.WriteLine("=====================================================================");
Console.WriteLine($"FatmaPeriodReconciler — الوضع: {(apply ? "APPLY (تطبيق فعليّ)" : "DRY-RUN (معاينة بلا كتابة)")}");
Console.WriteLine("=====================================================================");

// حارس النطاق: تحديد الموظّفة المالكة من البريد (لا هويّة مضمّنة في المصدر).
var submitter = await db.Users.AsNoTracking()
    .Where(u => u.Email == submitterEmail)
    .Select(u => new { u.Id, u.FullName, u.IsActive })
    .FirstOrDefaultAsync();
if (submitter is null)
{
    Console.Error.WriteLine("خطأ: لم يُعثر على الموظّفة المالكة بالبريد المُعطى.");
    return 2;
}
Console.WriteLine($"الموظّفة المالكة: {submitter.FullName} (نشطة: {submitter.IsActive})");

Guid? actorId = null;
if (!string.IsNullOrWhiteSpace(actorEmail))
{
    actorId = await db.Users.AsNoTracking()
        .Where(u => u.Email == actorEmail).Select(u => (Guid?)u.Id).FirstOrDefaultAsync();
    if (actorId is null)
    {
        Console.Error.WriteLine("خطأ: RECON_ACTOR_EMAIL مضبوط لكن لم يُعثر على المستخدِم.");
        return 2;
    }
}

Console.WriteLine();
Console.WriteLine("— الحالة قبل التنفيذ (كلّ تقارير الموظّفة النشطة) —");
await PrintSubmissionsAsync(db, submitter.Id);

// ===== التنفيذ داخل معاملة واحدة =====
await using var tx = await db.Database.BeginTransactionAsync();

var results = new List<StepResult>();
var backupLines = new List<string>
{
    "-- FatmaPeriodReconciler — ملفّ تراجع (استعادة مفاتيح الفترة إلى ما قبل التسوية).",
    "-- يُنفَّذ يدويًّا عند الحاجة فقط، ولا يمسّ أيّ عمود آخر.",
    "BEGIN;",
};

foreach (var step in plan)
{
    var entity = await db.ReportSubmissions.FirstOrDefaultAsync(s => s.Id == step.SubmissionId);

    if (entity is null)
    {
        results.Add(new StepResult(step, "NotFound", "لم يُعثر على التقرير بهذا المعرّف."));
        continue;
    }
    if (entity.IsDeleted)
    {
        results.Add(new StepResult(step, "NotFound", "التقرير محذوف منطقيًّا — مستبعَد من النطاق."));
        continue;
    }
    if (entity.SubmitterId != submitter.Id)
    {
        results.Add(new StepResult(step, "SourceMismatchSkipped",
            "التقرير لا يخصّ الموظّفة المحدَّدة — رفض صارم لحماية بقيّة المستخدمين."));
        continue;
    }
    if (string.Equals(entity.PeriodKey, step.To, StringComparison.Ordinal))
    {
        results.Add(new StepResult(step, "AlreadyApplied", "مفتاح الفترة يساوي الهدف مسبقًا — لا تغيير."));
        continue;
    }
    if (!string.Equals(entity.PeriodKey, step.From, StringComparison.Ordinal))
    {
        results.Add(new StepResult(step, "SourceMismatchSkipped",
            $"مفتاح الفترة الحاليّ ({entity.PeriodKey}) لا يطابق المصدر المتوقَّع ({step.From})."));
        continue;
    }

    // تصادم مع القيد الفريد (ReportTemplateVersionId, SubmitterId, PeriodKey) للسجلّات غير المحذوفة.
    var collision = await db.ReportSubmissions.AsNoTracking().AnyAsync(s =>
        s.Id != entity.Id &&
        !s.IsDeleted &&
        s.SubmitterId == entity.SubmitterId &&
        s.ReportTemplateVersionId == entity.ReportTemplateVersionId &&
        s.PeriodKey == step.To);
    if (collision)
    {
        results.Add(new StepResult(step, "CollisionSkipped",
            $"يوجد تقرير نشط آخر للموظّفة نفسها على الإصدار نفسه في الفترة {step.To}."));
        continue;
    }

    backupLines.Add(
        $"UPDATE report_submissions SET \"PeriodKey\" = '{step.From}' WHERE \"Id\" = '{entity.Id}' AND \"PeriodKey\" = '{step.To}';");

    entity.PeriodKey = step.To;
    entity.UpdatedAtUtc = DateTime.UtcNow;

    db.AuditLogs.Add(new AuditLog
    {
        Id = Guid.NewGuid(),
        ActorId = actorId,
        Action = "submission.period_reconciled",
        EntityType = nameof(ReportSubmission),
        EntityId = entity.Id,
        DataJson = JsonSerializer.Serialize(new
        {
            tool = "FatmaPeriodReconciler",
            stepOrder = step.Order,
            fromPeriodKey = step.From,
            toPeriodKey = step.To,
            submitterId = entity.SubmitterId,
            reportTemplateVersionId = entity.ReportTemplateVersionId,
            status = entity.Status.ToString(),
            reason = "تسوية انزياح دورة التقارير لهذه الموظّفة حصريًّا — بلا تغيير للمحتوى أو الحالة أو الاعتمادات.",
        }),
        CreatedAtUtc = DateTime.UtcNow,
    });

    await db.SaveChangesAsync();
    results.Add(new StepResult(step, "Applied", $"نُقل مفتاح الفترة من {step.From} إلى {step.To}."));
}

backupLines.Add("COMMIT;");

Console.WriteLine();
Console.WriteLine("— نتائج الخطوات (بالترتيب الإلزاميّ) —");
foreach (var r in results)
    Console.WriteLine($"  الخطوة {r.Step.Order}: {r.Step.SubmissionId} | {r.Step.From} → {r.Step.To} | {r.Outcome} | {r.Detail}");

var appliedCount = results.Count(r => r.Outcome == "Applied");
var blocked = results.Where(r => r.Outcome is not ("Applied" or "AlreadyApplied")).ToList();

if (blocked.Count > 0)
{
    await tx.RollbackAsync();
    Console.WriteLine();
    Console.WriteLine("✗ أُلغيت المعاملة بالكامل — توجد خطوة غير قابلة للتنفيذ، ولا تُترك حالة نصفيّة.");
    return 3;
}

if (!apply)
{
    await tx.RollbackAsync();
    Console.WriteLine();
    Console.WriteLine($"DRY-RUN: التغييرات المحتملة = {appliedCount}. أُلغيت المعاملة، لم تُكتب أيّ بيانات.");
    Console.WriteLine("لتطبيق الخطّة فعليًّا أعد التشغيل بـ --apply.");
    return 0;
}

// نسخة احتياطية/تراجع إلزاميّة قبل الالتزام.
var backupPath = Path.Combine(backupDir,
    $"fatma-period-reconciler-rollback-{DateTime.UtcNow:yyyyMMdd-HHmmss}.sql");
try
{
    await File.WriteAllTextAsync(backupPath, string.Join(Environment.NewLine, backupLines) + Environment.NewLine);
}
catch (Exception ex)
{
    await tx.RollbackAsync();
    Console.Error.WriteLine($"✗ تعذّرت كتابة ملفّ التراجع ({ex.GetType().Name}) — أُلغيت المعاملة ولم يُطبَّق شيء.");
    return 4;
}

await tx.CommitAsync();
Console.WriteLine();
Console.WriteLine($"✓ Commit — عدد التغييرات المُطبَّقة: {appliedCount}.");
Console.WriteLine($"ملفّ التراجع: {backupPath}");

Console.WriteLine();
Console.WriteLine("— الحالة بعد التنفيذ (كلّ تقارير الموظّفة النشطة) —");
db.ChangeTracker.Clear();
await PrintSubmissionsAsync(db, submitter.Id);

return 0;

static async Task PrintSubmissionsAsync(AppDbContext db, Guid submitterId)
{
    var rows = await db.ReportSubmissions.AsNoTracking()
        .Where(s => s.SubmitterId == submitterId && !s.IsDeleted)
        .OrderBy(s => s.PeriodKey)
        .Select(s => new { s.Id, s.PeriodKey, s.Status, s.PeriodType })
        .ToListAsync();
    if (rows.Count == 0) { Console.WriteLine("  (لا تقارير نشطة)"); return; }
    foreach (var r in rows)
        Console.WriteLine($"  {r.Id} | {r.PeriodKey} | {r.PeriodType} | {r.Status}");
}

internal sealed record Step(int Order, Guid SubmissionId, string From, string To);

internal sealed record StepResult(Step Step, string Outcome, string Detail);
