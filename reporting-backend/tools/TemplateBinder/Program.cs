using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Reporting.Application.Common;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace TemplateBinder;

/// <summary>
/// أداة آمنة لمرة واحدة: تربط قوالب التقارير (وقوالب KPI اختياريًّا) بالمسمّيات الوظيفية
/// بمطابقة العنوان تمامًا — مطابقة لخريطة OrgSeeder.SeedJobRoleBindingsAsync.
/// لا تلمس المستخدمين ولا تنشئ/تنقل أي بيانات تشغيلية. تضبط JobRoleId فقط حين يكون NULL.
/// التغيير الوحيد غير المتعلق بالربط: تصنيف «متابعة مقالات SEO» تكميليًّا (لازم للظهور الصحيح).
/// افتراضيًّا Dry-Run (لا يكتب شيئًا). يكتب فعليًّا فقط مع --apply.
/// قوالب KPI تُربط فقط مع --include-kpi (طلب المستخدم خصّ «قوالب التقارير»).
/// </summary>
internal static class Program
{
    // خريطة قوالب التقارير: العنوان → رمز المسمى الوظيفي (+ تصنيف جديد إن لزم).
    private static readonly (string Title, string Code, TemplateClassification? NewClassification)[] ReportBindings =
    {
        (B2cReportSchema.TemplateTitle,          "SALES_B2C",     null),
        (MediaBuyerReportSchema.TemplateTitle,   "MEDIA_BUYER",   null),
        (SeoReportSchema.TeamTemplateTitle,      "SEO_SPECIALIST", TemplateClassification.Primary),
        (SeoReportSchema.ArticlesTemplateTitle,  "SEO_SPECIALIST", TemplateClassification.Supplementary),
        (SeoReportSchema.LeaderTemplateTitle,    "SEO_TL",        null),
        (ContentWriterReportSchema.TemplateTitle,"CONTENT_WRITER",null),
        (DesignerReportSchema.TemplateTitle,     "DESIGNER",      null),
        (VideoReportSchema.TemplateTitle,        "VIDEO_EDITOR",  null),
        (ModerationReportSchema.TemplateTitle,   "SOCIAL_MOD",    null),
        ("التقرير المالي",                        "FIN_MGR",       null),
        ("تقرير المدير العام",                    "GM",            null),
        ("تقرير التيم ليدر للسوشيال ميديا",        "SOCIAL_TL",     null),
        ("📞 تقرير قائد فريق مبيعات B2C",          "SALES_B2C_TL",  null),
        ("🔍 تقرير التخطيط والجودة",               "PLAN_MGR",      null),
        ("تقرير الحسابات",                        "ACCOUNTANT",    null),
        ("تقرير مبيعات B2B",                      "SALES_B2B",     null),
        ("تقرير مدير المبيعات",                   "SALES_MGR",     null),
        ("💻 تقرير فريق الويب",                    "WEB_DEV",       null),
        ("📈 تقرير النمو والأداء — مدير الأداء",    "PERF_LEAD",     null),
    };

    // خريطة قوالب KPI (اختيارية — تُطبَّق فقط مع --include-kpi).
    private static readonly (string Title, string Code)[] KpiBindings =
    {
        (B2cReportSchema.KpiTitle,            "SALES_B2C"),
        ("مؤشرات مندوب المبيعات",              "SALES_B2B"),
        (MediaBuyerReportSchema.KpiTitle,     "MEDIA_BUYER"),
        (SeoReportSchema.KpiTitle,            "SEO_SPECIALIST"),
        (ContentWriterReportSchema.KpiTitle,  "CONTENT_WRITER"),
        (DesignerReportSchema.KpiTitle,       "DESIGNER"),
        (VideoReportSchema.KpiTitle,          "VIDEO_EDITOR"),
        (ModerationReportSchema.KpiTitle,     "SOCIAL_MOD"),
    };

    private static async Task<int> Main(string[] args)
    {
        var apply = args.Contains("--apply");
        var includeKpi = args.Contains("--include-kpi");

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

        Console.WriteLine(apply
            ? "=== ربط قوالب التقارير بالمسمّيات الوظيفية — وضع التطبيق (--apply) ==="
            : "=== ربط قوالب التقارير بالمسمّيات الوظيفية — وضع المعاينة (Dry-Run) ===");
        Console.WriteLine(includeKpi ? "نطاق: قوالب التقارير + قوالب KPI." : "نطاق: قوالب التقارير فقط (KPI مستثناة — مرّر --include-kpi لتضمينها).");
        Console.WriteLine();

        await using var tx = await db.Database.BeginTransactionAsync();

        var roleIdByCode = await db.JobRoles
            .Where(j => j.Code != null)
            .ToDictionaryAsync(j => j.Code!, j => j.Id);

        var rows = new List<MapRow>();
        var warnings = new List<string>();

        // ===== قوالب التقارير =====
        foreach (var (title, code, newClass) in ReportBindings)
        {
            if (!roleIdByCode.TryGetValue(code, out var roleId))
            {
                warnings.Add($"المسمى الوظيفي بالرمز «{code}» غير موجود — تخطّي قالب «{title}».");
                continue;
            }

            var tpl = await db.ReportTemplates.FirstOrDefaultAsync(t => t.Title == title);
            if (tpl is null)
            {
                warnings.Add($"قالب التقرير «{title}» غير موجود في قاعدة البيانات — لا ربط.");
                continue;
            }

            var willBind = tpl.JobRoleId is null;
            var willReclass = newClass is { } nc && tpl.Classification != nc && tpl.JobRoleId is null;

            string reason;
            if (!willBind)
                reason = "مربوط مسبقًا (لا تغيير — idempotent)";
            else
            {
                reason = $"مطابقة العنوان ⟶ {code}";
                if (willReclass) reason += $" + إعادة تصنيف ⟶ {newClass}";
            }

            rows.Add(new MapRow("Report", title, tpl.JobRoleId?.ToString() ?? "(NULL)", code,
                tpl.Classification.ToString(), willReclass ? newClass!.ToString()! : tpl.Classification.ToString(),
                willBind, reason));

            if (apply && willBind)
            {
                tpl.JobRoleId = roleId;
                if (willReclass) tpl.Classification = newClass!.Value;
            }
        }

        // ===== قوالب KPI (اختيارية) =====
        if (includeKpi)
        {
            foreach (var (title, code) in KpiBindings)
            {
                if (!roleIdByCode.TryGetValue(code, out var roleId))
                {
                    warnings.Add($"المسمى الوظيفي بالرمز «{code}» غير موجود — تخطّي قالب KPI «{title}».");
                    continue;
                }

                var kpi = await db.KpiTemplates.FirstOrDefaultAsync(t => t.Title == title);
                if (kpi is null)
                {
                    warnings.Add($"قالب KPI «{title}» غير موجود في قاعدة البيانات — لا ربط.");
                    continue;
                }

                var willBind = kpi.JobRoleId is null;
                rows.Add(new MapRow("KPI", title, kpi.JobRoleId?.ToString() ?? "(NULL)", code,
                    "—", "—", willBind, willBind ? $"مطابقة العنوان ⟶ {code}" : "مربوط مسبقًا (لا تغيير)"));

                if (apply && willBind) kpi.JobRoleId = roleId;
            }
        }

        PrintMap(rows);
        if (warnings.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("التحذيرات:");
            foreach (var w in warnings) Console.WriteLine($"  ⚠ {w}");
        }

        var toBind = rows.Count(r => r.WillBind);
        Console.WriteLine();
        Console.WriteLine($"الإجمالي: {rows.Count} قالب مفحوص — {toBind} سيُربط — {rows.Count - toBind} بلا تغيير — {warnings.Count} تحذير.");

        if (apply)
        {
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            Console.WriteLine();
            Console.WriteLine("✓ تم تطبيق الربط واعتماد المعاملة (Commit).");
        }
        else
        {
            await tx.RollbackAsync();
            Console.WriteLine();
            Console.WriteLine("ℹ Dry-Run — لم يُكتب أي تغيير (Rollback). للتطبيق مرّر --apply.");
        }

        return 0;
    }

    private static void PrintMap(List<MapRow> rows)
    {
        Console.WriteLine("خريطة الربط المقترحة:");
        Console.WriteLine(new string('-', 100));
        foreach (var r in rows)
        {
            var flag = r.WillBind ? "→ سيُربط" : "• ثابت";
            Console.WriteLine($"[{r.Kind,-6}] {flag}");
            Console.WriteLine($"   القالب     : {r.Title}");
            Console.WriteLine($"   JobRoleId  : {r.CurrentJobRoleId}  ⟶  {r.ProposedRoleCode}");
            if (r.Kind == "Report")
                Console.WriteLine($"   التصنيف    : {r.CurrentClassification}  ⟶  {r.ProposedClassification}");
            Console.WriteLine($"   السبب      : {r.Reason}");
            Console.WriteLine();
        }
        Console.WriteLine(new string('-', 100));
    }

    private sealed record MapRow(
        string Kind, string Title, string CurrentJobRoleId, string ProposedRoleCode,
        string CurrentClassification, string ProposedClassification, bool WillBind, string Reason);
}
