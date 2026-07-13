namespace Reporting.Application.Common;

/// <summary>
/// حالة قدرة لدور معيّن في مصفوفة العرض (Phase A — عرض فقط، لا تفويض).
/// </summary>
public enum CapabilityStatus
{
    /// <summary>مُفعّلة فعليًّا للدور (تطابق سياسة/فحص خادمي قائم).</summary>
    Active,
    /// <summary>غير ممنوحة للدور حاليًّا.</summary>
    NotGranted,
    /// <summary>غير مُفعّلة الآن — مقترحة لاحقًا لهذا الدور (مثل توسعة HR إلى People Operations).</summary>
    ProposedLater,
    /// <summary>صلاحية حسّاسة تحتاج قرارًا/صلاحية مستقلة قبل منحها لهذا الدور.</summary>
    SensitiveDecision
}

/// <summary>
/// نموذج عرض الصلاحيات لكل دور — مصفوفة قدرات مجمّعة تعكس <b>الحقيقة الحالية</b> في الخادم.
/// مصدر «المُفعّل» مشتقّ حرفيًّا من مصفوفات <see cref="Roles"/> ونطاق <see cref="RoleAccess.ScopeTypeFor"/>
/// كي لا ينحرف عن السياسات في Program.cs. هذا النموذج <b>للعرض فقط ولا يفرض أي تفويض</b>.
/// لا يغيّر <see cref="RoleAccess.PermissionsFor"/> (المستخدَم في الداشبورد) — إضافة جديدة بحتة.
/// </summary>
public static class RoleCapabilities
{
    private static readonly string[] AdminOnly = { Roles.Admin };
    private static readonly string[] Executive = { Roles.Admin, Roles.Ceo, Roles.GeneralManager };
    private static readonly string[] PasswordReset = { Roles.Admin, Roles.CeoSupport };

    // الأدوار التي يظهر لها بند المنطقة المستقبلية كـ«مقترح لاحقًا» بدل «غير ممنوح».
    private static readonly string[] FutureAudience =
        { Roles.Hr, Roles.Admin, Roles.Ceo, Roles.GeneralManager, Roles.CeoSupport };

    // قدرة واحدة في الكتالوج: المفتاح + التسمية + المجموعة + مَن يملكها فعليًّا + أعلام الحالة.
    private sealed record Cap(
        string Group,
        string Key,
        string LabelAr,
        string[] ActiveRoles,
        bool Sensitive = false,
        bool HrProposed = false,
        bool Future = false);

    // نطاق الرؤية team+ / dept+ / company+ مشتقّ من ScopeTypeFor (نفس مصدر الداشبورد والتقارير).
    private static bool ScopeAtLeast(string role, params string[] scopes)
        => scopes.Contains(RoleAccess.ScopeTypeFor(role));

    private static string[] RolesWithScopeAtLeast(params string[] scopes)
        => Roles.All.Where(r => ScopeAtLeast(r, scopes)).ToArray();

    // ترتيب المجموعات وعناوينها العربية.
    private static readonly (string Key, string TitleAr)[] GroupOrder =
    {
        ("reports", "التقارير"),
        ("governance", "الحوكمة والمتابعة"),
        ("templates", "القوالب"),
        ("kpi", "مؤشرات الأداء KPI"),
        ("users", "المستخدمون والهيكل"),
        ("self_service", "خدمات الموظف"),
        ("system", "النظام"),
        ("future", "HR / People Operations (مقترح لاحقًا — غير مُفعّل)")
    };

    private static readonly IReadOnlyList<Cap> Catalog = BuildCatalog();

    private static List<Cap> BuildCatalog()
    {
        var teamPlus = RolesWithScopeAtLeast("team", "department", "company", "governance");
        var deptPlus = RolesWithScopeAtLeast("department", "company", "governance");
        var companyPlus = RolesWithScopeAtLeast("company", "governance");

        // المُعتمِدون/المُرجِعون للتقارير = مَن في PermissionsFor(ApproveReports).
        string[] approvers = { Roles.TeamLeader, Roles.Manager, Roles.GeneralManager, Roles.Ceo, Roles.Admin };
        string[] exporters = { Roles.TeamLeader, Roles.Manager, Roles.GeneralManager, Roles.Ceo, Roles.Admin, Roles.CeoSupport };
        string[] analytics = { Roles.Manager, Roles.GeneralManager, Roles.Ceo, Roles.Admin, Roles.CeoSupport };
        string[] governance = { Roles.Admin, Roles.CeoSupport, Roles.Ceo, Roles.GeneralManager };
        string[] kpiTeam = { Roles.Admin, Roles.Ceo, Roles.GeneralManager, Roles.Manager, Roles.TeamLeader };
        string[] kpiCompany = { Roles.Admin, Roles.Ceo, Roles.GeneralManager };

        return new List<Cap>
        {
            // ── التقارير ─────────────────────────────────────────────
            new("reports", "reports.view.own", "عرض تقاريري", Roles.All),
            new("reports", "reports.view.team", "عرض تقارير الفريق", teamPlus, HrProposed: true),
            new("reports", "reports.view.department", "عرض تقارير الإدارة", deptPlus, HrProposed: true),
            new("reports", "reports.view.all", "عرض كل التقارير", companyPlus, HrProposed: true),
            new("reports", "reports.approve", "اعتماد التقارير", approvers),
            new("reports", "reports.return", "إرجاع التقارير", approvers),
            new("reports", "reports.export", "تصدير التقارير", exporters, HrProposed: true),
            new("reports", "reports.analytics", "التحليلات", analytics, HrProposed: true),
            new("reports", "reports.comparisons", "المقارنات", analytics, HrProposed: true),

            // ── الحوكمة والمتابعة ────────────────────────────────────
            new("governance", "governance.view", "عرض الحوكمة", governance, HrProposed: true),
            new("governance", "audit.view", "عرض سجل التدقيق", Executive),
            new("governance", "reports.completion.followup", "متابعة اكتمال التقارير", Roles.Management, HrProposed: true),

            // ── القوالب ──────────────────────────────────────────────
            new("templates", "report_templates.view", "عرض قوالب التقارير", Executive, HrProposed: true),
            new("templates", "report_templates.manage", "إدارة قوالب التقارير", Roles.TemplateGovernance, Sensitive: true),
            new("templates", "report_templates.assign", "إسناد قوالب التقارير", Roles.TemplateGovernance, Sensitive: true),
            new("templates", "kpi_templates.view", "عرض قوالب KPI", Executive, HrProposed: true),
            new("templates", "kpi_templates.manage", "إدارة قوالب KPI", Roles.TemplateGovernance, Sensitive: true),

            // ── مؤشرات الأداء KPI ────────────────────────────────────
            new("kpi", "kpi.view.own", "عرض مؤشراتي", Roles.All),
            new("kpi", "kpi.evaluation.manage", "إنشاء/إدارة تقييمات KPI", Roles.Management),
            new("kpi", "kpi.evaluate", "تقييم/تعديل درجة KPI", Roles.Management, Sensitive: true),
            new("kpi", "kpi.view.team", "رؤية KPI للفريق", kpiTeam, HrProposed: true),
            new("kpi", "kpi.view.company", "رؤية KPI للشركة", kpiCompany, HrProposed: true),

            // ── المستخدمون والهيكل ───────────────────────────────────
            new("users", "users.view", "عرض دليل المستخدمين", Roles.All),
            new("users", "users.manage", "إدارة المستخدمين (إنشاء/تعديل/تعطيل)", AdminOnly),
            new("users", "users.manage_basic_hr", "إدارة بيانات الموظف الأساسية (HR)", AdminOnly, HrProposed: true),
            new("users", "users.manage_roles", "إدارة أدوار المستخدم", AdminOnly, Sensitive: true),
            new("users", "users.reset_password", "إعادة تعيين كلمة المرور", PasswordReset, Sensitive: true),
            new("users", "jobroles.manage", "إدارة المسمّيات الوظيفية", Roles.UserJobRoleManagers),
            new("users", "teams_departments.manage", "إدارة الفرق والأقسام", Roles.TeamManagement, Sensitive: true),

            // ── خدمات الموظف ─────────────────────────────────────────
            new("self_service", "balances.view.own", "عرض أرصدتي", Roles.All),
            new("self_service", "balances.manage", "إدارة أرصدة الموظفين", Roles.BalanceManagers),
            new("self_service", "balances.opening", "إضافة رصيد افتتاحي", Roles.BalanceManagers),
            new("self_service", "balances.adjust", "تعديل يدوي للرصيد", Roles.BalanceManagers),
            new("self_service", "leave.revoke", "إبطال إجازة/إذن معتمد", Roles.BalanceManagers),
            new("self_service", "hr_requests.view", "عرض طلبات HR العامة", Roles.HrRequestManagers),
            new("self_service", "hr_requests.process", "معالجة طلبات HR العامة", Roles.HrRequestManagers),
            new("self_service", "hr_requests.create", "إنشاء طلب HR", Roles.All),
            new("self_service", "leave.final_approval", "الاعتماد النهائي للإجازات/الأذونات", Roles.LeaveFinalApprovers),
            new("self_service", "leave.review", "رؤية طابور مراجعة الإجازات", Roles.LeaveReviewers),

            // ── النظام ───────────────────────────────────────────────
            new("system", "system.settings", "إعدادات النظام", AdminOnly),
            new("system", "email.settings", "إعدادات البريد/التنبيهات", AdminOnly),
            new("system", "permissions.manage", "إدارة الصلاحيات", AdminOnly),

            // ── HR / People Operations (مستقبلي — غير مُفعّل لأي دور حاليًّا) ──
            new("future", "kpi.followup", "متابعة تقييمات الأداء", Array.Empty<string>(), Future: true),
            new("future", "kpi.export", "تصدير تقارير KPI", Array.Empty<string>(), Future: true),
            new("future", "training.view", "عرض التدريب والتطوير", Array.Empty<string>(), Future: true),
            new("future", "training.manage", "إدارة التدريب", Array.Empty<string>(), Future: true),
            new("future", "development_plans.manage", "إدارة خطط التطوير", Array.Empty<string>(), Future: true),
            new("future", "performance_improvement.manage", "خطط تحسين الأداء (PIP)", Array.Empty<string>(), Future: true),
            new("future", "people_development", "Talent Review / نمو الموظف", Array.Empty<string>(), Future: true),
            new("future", "hr_notes", "ملاحظات HR على الأداء", Array.Empty<string>(), Future: true),
            new("future", "employee_lifecycle", "إدارة دورة حياة الموظف (أساسي)", Array.Empty<string>(), Future: true),
        };
    }

    private static CapabilityStatus StatusFor(string role, Cap cap)
    {
        if (cap.ActiveRoles.Contains(role)) return CapabilityStatus.Active;
        if (cap.Future) return FutureAudience.Contains(role) ? CapabilityStatus.ProposedLater : CapabilityStatus.NotGranted;
        if (cap.HrProposed && role == Roles.Hr) return CapabilityStatus.ProposedLater;
        if (cap.Sensitive) return CapabilityStatus.SensitiveDecision;
        return CapabilityStatus.NotGranted;
    }

    /// <summary>هل القدرة مُفعّلة فعليًّا للدور؟ (تُستخدم في الاختبارات للتحقّق من مطابقة العرض للسياسات.)</summary>
    public static bool IsActive(string role, string capabilityKey)
    {
        var cap = Catalog.FirstOrDefault(c => c.Key == capabilityKey);
        return cap is not null && cap.ActiveRoles.Contains(role);
    }

    /// <summary>قدرات دور معيّن مجمّعةً حسب المجال، مع حالة كل قدرة.</summary>
    public static IReadOnlyList<RoleCapabilityGroup> ForRole(string role)
        => GroupOrder.Select(g => new RoleCapabilityGroup(
                g.Key,
                g.TitleAr,
                Catalog.Where(c => c.Group == g.Key)
                    .Select(c => new RoleCapability(c.Key, c.LabelAr, StatusFor(role, c).ToString()))
                    .ToList()))
            .ToList();
}

/// <summary>قدرة واحدة في مصفوفة العرض: مفتاح + تسمية عربية + حالة (Active/NotGranted/ProposedLater/SensitiveDecision).</summary>
public record RoleCapability(string Key, string LabelAr, string Status);

/// <summary>مجموعة قدرات (مجال) في مصفوفة العرض.</summary>
public record RoleCapabilityGroup(string Key, string TitleAr, IReadOnlyList<RoleCapability> Items);
