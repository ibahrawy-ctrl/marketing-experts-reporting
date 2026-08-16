using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Reporting.Application.Common;
using Reporting.Infrastructure.Identity;
using Reporting.Infrastructure.Persistence;
using RoleAwareOverridePublisher;

// =====================================================================================
// RoleAwareOverridePublisher — أداة آمنة لمرة واحدة (ROLE-AWARE-PERSONAL-REPORT-SUBMISSION-ACCESS-R1)
//
// تضبط تجاوز اعتماد التقارير وتجاوز مراجِع KPI الصريحَين للأربعة المستهدَفين ليتوجّه اعتمادهم
// (تقارير + KPI) مباشرةً إلى المعتمِد (المدير التنفيذي إبراهيم) — دون أيّ تغيير في ManagerId أو
// TeamId أو DepartmentId أو نطاق الرؤية أو الإجازات أو الحوكمة.
//
// تعدّل حصريًّا حقلَين على المستخدم (كلاهما FK ذاتيّ اختياريّ أُضيف في migration
// 20260724224053_AddReportApproverAndKpiReviewerOverrides):
//   ApplicationUser.ReportApproverOverrideUserId
//   ApplicationUser.KpiReviewerOverrideUserId
// لا تلمس أيّ عمود آخر، ولا تنشئ/تحذف أيّ صفّ، ولا تكتب SQL مباشرًا.
//
// حلّ الهويّات: بالبريد الإلكترونيّ (من متغيّرات البيئة، لا هويّات مضمّنة في المصدر) ثم التحقّق من
// أنّ المستخدم موجود ونشط وله الدور المتوقَّع، وبعدها يُستخدَم UserId الفعليّ المحلول. المعتمِد يُتحقَّق
// أنّه نشط وله دور CEO. لا يُطبَّق أيّ تغيير إن فشل أيّ تحقّق (لا تطبيق جزئيّ).
//
// الأوضاع:
//   (افتراضي)            Dry-Run لضبط التجاوزات — لا يكتب شيئًا (Rollback للمعاملة).
//   --apply              تطبيق ضبط التجاوزات فعليًّا (Commit).
//   --rollback           معاينة عكس التجاوزات (إرجاع الحقلَين للأربعة إلى NULL) — لا يكتب.
//   --rollback --apply   تطبيق العكس فعليًّا (Commit).
//
// idempotent: إعادة التشغيل لا تُنتج تغييرات إضافية. لا تُطبع أيّ أسرار (كلمات مرور/توكنات/سلسلة اتصال).
//
// الإعداد عبر متغيّرات البيئة فقط:
//   ConnectionStrings__Default             سلسلة اتصال PostgreSQL (إلزامية).
//   OVERRIDE_APPROVER_EMAIL                بريد المعتمِد (المتوقَّع دوره: CEO).
//   OVERRIDE_SUBJECT_GM_EMAIL              بريد الموظّف المتوقَّع دوره: GeneralManager.
//   OVERRIDE_SUBJECT_HRADMIN_EMAIL         بريد الموظّف المتوقَّع دوره: HR أو Admin.
//   OVERRIDE_SUBJECT_MANAGER_EMAIL         بريد الموظّف المتوقَّع دوره: Manager أو FinanceManager.
//   OVERRIDE_SUBJECT_CEOSUPPORT_EMAIL      بريد الموظّف المتوقَّع دوره: CeoSupport.
// =====================================================================================

Console.OutputEncoding = Encoding.UTF8;

var apply = args.Contains("--apply", StringComparer.OrdinalIgnoreCase);
var rollback = args.Contains("--rollback", StringComparer.OrdinalIgnoreCase);

var conn = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
if (string.IsNullOrWhiteSpace(conn))
{
    Console.Error.WriteLine("خطأ: متغيّر البيئة ConnectionStrings__Default غير مضبوط.");
    return 2;
}

// عقد الأدوار المتوقَّعة لكل هدف (قاعدة أعمال، ليست هويّة) — البريد يأتي من البيئة.
var approverEmail = Environment.GetEnvironmentVariable("OVERRIDE_APPROVER_EMAIL");
var targets = new List<TargetSpec>
{
    new("OVERRIDE_SUBJECT_GM_EMAIL",        new[] { Roles.GeneralManager }),
    new("OVERRIDE_SUBJECT_HRADMIN_EMAIL",   new[] { Roles.Hr, Roles.Admin }),
    new("OVERRIDE_SUBJECT_MANAGER_EMAIL",   new[] { Roles.Manager, Roles.FinanceManager }),
    new("OVERRIDE_SUBJECT_CEOSUPPORT_EMAIL", new[] { Roles.CeoSupport }),
};

if (string.IsNullOrWhiteSpace(approverEmail))
{
    Console.Error.WriteLine("خطأ: OVERRIDE_APPROVER_EMAIL غير مضبوط.");
    return 2;
}
foreach (var t in targets)
{
    t.Email = Environment.GetEnvironmentVariable(t.EnvVar);
    if (string.IsNullOrWhiteSpace(t.Email))
    {
        Console.Error.WriteLine($"خطأ: {t.EnvVar} غير مضبوط.");
        return 2;
    }
}

var services = new ServiceCollection();
services.AddLogging(b => b.AddSimpleConsole(o => o.SingleLine = true).SetMinimumLevel(LogLevel.Warning));
services.AddDataProtection();
services.AddDbContext<AppDbContext>(o => o.UseNpgsql(conn));
services.AddIdentityCore<ApplicationUser>()
    .AddRoles<ApplicationRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

await using var sp = services.BuildServiceProvider();
using var scope = sp.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

var mode = rollback ? "عكس التجاوزات (Rollback)" : "ضبط التجاوزات (Bind)";
Console.WriteLine($"=== RoleAwareOverridePublisher — {mode} — {(apply ? "وضع التطبيق (--apply)" : "وضع المعاينة (Dry-Run)")} ===");
Console.WriteLine();

await using var tx = await db.Database.BeginTransactionAsync();

// ---- 1) حلّ المعتمِد والتحقّق (نشط + دور CEO) ----
var approver = await userMgr.FindByEmailAsync(approverEmail);
if (approver is null)
{
    Console.Error.WriteLine("خطأ: تعذّر العثور على المعتمِد بالبريد المُعطى.");
    await tx.RollbackAsync();
    return 3;
}
if (!approver.IsActive)
{
    Console.Error.WriteLine("خطأ: المعتمِد غير نشط.");
    await tx.RollbackAsync();
    return 3;
}
if (!await userMgr.IsInRoleAsync(approver, Roles.Ceo))
{
    Console.Error.WriteLine("خطأ: المعتمِد ليس بدور CEO المتوقَّع.");
    await tx.RollbackAsync();
    return 3;
}
var approverId = approver.Id;
Console.WriteLine($"المعتمِد: {approver.FullName} <{approver.Email}> — UserId={approverId} — CEO ✓ نشط ✓");
Console.WriteLine();

// ---- 2) حلّ الأربعة والتحقّق (نشط + الدور المتوقَّع + ليس المعتمِد نفسه) ----
var rows = new List<ResultRow>();
var hadError = false;
foreach (var t in targets)
{
    var u = await userMgr.FindByEmailAsync(t.Email!);
    if (u is null)
    {
        Console.Error.WriteLine($"خطأ: تعذّر العثور على مستخدِم {t.EnvVar}.");
        hadError = true;
        continue;
    }
    if (!u.IsActive)
    {
        Console.Error.WriteLine($"خطأ: مستخدِم {t.EnvVar} غير نشط.");
        hadError = true;
        continue;
    }
    var userRoles = await userMgr.GetRolesAsync(u);
    if (!t.ExpectedRoles.Any(r => userRoles.Contains(r)))
    {
        Console.Error.WriteLine($"خطأ: مستخدِم {t.EnvVar} ليس له أيّ من الأدوار المتوقَّعة ({string.Join("/", t.ExpectedRoles)}).");
        hadError = true;
        continue;
    }
    if (u.Id == approverId)
    {
        Console.Error.WriteLine($"خطأ: مستخدِم {t.EnvVar} هو نفسه المعتمِد — لا يجوز أن يعتمد نفسه.");
        hadError = true;
        continue;
    }

    var oldReport = u.ReportApproverOverrideUserId;
    var oldKpi = u.KpiReviewerOverrideUserId;
    Guid? newReport = rollback ? null : approverId;
    Guid? newKpi = rollback ? null : approverId;

    // في وضع الضبط: القيمة السابقة يُفترَض أنّها NULL (الحقلان جديدان). ننبّه إن لم تكن كذلك.
    if (!rollback && (oldReport is not null || oldKpi is not null) && oldReport != approverId)
        Console.WriteLine($"  ⚠ تنبيه: {t.EnvVar} يحمل قيمة تجاوز سابقة غير متوقَّعة (سيُعاد ضبطها إلى المعتمِد).");

    var willChange = oldReport != newReport || oldKpi != newKpi;
    if (apply && willChange)
    {
        u.ReportApproverOverrideUserId = newReport;
        u.KpiReviewerOverrideUserId = newKpi;
        // لا يُمَسّ ManagerId/TeamId/DepartmentId/BypassTeamLeaderApproval إطلاقًا.
        await userMgr.UpdateAsync(u);
    }

    rows.Add(new ResultRow(u.FullName, u.Email ?? "", u.Id, oldReport, oldKpi, newReport, newKpi, willChange));
}

if (hadError)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("✗ فشل التحقّق لواحد أو أكثر من المستهدَفين — لا تطبيق (معاملة مُلغاة).");
    await tx.RollbackAsync();
    return 3;
}

// ---- 3) طباعة مصفوفة قبل/بعد (بلا أسرار) ----
Console.WriteLine("— مصفوفة قبل/بعد —");
Console.WriteLine(new string('-', 96));
var changeCount = 0;
foreach (var r in rows)
{
    if (r.WillChange) changeCount++;
    Console.WriteLine($"{(r.WillChange ? "→ سيتغيّر" : "• ثابت")}  | {r.FullName} <{r.Email}> — UserId={r.UserId}");
    Console.WriteLine($"    ReportApproverOverrideUserId : {Fmt(r.OldReport)}  ⟶  {Fmt(r.NewReport)}");
    Console.WriteLine($"    KpiReviewerOverrideUserId    : {Fmt(r.OldKpi)}  ⟶  {Fmt(r.NewKpi)}");
}
Console.WriteLine(new string('-', 96));
Console.WriteLine();
Console.WriteLine($"عدد المستهدَفين: {rows.Count} — عدد التغييرات في هذا التشغيل: {changeCount}");
Console.WriteLine("لا يُمَسّ ManagerId / TeamId / DepartmentId / BypassTeamLeaderApproval لأيّ مستخدِم.");

if (apply)
{
    await db.SaveChangesAsync();
    await tx.CommitAsync();
    Console.WriteLine();
    Console.WriteLine($"✓ تم اعتماد المعاملة (Commit). تغييرات مطبَّقة: {changeCount}.");
}
else
{
    await tx.RollbackAsync();
    Console.WriteLine();
    Console.WriteLine($"ℹ Dry-Run — لم يُكتب شيء (Rollback). التغييرات المحتملة: {changeCount}. للتطبيق مرّر --apply.");
}

return 0;

static string Fmt(Guid? id) => id?.ToString() ?? "NULL";

namespace RoleAwareOverridePublisher
{
    internal sealed class TargetSpec
    {
        public TargetSpec(string envVar, string[] expectedRoles)
        {
            EnvVar = envVar;
            ExpectedRoles = expectedRoles;
        }

        public string EnvVar { get; }
        public string[] ExpectedRoles { get; }
        public string? Email { get; set; }
    }

    internal sealed record ResultRow(
        string FullName, string Email, Guid UserId,
        Guid? OldReport, Guid? OldKpi, Guid? NewReport, Guid? NewKpi, bool WillChange);
}
