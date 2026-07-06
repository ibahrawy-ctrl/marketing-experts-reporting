using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Reporting.Infrastructure.Persistence;

namespace KpiTemplateBinder;

/// <summary>
/// أداة آمنة لمرة واحدة: تربط 8 قوالب KPI الحالية بمسمّياتها الوظيفية بمطابقة العنوان تمامًا،
/// مع إبقاء «النبض الأسبوعي العام» قالبًا عامًا (JobRoleId = null) — وفق الـ Mapping المعتمد.
///
/// تعدّل KpiTemplate.JobRoleId فقط للقوالب الثمانية المحدّدة. لا تنشئ قوالب، لا تلمس
/// Versions ولا Metrics ولا Evaluations ولا المستخدمين. معاملة واحدة. idempotent.
///
/// الأوضاع:
///   (افتراضي)            Dry-Run لربط القوالب — لا يكتب شيئًا (Rollback).
///   --apply              تطبيق الربط فعليًّا (Commit).
///   --rollback           معاينة عكس الربط (إرجاع الثمانية إلى JobRoleId = null) — لا يكتب.
///   --rollback --apply   تطبيق العكس فعليًّا (Commit).
///
/// متطلّب بيئي: ConnectionStrings__Default.
/// </summary>
internal static class Program
{
    // الـ Mapping المعتمد: عنوان قالب KPI ⟶ رمز المسمى الوظيفي.
    // «النبض الأسبوعي العام» غير مذكور عمدًا ⇒ يبقى عامًا (JobRoleId = null).
    private static readonly (string Title, string Code)[] Bindings =
    {
        ("مؤشرات أداء المودريشن",      "SOCIAL_MOD"),
        ("مؤشرات أداء الفيديو",        "VIDEO_EDITOR"),
        ("مؤشرات أداء المصمم",         "DESIGNER"),
        ("مؤشرات أداء كاتب المحتوى",   "CONTENT_WRITER"),
        ("مؤشرات أداء SEO",            "SEO_SPECIALIST"),
        ("مؤشرات مشتري الإعلانات",     "MEDIA_BUYER"),
        ("مؤشرات مندوب المبيعات",      "SALES_B2B"),
        ("مؤشرات مندوب مبيعات B2C",    "SALES_B2C"),
    };

    private const string GeneralFallbackTitle = "النبض الأسبوعي العام";

    private static async Task<int> Main(string[] args)
    {
        var apply = args.Contains("--apply");
        var rollback = args.Contains("--rollback");

        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (string.IsNullOrWhiteSpace(conn))
        {
            Console.Error.WriteLine("✗ المتغير البيئي ConnectionStrings__Default مطلوب.");
            return 2;
        }

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddSimpleConsole(o => o.SingleLine = true).SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(conn));
        await using var sp = services.BuildServiceProvider();

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var mode = rollback ? "عكس الربط (Rollback)" : "ربط القوالب (Bind)";
        Console.WriteLine($"=== KpiTemplateBinder — {mode} — {(apply ? "وضع التطبيق (--apply)" : "وضع المعاينة (Dry-Run)")} ===");
        Console.WriteLine();

        await using var tx = await db.Database.BeginTransactionAsync();

        var roleIdByCode = await db.JobRoles
            .Where(j => j.Code != null)
            .ToDictionaryAsync(j => j.Code!, j => j.Id);
        var roleNameById = await db.JobRoles.ToDictionaryAsync(j => j.Id, j => j.NameAr);

        // عدّ ما قبل التنفيذ (على كامل جدول قوالب KPI).
        var totalBefore = await db.KpiTemplates.CountAsync();
        var generalBefore = await db.KpiTemplates.CountAsync(t => t.JobRoleId == null);
        var linkedBefore = totalBefore - generalBefore;

        var rows = new List<MapRow>();
        var warnings = new List<string>();
        var changeCount = 0;

        foreach (var (title, code) in Bindings)
        {
            Guid? targetRoleId = null;
            if (!rollback)
            {
                if (!roleIdByCode.TryGetValue(code, out var roleId))
                {
                    warnings.Add($"المسمى الوظيفي بالرمز «{code}» غير موجود — تخطّي قالب «{title}».");
                    continue;
                }
                targetRoleId = roleId;
            }

            var tpl = await db.KpiTemplates.FirstOrDefaultAsync(t => t.Title == title);
            if (tpl is null)
            {
                warnings.Add($"قالب KPI «{title}» غير موجود في قاعدة البيانات — لا تغيير.");
                continue;
            }

            bool willChange;
            string reason;
            string targetLabel;

            if (rollback)
            {
                willChange = tpl.JobRoleId is not null;
                targetLabel = "(NULL — عام)";
                reason = willChange ? "إرجاع إلى عام (JobRoleId = null)" : "عام مسبقًا (لا تغيير — idempotent)";
            }
            else
            {
                // idempotent: لا تربط إلا إن كان عامًا الآن (NULL) أو مربوطًا بدور مختلف.
                willChange = tpl.JobRoleId != targetRoleId;
                targetLabel = $"{code} = {(roleNameById.TryGetValue(targetRoleId!.Value, out var nm) ? nm : "?")}";
                reason = willChange
                    ? (tpl.JobRoleId is null ? $"مطابقة العنوان ⟶ {code}" : $"تصحيح ربط ⟶ {code}")
                    : "مربوط بالدور الصحيح مسبقًا (لا تغيير — idempotent)";
            }

            var current = tpl.JobRoleId is { } cur
                ? $"{cur} ({(roleNameById.TryGetValue(cur, out var cn) ? cn : "?")})"
                : "(NULL — عام)";
            rows.Add(new MapRow(title, current, rollback ? "(NULL — عام)" : targetLabel, willChange, reason));

            if (apply && willChange)
            {
                tpl.JobRoleId = rollback ? null : targetRoleId;
                changeCount++;
            }
            else if (!apply && willChange)
            {
                changeCount++;
            }
        }

        // تحقّق أن قالب الـ fallback العام موجود ويبقى عامًا (تشخيص فقط — لا يُعدَّل أبدًا).
        var fallback = await db.KpiTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Title == GeneralFallbackTitle);
        var fallbackNote = fallback is null
            ? $"⚠ قالب الـ fallback «{GeneralFallbackTitle}» غير موجود!"
            : $"قالب الـ fallback العام: «{GeneralFallbackTitle}» — JobRoleId={(fallback.JobRoleId?.ToString() ?? "NULL")} (لا يُمَسّ).";

        PrintMap(rows);
        if (warnings.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("التحذيرات:");
            foreach (var w in warnings) Console.WriteLine($"  ⚠ {w}");
        }

        // عدّ ما بعد (محسوب من الحالة الحالية للسياق قبل أي Rollback للمعاملة).
        var generalAfter = rollback ? generalBefore + changeCount : generalBefore - changeCount;
        var linkedAfter = totalBefore - generalAfter;

        Console.WriteLine();
        Console.WriteLine("— قبل/بعد —");
        Console.WriteLine($"  إجمالي قوالب KPI            : {totalBefore} (ثابت — لا إنشاء/حذف)");
        Console.WriteLine($"  قوالب عامة (قبل)           : {generalBefore}");
        Console.WriteLine($"  قوالب عامة (بعد)           : {generalAfter}");
        Console.WriteLine($"  قوالب مرتبطة بمسمى (قبل)   : {linkedBefore}");
        Console.WriteLine($"  قوالب مرتبطة بمسمى (بعد)   : {linkedAfter}");
        Console.WriteLine($"  عدد التغييرات في هذا التشغيل: {changeCount}");
        Console.WriteLine();
        Console.WriteLine($"  {fallbackNote}");

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
    }

    private static void PrintMap(List<MapRow> rows)
    {
        Console.WriteLine("خريطة الربط:");
        Console.WriteLine(new string('-', 92));
        foreach (var r in rows)
        {
            Console.WriteLine($"{(r.WillChange ? "→ سيتغيّر" : "• ثابت")}  | {r.Title}");
            Console.WriteLine($"    JobRoleId : {r.Current}  ⟶  {r.Target}");
            Console.WriteLine($"    السبب     : {r.Reason}");
        }
        Console.WriteLine(new string('-', 92));
    }

    private sealed record MapRow(string Title, string Current, string Target, bool WillChange, string Reason);
}
