using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Domain.Entities.EmployeeServices;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Entities.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Identity;

namespace Reporting.Infrastructure.Persistence;

/// <summary>
/// يزرع الهيكل التنظيمي الفعلي لشركة خبراء التسويق (بيئة التطوير فقط) — الأسماء وخطوط الرفع
/// مأخوذة من «خريطة الموظفين مقابل الداشبورد» في ملف
/// Dashboard_Role_Data_Binding_Handoff_MarketingExperts_AR_v2 (الأقسام 2 و3 و4).
/// نطاق الرؤية يُحسب من سلسلة ManagerId. كما يُنشئ الإدارات والفرق ويربط المستخدمين بها.
/// idempotent على مستويين: المستخدمون (إن وُجد المدير التنفيذي) والإدارات (إن وُجدت).
/// كلمة المرور الموحّدة: Pass#2026.
/// </summary>
public static class OrgSeeder
{
    public const string DefaultPassword = "Pass#2026";

    private const string CeoEmail = "ibrahim.bahrawi@marketingexperts.local";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db = services.GetRequiredService<AppDbContext>();

        await SeedPeopleAsync(users);
        await SeedOrgStructureAsync(users, db);
        await SeedJobRolesAsync(db);
        await SeedJobRoleBindingsAsync(db);
        await SeedBalancePoliciesAsync(db);
    }

    // ===== سياسة رصيد عامّة لسنة 2026 (بيئة التطوير فقط، عبر OrgSeeder) =====
    // PermissionUnit=Count، حدّ شهري للأذونات=2، السماح بالرصيد السالب=true، عامّة (JobRoleId=null).
    // idempotent: لا تُنشأ إن وُجدت سياسة عامّة لنفس السنة (الفهرس الفريد على (Year, JobRoleId)).
    // لا تمسّ الإنتاج إطلاقًا — OrgSeeder لا يعمل إلا في Development.
    private static async Task SeedBalancePoliciesAsync(AppDbContext db)
    {
        const int year = 2026;
        var exists = await db.BalancePolicies.AnyAsync(p => p.Year == year && p.JobRoleId == null);
        if (exists) return;

        db.BalancePolicies.Add(new BalancePolicy
        {
            Year = year,
            JobRoleId = null,
            PermissionUnit = PermissionUnit.Count,
            PermissionMonthlyLimit = 2,
            PermissionAnnualLimit = null,
            AllowNegativeBalance = true
        });
        await db.SaveChangesAsync();
    }

    // ===== ربط المستخدمين بالمسميات الوظيفية + ربط قالب B2C الفردي بدور المندوب =====
    // idempotent: لا يُحدّث إلا ما كان فارغًا (JobRoleId == null) فلا يلمس بيانات مضبوطة سابقًا.
    private static async Task SeedJobRoleBindingsAsync(AppDbContext db)
    {
        var roleIdByCode = await db.JobRoles
            .Where(j => j.Code != null)
            .ToDictionaryAsync(j => j.Code!, j => j.Id);

        Guid? Role(string code) => roleIdByCode.TryGetValue(code, out var id) ? id : null;

        // خريطة البريد → رمز المسمى الوظيفي (مطابقة لـ SeedPeopleAsync).
        var userJobByEmail = new Dictionary<string, string>
        {
            ["ibrahim.bahrawi@marketingexperts.local"] = "CEO",
            ["ahmed.abdelraouf@marketingexperts.local"] = "GM",
            ["mohamed.abdelqawi@marketingexperts.local"] = "SALES_MGR",
            // خالد قائد فريق B2C (لا مندوب): مسمّاه الوظيفي قيادي ليظهر له «قالب قائد فريق B2C» لا قالب المندوب.
            ["khaled.tl@marketingexperts.local"] = "SALES_B2C_TL",
            ["zainab.emp@marketingexperts.local"] = "SALES_B2C",
            ["reem.emp@marketingexperts.local"] = "SALES_B2C",
            ["aisha.emp@marketingexperts.local"] = "SALES_B2C",
            ["marwan.emp@marketingexperts.local"] = "SALES_B2C",
            ["shrouk.emp@marketingexperts.local"] = "SALES_B2B",
            ["mahmoud.alqousi@marketingexperts.local"] = "PERF_LEAD",
            ["ahmed.abdelfattah@marketingexperts.local"] = "MEDIA_BUYER",
            // نرمين مديرة التخطيط والجودة: مسمّى وظيفي إداري ليظهر لها «قالب التخطيط والجودة» في «إنشاء تقريري».
            ["nermin.mgr@marketingexperts.local"] = "PLAN_MGR",
            ["basant.social@marketingexperts.local"] = "SOCIAL_TL",
            // Business-1D-1: عضوا بود السوشيال الأول كاتبا محتوى تحت قيادة بسنت (لاختبار تجميع كاتب المحتوى).
            ["samar.social@marketingexperts.local"] = "CONTENT_WRITER",
            ["mohamed.ibrahim@marketingexperts.local"] = "CONTENT_WRITER",
            ["ahmed.sobhy@marketingexperts.local"] = "SOCIAL_MOD",
            ["amira.social@marketingexperts.local"] = "SOCIAL_TL",
            // Business-1D-2: عضوا بود السوشيال الثاني مصمّمَا جرافيك تحت قيادة أميرة (لاختبار تجميع التصميم).
            ["esraa.social@marketingexperts.local"] = "DESIGNER",
            ["nada.social@marketingexperts.local"] = "DESIGNER",
            ["ahmed.atef@marketingexperts.local"] = "SOCIAL_MOD",
            // Business-1D-3: محرّرا فيديو تحت قيادة أميرة (بود السوشيال الثاني) لاختبار تجميع الفيديو.
            ["kareem.video@marketingexperts.local"] = "VIDEO_EDITOR",
            ["hossam.video@marketingexperts.local"] = "VIDEO_EDITOR",
            // Business-1D-4: مودريتر إضافي تحت قيادة أميرة (مع أحمد عاطف) لاختبار تجميع المودريشن على مستوى الفريق.
            ["tarek.mod@marketingexperts.local"] = "SOCIAL_MOD",
            ["shaimaa.seo@marketingexperts.local"] = "SEO_TL",
            ["nour.emp@marketingexperts.local"] = "SEO_SPECIALIST",
            ["abdelrahman.emp@marketingexperts.local"] = "SEO_SPECIALIST",
            ["amir.web@marketingexperts.local"] = "WEB_TL",
            ["ahmed.nassar@marketingexperts.local"] = "WEB_DEV",
            ["mohamed.abdullah@marketingexperts.local"] = "FIN_MGR",
            ["youssef.emp@marketingexperts.local"] = "ACCOUNTANT",
        };

        var emails = userJobByEmail.Keys.ToList();
        var dbUsers = await db.Users.Where(u => u.Email != null && emails.Contains(u.Email)).ToListAsync();
        var changed = false;
        foreach (var u in dbUsers)
        {
            if (u.JobRoleId is not null) continue;
            if (u.Email is { } email && userJobByEmail.TryGetValue(email, out var code) && Role(code) is { } roleId)
            {
                u.JobRoleId = roleId;
                changed = true;
            }
        }

        // B2C-UAT-FIXPACK + Phase 7.1 — الجزء 1: تفعيل «تقرير مبيعات B2C — بيانات جديدة/قديمة» (قالب الجدولين)
        // لمندوبي B2C بدل القالب أحادي الجدول القديم. قالب الجدولين يُربَط بدور SALES_B2C فيصبح القالب الأخصّ
        // (JobRole tier) لمندوب B2C ⇒ يظهر له وحده (يُخفي العام). القالبان القديمان يُنقلان إلى حالة Legacy:
        //  - «تقرير مندوب مبيعات B2C الفردي» (B2cReportSchema) — القديم جدًّا.
        //  - «تقرير مبيعات B2C حسب الدورة» (B2cByCourseReportSchema) — أحادي الجدول، كان مُفعَّلًا سابقًا.
        // Legacy = يُفكّ ربطه بالدور ويُعطَّل (IsActive=false + Archived) كي لا يظهر في إنشاء التقارير الجديدة
        // ولا يتسرّب كقالب عام — دون حذفه (التقارير القديمة محفوظة عبر الإصدار).
        // idempotent: التغيير يُطبَّق فقط عند اختلاف الحالة الحالية عن المستهدفة (يعمل ولو كانت قاعدة الاختبار مبذورة مسبقًا).
        if (Role("SALES_B2C") is { } b2cRoleId)
        {
            var newB2cTemplate = await db.ReportTemplates
                .Include(t => t.Versions).ThenInclude(v => v.Fields)
                .FirstOrDefaultAsync(t => t.Title == B2cNewOldReportSchema.TemplateTitle);
            if (newB2cTemplate is not null && newB2cTemplate.JobRoleId != b2cRoleId)
            {
                newB2cTemplate.JobRoleId = b2cRoleId;
                newB2cTemplate.Classification = TemplateClassification.Primary;
                changed = true;
            }

            // Phase 7.1 — مواءمة أعمدة جدولَي القالب مع الـSchema (بعد حذف Lost/Lost Reason): القالب مزروع مسبقًا
            // في القواعد الموجودة و TemplateSeeder يتخطّى القوالب الموجودة، لذا نحدّث ConfigJson هنا كي يتوقّف الجدولان
            // عند Revenue. حذف عمودَي النهاية آمن (التجميع مفهرس بالاسم عبر Array.IndexOf) والقالب لم يُستخدم بعد.
            if (newB2cTemplate is not null && ReconcileGridColumns(newB2cTemplate)) changed = true;

            // القوالب B2C القديمة تُنقَل إلى Legacy (الفردي + أحادي الجدول حسب الدورة).
            var legacyB2cTitles = new[] { B2cReportSchema.TemplateTitle, B2cByCourseReportSchema.TemplateTitle };
            var legacyB2cTemplates = await db.ReportTemplates
                .Where(t => legacyB2cTitles.Contains(t.Title))
                .ToListAsync();
            foreach (var legacyB2cTemplate in legacyB2cTemplates)
            {
                if (legacyB2cTemplate.JobRoleId is not null
                    || legacyB2cTemplate.IsActive || legacyB2cTemplate.Status != TemplateStatus.Archived)
                {
                    legacyB2cTemplate.JobRoleId = null;
                    legacyB2cTemplate.IsActive = false;
                    legacyB2cTemplate.Status = TemplateStatus.Archived;
                    changed = true;
                }
            }
        }

        // RC-3 Task 2 — إعادة بناء تقرير B2B حسب الخدمة: القالب المُهيكَل الجديد
        // «📊 تقرير مبيعات B2B حسب الخدمة» (B2bByServiceReportSchema) يُربَط بمندوب B2B كأساسي (يظهر له وحده)،
        // والقالب القديم أحادي «تقرير مبيعات B2B» يُنقَل إلى Legacy (يُفكّ ربطه ويُعطَّل + Archived) دون حذف
        // (التقارير القديمة محفوظة عبر الإصدار). idempotent: التغيير عند اختلاف الحالة فقط.
        if (Role("SALES_B2B") is { } b2bRoleId)
        {
            var newB2bTemplate = await db.ReportTemplates
                .FirstOrDefaultAsync(t => t.Title == B2bByServiceReportSchema.TemplateTitle);
            if (newB2bTemplate is not null && newB2bTemplate.JobRoleId != b2bRoleId)
            {
                newB2bTemplate.JobRoleId = b2bRoleId;
                newB2bTemplate.Classification = TemplateClassification.Primary;
                changed = true;
            }

            var legacyB2bTemplate = await db.ReportTemplates
                .FirstOrDefaultAsync(t => t.Title == "تقرير مبيعات B2B");
            if (legacyB2bTemplate is not null
                && (legacyB2bTemplate.JobRoleId is not null
                    || legacyB2bTemplate.IsActive || legacyB2bTemplate.Status != TemplateStatus.Archived))
            {
                legacyB2bTemplate.JobRoleId = null;
                legacyB2bTemplate.IsActive = false;
                legacyB2bTemplate.Status = TemplateStatus.Archived;
                changed = true;
            }

            // RC-3 — فصل مصدر بيانات B2B: القالب المُهيكَل الجديد بجدولين «📊 تقرير مبيعات B2B — حسب مصدر البيانات»
            // (B2bBySourceReportSchema) يُربَط بمندوب B2B كأساسي (يظهر له وحده)، والقالب أحادي الجدول السابق
            // «حسب الخدمة» يُنقَل إلى Legacy (يُفكّ ربطه ويُعطَّل + Archived) دون حذف — التقارير القديمة محفوظة
            // عبر الإصدار والتجميع لا يزال يقرؤها. idempotent: التغيير عند اختلاف الحالة فقط.
            var newB2bSourceTemplate = await db.ReportTemplates
                .FirstOrDefaultAsync(t => t.Title == B2bBySourceReportSchema.TemplateTitle);
            if (newB2bSourceTemplate is not null && newB2bSourceTemplate.JobRoleId != b2bRoleId)
            {
                newB2bSourceTemplate.JobRoleId = b2bRoleId;
                newB2bSourceTemplate.Classification = TemplateClassification.Primary;
                changed = true;
            }

            var singleTableB2bTemplate = await db.ReportTemplates
                .FirstOrDefaultAsync(t => t.Title == B2bByServiceReportSchema.TemplateTitle);
            if (singleTableB2bTemplate is not null
                && (singleTableB2bTemplate.JobRoleId is not null
                    || singleTableB2bTemplate.IsActive || singleTableB2bTemplate.Status != TemplateStatus.Archived))
            {
                singleTableB2bTemplate.JobRoleId = null;
                singleTableB2bTemplate.IsActive = false;
                singleTableB2bTemplate.Status = TemplateStatus.Archived;
                changed = true;
            }
        }

        // Business-1B: ربط قالب «تقرير النمو والأداء — Media Buyer» بدور مشتري الإعلانات ليظهر له فقط.
        if (Role("MEDIA_BUYER") is { } mediaBuyerRoleId)
        {
            var mbTemplate = await db.ReportTemplates
                .FirstOrDefaultAsync(t => t.Title == MediaBuyerReportSchema.TemplateTitle);
            if (mbTemplate is not null && mbTemplate.JobRoleId is null)
            {
                mbTemplate.JobRoleId = mediaBuyerRoleId;
                changed = true;
            }
        }

        // Business-1C: ربط قوالب تقارير SEO بأدوارها ليظهر كلٌّ لدوره فقط.
        // قالبا «🔍 تقرير فريق SEO» و«متابعة مقالات SEO» للأخصائي؛ «تقرير قائد فريق SEO» للقائد.
        if (Role("SEO_SPECIALIST") is { } seoSpecialistRoleId)
        {
            var seoSpecTitles = new[] { SeoReportSchema.TeamTemplateTitle, SeoReportSchema.ArticlesTemplateTitle };
            var seoSpecTemplates = await db.ReportTemplates
                .Where(t => seoSpecTitles.Contains(t.Title) && t.JobRoleId == null)
                .ToListAsync();
            foreach (var t in seoSpecTemplates)
            {
                t.JobRoleId = seoSpecialistRoleId;
                // UAT Phase 3 — البند 9: لأخصائي SEO قالبان أسبوعيّان. نُبقي «تقرير فريق SEO»
                // أساسيًّا (إلزامي) ونصنّف «متابعة مقالات SEO» تكميليًّا (اختياري) لمنع ازدواج
                // تقريرَين إلزاميَّين لنفس الموظّف. الدمج الكامل موثّق كدَيْن تصميمي (حوكمة HR).
                t.Classification = t.Title == SeoReportSchema.ArticlesTemplateTitle
                    ? TemplateClassification.Supplementary
                    : TemplateClassification.Primary;
                changed = true;
            }
        }

        if (Role("SEO_TL") is { } seoLeaderRoleId)
        {
            var seoLeaderTemplate = await db.ReportTemplates
                .FirstOrDefaultAsync(t => t.Title == SeoReportSchema.LeaderTemplateTitle);
            if (seoLeaderTemplate is not null && seoLeaderTemplate.JobRoleId is null)
            {
                seoLeaderTemplate.JobRoleId = seoLeaderRoleId;
                changed = true;
            }
        }

        // Business-1D-1: ربط قالب «تقرير كاتب المحتوى الأسبوعي» بدور كاتب المحتوى ليظهر له فقط.
        if (Role("CONTENT_WRITER") is { } contentWriterRoleId)
        {
            var cwTemplate = await db.ReportTemplates
                .FirstOrDefaultAsync(t => t.Title == ContentWriterReportSchema.TemplateTitle);
            if (cwTemplate is not null && cwTemplate.JobRoleId is null)
            {
                cwTemplate.JobRoleId = contentWriterRoleId;
                changed = true;
            }
        }

        // Business-1D-2: ربط قالب «تقرير فريق التصميم» بدور مصمم الجرافيك ليظهر له فقط.
        if (Role("DESIGNER") is { } designerRoleId)
        {
            var designerTemplate = await db.ReportTemplates
                .FirstOrDefaultAsync(t => t.Title == DesignerReportSchema.TemplateTitle);
            if (designerTemplate is not null && designerTemplate.JobRoleId is null)
            {
                designerTemplate.JobRoleId = designerRoleId;
                changed = true;
            }
        }

        // Business-1D-3: ربط قالب «تقرير فريق الفيديو» بدور محرّر الفيديو ليظهر له فقط.
        if (Role("VIDEO_EDITOR") is { } videoRoleId)
        {
            var videoTemplate = await db.ReportTemplates
                .FirstOrDefaultAsync(t => t.Title == VideoReportSchema.TemplateTitle);
            if (videoTemplate is not null && videoTemplate.JobRoleId is null)
            {
                videoTemplate.JobRoleId = videoRoleId;
                changed = true;
            }
        }

        // Business-1D-4: ربط قالب «تقرير المديرشن الأسبوعي» بدور مشرف السوشيال (المودريتر) ليظهر له فقط.
        if (Role("SOCIAL_MOD") is { } modRoleId)
        {
            var modTemplate = await db.ReportTemplates
                .FirstOrDefaultAsync(t => t.Title == ModerationReportSchema.TemplateTitle);
            if (modTemplate is not null && modTemplate.JobRoleId is null)
            {
                modTemplate.JobRoleId = modRoleId;
                changed = true;
            }
        }

        // UAT-Fix-1 + إصلاح أولوية قالب «تقريري»: ربط القوالب الخاصة بأدوار وظيفية محدّدة
        // (بما فيها القوالب القيادية) لمنع ظهورها للجميع وضمان أن يرى كلّ صاحب دور قالبه فقط:
        // قائد فريق B2C ⟶ SALES_B2C_TL، التخطيط والجودة ⟶ PLAN_MGR، المدير العام ⟶ GM …
        // القوالب الباقية بلا مسمّى وظيفي مطابق (الأكاديمية/الموارد البشرية/مدير الحسابات)
        // تبقى بلا ربط (مرئية للأدمن فقط) — موثّقة كمرحلة لاحقة. الربط idempotent (لا يلمس قالبًا مربوطًا).
        var titleToRole = new (string Title, string Code)[]
        {
            ("التقرير المالي", "FIN_MGR"),
            ("تقرير المدير العام", "GM"),
            ("تقرير التيم ليدر للسوشيال ميديا", "SOCIAL_TL"),
            ("📞 تقرير قائد فريق مبيعات B2C", "SALES_B2C_TL"),
            ("🔍 تقرير التخطيط والجودة", "PLAN_MGR"),
            ("تقرير الحسابات", "ACCOUNTANT"),
            // RC-3 Task 2: القالب القديم «تقرير مبيعات B2B» لم يعد يُربَط هنا —
            // كتلة B2B أعلاه تربط القالب المُهيكَل الجديد وتنقل القديم إلى Legacy.
            ("تقرير مدير المبيعات", "SALES_MGR"),
            ("💻 تقرير فريق الويب", "WEB_DEV"),
            ("📈 تقرير النمو والأداء — مدير الأداء", "PERF_LEAD"),
        };
        foreach (var (title, code) in titleToRole)
        {
            if (Role(code) is not { } rid) continue;
            var tpl = await db.ReportTemplates.FirstOrDefaultAsync(t => t.Title == title);
            if (tpl is not null && tpl.JobRoleId is null)
            {
                tpl.JobRoleId = rid;
                changed = true;
            }
        }

        // UAT-Fix-1: ربط قوالب الـ KPI الخاصّة بمسارات محدّدة بمسمّياتها الوظيفية
        // حتى يُظهر نموذج التقييم للمدير فقط القوالب المناسبة للموظّف المُختار.
        // يبقى «النبض الأسبوعي العام» فقط بلا ربط (مؤشّر عام لكل الموظّفين). أمّا
        // «مؤشرات مندوب المبيعات» العامّة فتُربط بـ SALES_B2B (الدور الوحيد بلا قالب
        // مبيعات خاص) فلا تظهر لغير المبيعات. الربط idempotent (لا يلمس قالبًا مربوطًا).
        var kpiTitleToRole = new (string Title, string Code)[]
        {
            (B2cReportSchema.KpiTitle, "SALES_B2C"),
            ("مؤشرات مندوب المبيعات", "SALES_B2B"),
            (MediaBuyerReportSchema.KpiTitle, "MEDIA_BUYER"),
            (SeoReportSchema.KpiTitle, "SEO_SPECIALIST"),
            (ContentWriterReportSchema.KpiTitle, "CONTENT_WRITER"),
            (DesignerReportSchema.KpiTitle, "DESIGNER"),
            (VideoReportSchema.KpiTitle, "VIDEO_EDITOR"),
            (ModerationReportSchema.KpiTitle, "SOCIAL_MOD"),
        };
        foreach (var (title, code) in kpiTitleToRole)
        {
            if (Role(code) is not { } rid) continue;
            var kpi = await db.KpiTemplates.FirstOrDefaultAsync(t => t.Title == title);
            if (kpi is not null && kpi.JobRoleId is null)
            {
                kpi.JobRoleId = rid;
                changed = true;
            }
        }

        if (changed) await db.SaveChangesAsync();
    }

    // Phase 7.1 — مواءمة أعمدة جدولَي قالب «B2C بيانات جديدة/قديمة» مع الـSchema الحالي.
    // القالب مزروع مسبقًا و TemplateSeeder يتخطّى القوالب الموجودة (skip-if-exists) فلا يُحدّث الأعمدة،
    // لذا نُصلح ConfigJson لكل حقل TableGrid هنا كي يتوقّف الجدولان عند Revenue (بلا Lost/Lost Reason).
    // التنسيق يطابق TemplateSeeder.BuildConfigJson تمامًا: JsonSerializer.Serialize(new { columns }) بلا خيارات.
    // idempotent: يُرجِع true فقط عند اختلاف ConfigJson الحالي عن المستهدف.
    private static bool ReconcileGridColumns(ReportTemplate template)
    {
        var newLeadsConfig = JsonSerializer.Serialize(new { columns = B2cNewOldReportSchema.NewLeadsColumns });
        var oldCrmConfig = JsonSerializer.Serialize(new { columns = B2cNewOldReportSchema.OldCrmColumns });

        var changed = false;
        foreach (var version in template.Versions)
        {
            foreach (var field in version.Fields)
            {
                string? desired = field.Label switch
                {
                    var l when l == B2cNewOldReportSchema.NewLeadsTableLabel => newLeadsConfig,
                    var l when l == B2cNewOldReportSchema.OldCrmTableLabel => oldCrmConfig,
                    _ => null,
                };
                if (desired is null) continue;
                if (field.ConfigJson != desired)
                {
                    field.ConfigJson = desired;
                    changed = true;
                }
            }
        }
        return changed;
    }

    // ===== المسميات الوظيفية (idempotent على وجود أي مسمى) =====
    // تُربط بها قوالب التقارير من لوحة الأدمن. الإدارة اختيارية (تُربط بالرمز إن وُجدت الإدارة).
    private static async Task SeedJobRolesAsync(AppDbContext db)
    {
        if (await db.JobRoles.AnyAsync()) return;

        var deptByCode = await db.Departments.ToDictionaryAsync(d => d.Code ?? d.NameAr, d => d.Id);
        Guid? Dept(string code) => deptByCode.TryGetValue(code, out var id) ? id : null;

        var roles = new (string NameAr, string Code, string? DeptCode)[]
        {
            ("مندوب مبيعات B2C", "SALES_B2C", "SALES"),
            ("قائد فريق مبيعات B2C", "SALES_B2C_TL", "SALES"),
            ("مندوب مبيعات B2B", "SALES_B2B", "SALES"),
            ("مدير المبيعات", "SALES_MGR", "SALES"),
            ("مشتري إعلانات", "MEDIA_BUYER", "PERF"),
            ("قائد الأداء", "PERF_LEAD", "PERF"),
            ("كاتب محتوى", "CONTENT_WRITER", "PLAN"),
            ("مصمم جرافيك", "DESIGNER", "PLAN"),
            ("محرر فيديو", "VIDEO_EDITOR", "PLAN"),
            ("مشرف سوشيال", "SOCIAL_MOD", "PLAN"),
            ("قائد فريق السوشيال", "SOCIAL_TL", "PLAN"),
            ("أخصائي SEO", "SEO_SPECIALIST", "PLAN"),
            ("قائد فريق SEO", "SEO_TL", "PLAN"),
            ("مطوّر ويب", "WEB_DEV", "PLAN"),
            ("قائد فريق الويب", "WEB_TL", "PLAN"),
            ("مدير التخطيط والجودة", "PLAN_MGR", "PLAN"),
            ("مدير حسابات", "ACCOUNT_MGR", "GM"),
            ("محاسب", "ACCOUNTANT", "FIN"),
            ("مدير مالي", "FIN_MGR", "FIN"),
            ("المدير العام", "GM", "GM"),
            ("الرئيس التنفيذي", "CEO", "GM"),
        };

        foreach (var r in roles)
        {
            db.JobRoles.Add(new JobRole
            {
                NameAr = r.NameAr,
                Code = r.Code,
                DepartmentId = r.DeptCode is null ? null : Dept(r.DeptCode),
                IsActive = true,
            });
        }

        await db.SaveChangesAsync();
    }

    // ===== الأشخاص (idempotent على المدير التنفيذي) =====
    private static async Task SeedPeopleAsync(UserManager<ApplicationUser> users)
    {
        if (await users.FindByEmailAsync(CeoEmail) is not null) return;

        async Task Add(string email, string fullName, string role, string? managerEmail)
        {
            Guid? managerId = managerEmail is null ? null : (await users.FindByEmailAsync(managerEmail))?.Id;
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                IsActive = true,
                ManagerId = managerId
            };
            var created = await users.CreateAsync(user, DefaultPassword);
            if (created.Succeeded)
                await users.AddToRoleAsync(user, role);
        }

        // القمة: المدير التنفيذي + المدير العام + دعم المدير التنفيذي
        await Add(CeoEmail, "إبراهيم البحراوي", Roles.Ceo, null);
        await Add("ahmed.abdelraouf@marketingexperts.local", "أحمد عبدالرؤوف", Roles.GeneralManager, CeoEmail);
        await Add("fatima.support@marketingexperts.local", "فاطمة", Roles.CeoSupport, null);

        const string gmEmail = "ahmed.abdelraouf@marketingexperts.local";

        // قسم المبيعات
        await Add("mohamed.abdelqawi@marketingexperts.local", "محمد عبدالقوي", Roles.Manager, gmEmail);
        await Add("khaled.tl@marketingexperts.local", "خالد", Roles.TeamLeader, "mohamed.abdelqawi@marketingexperts.local");
        await Add("zainab.emp@marketingexperts.local", "زينب", Roles.Employee, "khaled.tl@marketingexperts.local");
        await Add("reem.emp@marketingexperts.local", "ريم", Roles.Employee, "khaled.tl@marketingexperts.local");
        await Add("aisha.emp@marketingexperts.local", "عائشة", Roles.Employee, "khaled.tl@marketingexperts.local");
        await Add("marwan.emp@marketingexperts.local", "مروان", Roles.Employee, "khaled.tl@marketingexperts.local");
        await Add("shrouk.emp@marketingexperts.local", "شروق", Roles.Employee, "mohamed.abdelqawi@marketingexperts.local");

        // الأداء والميديا
        await Add("mahmoud.alqousi@marketingexperts.local", "محمود القوصي", Roles.Manager, gmEmail);
        await Add("ahmed.abdelfattah@marketingexperts.local", "أحمد عبدالفتاح", Roles.Employee, "mahmoud.alqousi@marketingexperts.local");

        // التخطيط والجودة
        await Add("nermin.mgr@marketingexperts.local", "نرمين", Roles.Manager, gmEmail);
        await Add("basant.social@marketingexperts.local", "بسنت", Roles.TeamLeader, "nermin.mgr@marketingexperts.local");
        await Add("samar.social@marketingexperts.local", "سمر", Roles.Employee, "basant.social@marketingexperts.local");
        await Add("mohamed.ibrahim@marketingexperts.local", "محمد إبراهيم", Roles.Employee, "basant.social@marketingexperts.local");
        await Add("ahmed.sobhy@marketingexperts.local", "أحمد صبحي", Roles.Employee, "basant.social@marketingexperts.local");
        await Add("amira.social@marketingexperts.local", "أميرة", Roles.TeamLeader, "nermin.mgr@marketingexperts.local");
        await Add("esraa.social@marketingexperts.local", "إسراء", Roles.Employee, "amira.social@marketingexperts.local");
        await Add("nada.social@marketingexperts.local", "ندى", Roles.Employee, "amira.social@marketingexperts.local");
        await Add("ahmed.atef@marketingexperts.local", "أحمد عاطف", Roles.Employee, "amira.social@marketingexperts.local");
        await Add("tarek.mod@marketingexperts.local", "طارق", Roles.Employee, "amira.social@marketingexperts.local");
        await Add("kareem.video@marketingexperts.local", "كريم", Roles.Employee, "amira.social@marketingexperts.local");
        await Add("hossam.video@marketingexperts.local", "حسام", Roles.Employee, "amira.social@marketingexperts.local");
        await Add("shaimaa.seo@marketingexperts.local", "شيماء", Roles.TeamLeader, "nermin.mgr@marketingexperts.local");
        await Add("nour.emp@marketingexperts.local", "نور", Roles.Employee, "shaimaa.seo@marketingexperts.local");
        await Add("abdelrahman.emp@marketingexperts.local", "عبدالرحمن", Roles.Employee, "shaimaa.seo@marketingexperts.local");
        await Add("amir.web@marketingexperts.local", "أمير", Roles.TeamLeader, "nermin.mgr@marketingexperts.local");
        await Add("ahmed.nassar@marketingexperts.local", "أحمد نصار", Roles.Employee, "amir.web@marketingexperts.local");

        // وحدات تابعة للإدارة العامة مباشرة
        await Add("samah.emp@marketingexperts.local", "سماح", Roles.Employee, gmEmail);
        await Add("sherry.emp@marketingexperts.local", "شيري", Roles.Employee, gmEmail);
        await Add("mohsen.emp@marketingexperts.local", "محسن", Roles.Employee, gmEmail);
        await Add("luqman.cs@marketingexperts.local", "لقمان", Roles.Employee, gmEmail);

        // المالية
        await Add("mohamed.abdullah@marketingexperts.local", "محمد عبدالله", Roles.Manager, gmEmail);
        await Add("youssef.emp@marketingexperts.local", "يوسف", Roles.Employee, "mohamed.abdullah@marketingexperts.local");
    }

    // ===== الإدارات والفرق (idempotent على وجود إدارات) =====
    private static async Task SeedOrgStructureAsync(UserManager<ApplicationUser> users, AppDbContext db)
    {
        if (await db.Departments.AnyAsync()) return;

        async Task<ApplicationUser> U(string email) =>
            await users.FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"المستخدم غير موجود أثناء زرع الهيكل: {email}");

        Department Dept(string nameAr, string code, Guid managerId)
        {
            var d = new Department { NameAr = nameAr, Code = code, ManagerId = managerId, IsActive = true };
            db.Departments.Add(d);
            return d;
        }

        Team MakeTeam(string nameAr, Guid departmentId, Guid? leaderId)
        {
            var t = new Team { NameAr = nameAr, DepartmentId = departmentId, TeamLeaderId = leaderId, IsActive = true };
            db.Teams.Add(t);
            return t;
        }

        // أعضاء الفريق: تُضبط TeamId + DepartmentId
        async Task AssignMembers(Guid deptId, Guid teamId, params string[] emails)
        {
            foreach (var e in emails)
            {
                var u = await U(e);
                u.DepartmentId = deptId;
                u.TeamId = teamId;
            }
        }

        // مدير/قائد: تُضبط DepartmentId فقط
        async Task AssignDept(Guid deptId, params string[] emails)
        {
            foreach (var e in emails)
            {
                var u = await U(e);
                u.DepartmentId = deptId;
            }
        }

        // ===== المبيعات =====
        var salesMgr = await U("mohamed.abdelqawi@marketingexperts.local");
        var sales = Dept("المبيعات", "SALES", salesMgr.Id);
        await db.SaveChangesAsync(); // لتثبيت معرّفات الإدارات قبل إنشاء الفرق
        var b2c = MakeTeam("فريق B2C", sales.Id, (await U("khaled.tl@marketingexperts.local")).Id);
        var b2b = MakeTeam("فريق B2B", sales.Id, null);

        // ===== الأداء والميديا =====
        var perfMgr = await U("mahmoud.alqousi@marketingexperts.local");
        var perf = Dept("الأداء والميديا", "PERF", perfMgr.Id);

        // ===== التخطيط والجودة =====
        var planMgr = await U("nermin.mgr@marketingexperts.local");
        var planning = Dept("التخطيط والجودة", "PLAN", planMgr.Id);
        await db.SaveChangesAsync();
        var pod1 = MakeTeam("سوشيال — البود الأول", planning.Id, (await U("basant.social@marketingexperts.local")).Id);
        var pod2 = MakeTeam("سوشيال — البود الثاني", planning.Id, (await U("amira.social@marketingexperts.local")).Id);
        var seo = MakeTeam("تحسين محركات البحث SEO", planning.Id, (await U("shaimaa.seo@marketingexperts.local")).Id);
        var web = MakeTeam("تطوير الويب", planning.Id, (await U("amir.web@marketingexperts.local")).Id);
        var media = MakeTeam("شراء الإعلام", perf.Id, null);

        // ===== المالية =====
        var finMgr = await U("mohamed.abdullah@marketingexperts.local");
        var finance = Dept("المالية", "FIN", finMgr.Id);
        await db.SaveChangesAsync();
        var accounting = MakeTeam("المحاسبة", finance.Id, null);

        // ===== الإدارة العامة (وحدات مباشرة) =====
        var gm = await U("ahmed.abdelraouf@marketingexperts.local");
        var gmDept = Dept("الإدارة العامة", "GM", gm.Id);
        await db.SaveChangesAsync();
        var sharedTeam = MakeTeam("الحسابات والعمليات المشتركة", gmDept.Id, null);

        await db.SaveChangesAsync(); // تثبيت معرّفات الفرق

        // ===== ربط المستخدمين =====
        await AssignDept(sales.Id, "mohamed.abdelqawi@marketingexperts.local", "khaled.tl@marketingexperts.local");
        await AssignMembers(sales.Id, b2c.Id, "zainab.emp@marketingexperts.local", "reem.emp@marketingexperts.local",
            "aisha.emp@marketingexperts.local", "marwan.emp@marketingexperts.local");
        await AssignMembers(sales.Id, b2b.Id, "shrouk.emp@marketingexperts.local");

        await AssignDept(perf.Id, "mahmoud.alqousi@marketingexperts.local");
        await AssignMembers(perf.Id, media.Id, "ahmed.abdelfattah@marketingexperts.local");

        await AssignDept(planning.Id, "nermin.mgr@marketingexperts.local", "basant.social@marketingexperts.local",
            "amira.social@marketingexperts.local", "shaimaa.seo@marketingexperts.local", "amir.web@marketingexperts.local");
        await AssignMembers(planning.Id, pod1.Id, "samar.social@marketingexperts.local",
            "mohamed.ibrahim@marketingexperts.local", "ahmed.sobhy@marketingexperts.local");
        await AssignMembers(planning.Id, pod2.Id, "esraa.social@marketingexperts.local",
            "nada.social@marketingexperts.local", "ahmed.atef@marketingexperts.local",
            "tarek.mod@marketingexperts.local",
            "kareem.video@marketingexperts.local", "hossam.video@marketingexperts.local");
        await AssignMembers(planning.Id, seo.Id, "nour.emp@marketingexperts.local", "abdelrahman.emp@marketingexperts.local");
        await AssignMembers(planning.Id, web.Id, "ahmed.nassar@marketingexperts.local");

        await AssignDept(finance.Id, "mohamed.abdullah@marketingexperts.local");
        await AssignMembers(finance.Id, accounting.Id, "youssef.emp@marketingexperts.local");

        await AssignMembers(gmDept.Id, sharedTeam.Id, "samah.emp@marketingexperts.local",
            "sherry.emp@marketingexperts.local", "mohsen.emp@marketingexperts.local", "luqman.cs@marketingexperts.local");

        await db.SaveChangesAsync();
    }
}
