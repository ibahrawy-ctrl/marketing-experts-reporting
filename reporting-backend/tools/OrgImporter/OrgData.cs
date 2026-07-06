using Reporting.Application.Common;

namespace OrgImporter;

/// <summary>
/// مجموعة بيانات الديمو المضمّنة — مطابقة لـ OrgSeeder (المصدر المرجعي للهيكل التنظيمي).
/// 35 مستخدمًا، 5 إدارات، 21 مسمّى وظيفيًّا، 9 فِرق. لا أحد منهم بدور Admin.
/// عضوية الفريق وعلاقة الرفع أعمدة على ApplicationUser (TeamId / ManagerId).
/// </summary>
internal static class OrgData
{
    private const string Gm = "ahmed.abdelraouf@marketingexperts.local";
    private const string Ceo = "ibrahim.bahrawi@marketingexperts.local";

    // أسماء الفِرق (مفاتيح العضوية)
    private const string TeamB2C = "فريق B2C";
    private const string TeamB2B = "فريق B2B";
    private const string TeamPod1 = "سوشيال — البود الأول";
    private const string TeamPod2 = "سوشيال — البود الثاني";
    private const string TeamSeo = "تحسين محركات البحث SEO";
    private const string TeamWeb = "تطوير الويب";
    private const string TeamMedia = "شراء الإعلام";
    private const string TeamAccounting = "المحاسبة";
    private const string TeamShared = "الحسابات والعمليات المشتركة";

    internal record PersonDef(
        string Email, string FullName, string Role,
        string? ManagerEmail, string? JobCode, string? DeptCode, string? TeamName);

    internal record DeptDef(string NameAr, string Code, string ManagerEmail);

    internal record JobRoleDef(string NameAr, string Code, string? DeptCode);

    internal record TeamDef(string NameAr, string DeptCode, string? LeaderEmail);

    internal static readonly PersonDef[] People =
    {
        // القمة
        new(Ceo, "إبراهيم البحراوي", Roles.Ceo, null, "CEO", null, null),
        new(Gm, "أحمد عبدالرؤوف", Roles.GeneralManager, Ceo, "GM", null, null),
        new("fatima.support@marketingexperts.local", "فاطمة", Roles.CeoSupport, null, null, null, null),

        // المبيعات
        new("mohamed.abdelqawi@marketingexperts.local", "محمد عبدالقوي", Roles.Manager, Gm, "SALES_MGR", "SALES", null),
        new("khaled.tl@marketingexperts.local", "خالد", Roles.TeamLeader, "mohamed.abdelqawi@marketingexperts.local", "SALES_B2C_TL", "SALES", null),
        new("zainab.emp@marketingexperts.local", "زينب", Roles.Employee, "khaled.tl@marketingexperts.local", "SALES_B2C", "SALES", TeamB2C),
        new("reem.emp@marketingexperts.local", "ريم", Roles.Employee, "khaled.tl@marketingexperts.local", "SALES_B2C", "SALES", TeamB2C),
        new("aisha.emp@marketingexperts.local", "عائشة", Roles.Employee, "khaled.tl@marketingexperts.local", "SALES_B2C", "SALES", TeamB2C),
        new("marwan.emp@marketingexperts.local", "مروان", Roles.Employee, "khaled.tl@marketingexperts.local", "SALES_B2C", "SALES", TeamB2C),
        new("shrouk.emp@marketingexperts.local", "شروق", Roles.Employee, "mohamed.abdelqawi@marketingexperts.local", "SALES_B2B", "SALES", TeamB2B),

        // الأداء والميديا
        new("mahmoud.alqousi@marketingexperts.local", "محمود القوصي", Roles.Manager, Gm, "PERF_LEAD", "PERF", null),
        new("ahmed.abdelfattah@marketingexperts.local", "أحمد عبدالفتاح", Roles.Employee, "mahmoud.alqousi@marketingexperts.local", "MEDIA_BUYER", "PERF", TeamMedia),

        // التخطيط والجودة
        new("nermin.mgr@marketingexperts.local", "نرمين", Roles.Manager, Gm, "PLAN_MGR", "PLAN", null),
        new("basant.social@marketingexperts.local", "بسنت", Roles.TeamLeader, "nermin.mgr@marketingexperts.local", "SOCIAL_TL", "PLAN", null),
        new("samar.social@marketingexperts.local", "سمر", Roles.Employee, "basant.social@marketingexperts.local", "CONTENT_WRITER", "PLAN", TeamPod1),
        new("mohamed.ibrahim@marketingexperts.local", "محمد إبراهيم", Roles.Employee, "basant.social@marketingexperts.local", "CONTENT_WRITER", "PLAN", TeamPod1),
        new("ahmed.sobhy@marketingexperts.local", "أحمد صبحي", Roles.Employee, "basant.social@marketingexperts.local", "SOCIAL_MOD", "PLAN", TeamPod1),
        new("amira.social@marketingexperts.local", "أميرة", Roles.TeamLeader, "nermin.mgr@marketingexperts.local", "SOCIAL_TL", "PLAN", null),
        new("esraa.social@marketingexperts.local", "إسراء", Roles.Employee, "amira.social@marketingexperts.local", "DESIGNER", "PLAN", TeamPod2),
        new("nada.social@marketingexperts.local", "ندى", Roles.Employee, "amira.social@marketingexperts.local", "DESIGNER", "PLAN", TeamPod2),
        new("ahmed.atef@marketingexperts.local", "أحمد عاطف", Roles.Employee, "amira.social@marketingexperts.local", "SOCIAL_MOD", "PLAN", TeamPod2),
        new("tarek.mod@marketingexperts.local", "طارق", Roles.Employee, "amira.social@marketingexperts.local", "SOCIAL_MOD", "PLAN", TeamPod2),
        new("kareem.video@marketingexperts.local", "كريم", Roles.Employee, "amira.social@marketingexperts.local", "VIDEO_EDITOR", "PLAN", TeamPod2),
        new("hossam.video@marketingexperts.local", "حسام", Roles.Employee, "amira.social@marketingexperts.local", "VIDEO_EDITOR", "PLAN", TeamPod2),
        new("shaimaa.seo@marketingexperts.local", "شيماء", Roles.TeamLeader, "nermin.mgr@marketingexperts.local", "SEO_TL", "PLAN", null),
        new("nour.emp@marketingexperts.local", "نور", Roles.Employee, "shaimaa.seo@marketingexperts.local", "SEO_SPECIALIST", "PLAN", TeamSeo),
        new("abdelrahman.emp@marketingexperts.local", "عبدالرحمن", Roles.Employee, "shaimaa.seo@marketingexperts.local", "SEO_SPECIALIST", "PLAN", TeamSeo),
        new("amir.web@marketingexperts.local", "أمير", Roles.TeamLeader, "nermin.mgr@marketingexperts.local", "WEB_TL", "PLAN", null),
        new("ahmed.nassar@marketingexperts.local", "أحمد نصار", Roles.Employee, "amir.web@marketingexperts.local", "WEB_DEV", "PLAN", TeamWeb),

        // وحدات تابعة للإدارة العامة مباشرة
        new("samah.emp@marketingexperts.local", "سماح", Roles.Employee, Gm, null, "GM", TeamShared),
        new("sherry.emp@marketingexperts.local", "شيري", Roles.Employee, Gm, null, "GM", TeamShared),
        new("mohsen.emp@marketingexperts.local", "محسن", Roles.Employee, Gm, null, "GM", TeamShared),
        new("luqman.cs@marketingexperts.local", "لقمان", Roles.Employee, Gm, null, "GM", TeamShared),

        // المالية
        new("mohamed.abdullah@marketingexperts.local", "محمد عبدالله", Roles.Manager, Gm, "FIN_MGR", "FIN", null),
        new("youssef.emp@marketingexperts.local", "يوسف", Roles.Employee, "mohamed.abdullah@marketingexperts.local", "ACCOUNTANT", "FIN", TeamAccounting),
    };

    internal static readonly DeptDef[] Departments =
    {
        new("المبيعات", "SALES", "mohamed.abdelqawi@marketingexperts.local"),
        new("الأداء والميديا", "PERF", "mahmoud.alqousi@marketingexperts.local"),
        new("التخطيط والجودة", "PLAN", "nermin.mgr@marketingexperts.local"),
        new("المالية", "FIN", "mohamed.abdullah@marketingexperts.local"),
        new("الإدارة العامة", "GM", Gm),
    };

    internal static readonly JobRoleDef[] JobRoles =
    {
        new("مندوب مبيعات B2C", "SALES_B2C", "SALES"),
        new("قائد فريق مبيعات B2C", "SALES_B2C_TL", "SALES"),
        new("مندوب مبيعات B2B", "SALES_B2B", "SALES"),
        new("مدير المبيعات", "SALES_MGR", "SALES"),
        new("مشتري إعلانات", "MEDIA_BUYER", "PERF"),
        new("قائد الأداء", "PERF_LEAD", "PERF"),
        new("كاتب محتوى", "CONTENT_WRITER", "PLAN"),
        new("مصمم جرافيك", "DESIGNER", "PLAN"),
        new("محرر فيديو", "VIDEO_EDITOR", "PLAN"),
        new("مشرف سوشيال", "SOCIAL_MOD", "PLAN"),
        new("قائد فريق السوشيال", "SOCIAL_TL", "PLAN"),
        new("أخصائي SEO", "SEO_SPECIALIST", "PLAN"),
        new("قائد فريق SEO", "SEO_TL", "PLAN"),
        new("مطوّر ويب", "WEB_DEV", "PLAN"),
        new("قائد فريق الويب", "WEB_TL", "PLAN"),
        new("مدير التخطيط والجودة", "PLAN_MGR", "PLAN"),
        new("مدير حسابات", "ACCOUNT_MGR", "GM"),
        new("محاسب", "ACCOUNTANT", "FIN"),
        new("مدير مالي", "FIN_MGR", "FIN"),
        new("المدير العام", "GM", "GM"),
        new("الرئيس التنفيذي", "CEO", "GM"),
    };

    internal static readonly TeamDef[] Teams =
    {
        new(TeamB2C, "SALES", "khaled.tl@marketingexperts.local"),
        new(TeamB2B, "SALES", null),
        new(TeamPod1, "PLAN", "basant.social@marketingexperts.local"),
        new(TeamPod2, "PLAN", "amira.social@marketingexperts.local"),
        new(TeamSeo, "PLAN", "shaimaa.seo@marketingexperts.local"),
        new(TeamWeb, "PLAN", "amir.web@marketingexperts.local"),
        new(TeamMedia, "PERF", null),
        new(TeamAccounting, "FIN", null),
        new(TeamShared, "GM", null),
    };
}
