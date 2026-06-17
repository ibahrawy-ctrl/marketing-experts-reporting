namespace Reporting.Application.Common;

/// <summary>أدوار النظام — تُدار عبر جداول ASP.NET Identity (لا عمود Role في المستخدمين).</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Ceo = "CEO";
    public const string GeneralManager = "GeneralManager";
    public const string Manager = "Manager";
    public const string TeamLeader = "TeamLeader";
    public const string Employee = "Employee";
    public const string CeoSupport = "CeoSupport";
    public const string Viewer = "Viewer";
    // دور الموارد البشرية الرسمي (V1.0.1-A) — المعتمِد النهائي الطبيعي لطلبات الإجازة/الاستئذان.
    // ليس دورًا إداريًّا (ليس ضمن Management) ولا يملك صلاحيات الأدمن؛ سلطته مقصورة على الاعتماد النهائي للإجازات.
    public const string Hr = "HR";

    public static readonly string[] All =
    {
        Admin, Ceo, GeneralManager, Manager, TeamLeader, Employee, CeoSupport, Viewer, Hr
    };

    /// <summary>الأدوار الإدارية التي ترى تجميعات أعلى من المستخدم نفسه.</summary>
    public static readonly string[] Management =
    {
        Admin, Ceo, GeneralManager, Manager, TeamLeader
    };

    /// <summary>
    /// الأدوار المخوّلة بإدارة عضوية الفرق (تعديل الاسم/القائد، إضافة/إزالة عضو):
    /// المستوى الإداري الأعلى فقط — Admin / CEO / GM. عمدًا لا تشمل Manager أو TeamLeader.
    /// TODO: عند تعريف أدوار HR / HR Manager / HR Officer / المساعد الإداري (Administrative Assistant)
    /// تُضاف هنا — ليست معرّفة في النظام حاليًا (لا تفتح Phase 4 لإضافتها).
    /// </summary>
    public static readonly string[] TeamManagement =
    {
        Admin, Ceo, GeneralManager
    };

    /// <summary>
    /// الأدوار المخوّلة بحوكمة القوالب ومؤشرات الأداء (إنشاء/تعديل قوالب التقارير وKPI،
    /// إدارة الإصدارات والأوزان والربط بالأدوار، وإعدادات الأداء):
    /// المستوى الإداري الأعلى فقط — Admin / CEO / GM. عمدًا لا تشمل Manager أو TeamLeader أو Employee.
    /// إدارة القوالب التفصيلية تعيش في منطقة Admin/Governance؛ CEO/GM يَطّلعون على النتائج أساسًا.
    /// TODO: عند تعريف أدوار HR / المساعد الإداري (Administrative Assistant) تُضاف هنا — غير معرّفة بالنظام حاليًا.
    /// </summary>
    public static readonly string[] TemplateGovernance =
    {
        Admin, Ceo, GeneralManager
    };

    /// <summary>
    /// الأدوار المخوّلة بالاعتماد النهائي لطلبات الإجازة/الاستئذان (V1.0.1-A — قدرة LeaveFinalApproval).
    /// HR هو المعتمِد النهائي الطبيعي؛ يبقى Admin / CEO / GM كصلاحية تدخّل/override حسب السياسة.
    /// ملاحظة: الاعتماد النهائي لطلب HR الشخصي نفسه لا يتم عبر هذه المجموعة بل عبر مسار خاص
    /// (المدير العام يراجع ثم CEO/Admin يعتمد) — يُفرَض في طبقة الخدمة.
    /// </summary>
    public static readonly string[] LeaveFinalApprovers =
    {
        Hr, Admin, Ceo, GeneralManager
    };

    /// <summary>
    /// الأدوار التي ترى طابور «بانتظار قراري» وتراجع طلبات الإجازة (اتحاد الإدارة + الموارد البشرية).
    /// الفرض النهائي للنطاق والخطوة في طبقة الخدمة؛ هذه طبقة دفاع أولى عند نقطة النهاية.
    /// </summary>
    public static readonly string[] LeaveReviewers =
    {
        Admin, Ceo, GeneralManager, Manager, TeamLeader, Hr
    };

    public const string DisplayAr_Admin = "مدير النظام";
    public const string DisplayAr_Ceo = "الرئيس التنفيذي";
    public const string DisplayAr_GeneralManager = "المدير العام";
    public const string DisplayAr_Manager = "مدير";
    public const string DisplayAr_TeamLeader = "قائد فريق";
    public const string DisplayAr_Employee = "موظف";
    public const string DisplayAr_CeoSupport = "دعم الرئيس التنفيذي";
    public const string DisplayAr_Viewer = "مُطّلِع";
    public const string DisplayAr_Hr = "الموارد البشرية";

    public static string DisplayAr(string role) => role switch
    {
        Admin => DisplayAr_Admin,
        Ceo => DisplayAr_Ceo,
        GeneralManager => DisplayAr_GeneralManager,
        Manager => DisplayAr_Manager,
        TeamLeader => DisplayAr_TeamLeader,
        Employee => DisplayAr_Employee,
        CeoSupport => DisplayAr_CeoSupport,
        Viewer => DisplayAr_Viewer,
        Hr => DisplayAr_Hr,
        _ => role
    };
}

/// <summary>أسماء سياسات التفويض.</summary>
public static class Policies
{
    public const string ManagementOnly = "ManagementOnly";
    public const string AdminOnly = "AdminOnly";
    public const string ExecutiveOnly = "ExecutiveOnly"; // Admin + CEO + GM
    public const string TeamManagement = "TeamManagement"; // إدارة عضوية الفرق — Admin/CEO/GM (+ HR لاحقًا)
    // حوكمة القوالب وKPI والأوزان والربط والإصدارات وإعدادات الأداء — Admin/CEO/GM (+ HR لاحقًا).
    // تغطّي القدرات المنطقية: ManageReportTemplates / ManageKpiTemplates / AssignTemplatesToRoles /
    // ManageTemplateVersions / ManagePerformanceSettings (سياسة واحدة مجمّعة بدل خمس سياسات منفصلة، اتباعًا للنمط القائم).
    public const string TemplateGovernance = "TemplateGovernance";

    // الاعتماد النهائي لطلبات الإجازة/الاستئذان (V1.0.1-A) — قدرة الموارد البشرية HR (+ تدخّل Admin/CEO/GM).
    public const string LeaveFinalApproval = "LeaveFinalApproval";

    // رؤية طابور المراجعة «بانتظار قراري» للإجازات — اتحاد الإدارة + الموارد البشرية (Roles.LeaveReviewers).
    public const string LeaveReview = "LeaveReview";
}
