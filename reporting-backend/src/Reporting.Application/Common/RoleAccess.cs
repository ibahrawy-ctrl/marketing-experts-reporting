namespace Reporting.Application.Common;

/// <summary>
/// مصدر واحد لحقيقة نطاق الرؤية (Scope) والصلاحيات (Permissions) لكل دور.
/// تستخدمه لوحة التحكم لتحديد ما يراه المستخدم، وصفحة إدارة المستخدمين لعرض/توزيع الصلاحيات.
/// </summary>
public static class RoleAccess
{
    /// <summary>الدور الأعلى صلاحية للمستخدم (يحدد نوع الداشبورد ونطاق الرؤية).</summary>
    public static string PrimaryRole(IEnumerable<string> roles)
    {
        var set = roles as ICollection<string> ?? roles.ToList();
        string[] order =
        {
            Roles.Admin, Roles.Ceo, Roles.GeneralManager, Roles.Manager,
            Roles.TeamLeader, Roles.CeoSupport, Roles.Employee, Roles.Viewer
        };
        return order.FirstOrDefault(set.Contains) ?? Roles.Employee;
    }

    /// <summary>نوع نطاق الرؤية المحسوب من شجرة التسلسل الإداري (ManagerId).</summary>
    public static string ScopeTypeFor(string role) => role switch
    {
        Roles.Admin => "governance",
        Roles.CeoSupport => "governance",
        Roles.Ceo => "company",
        Roles.GeneralManager => "company",
        Roles.Manager => "department",
        Roles.TeamLeader => "team",
        _ => "own"
    };

    /// <summary>وصف عربي مبسّط لنطاق الرؤية يُعرض للأدمن.</summary>
    public static string ScopeDescriptionAr(string scopeType) => scopeType switch
    {
        "governance" => "كل المؤسسة (حوكمة وإشراف كامل)",
        "company" => "كل المؤسسة",
        "department" => "إدارته وكل الفرق التابعة لها",
        "team" => "فريقه المباشر فقط",
        _ => "بياناته الشخصية فقط"
    };

    /// <summary>الصلاحيات الفعّالة لمجموعة أدوار (تتراكم).</summary>
    public static IReadOnlyList<string> PermissionsFor(IEnumerable<string> roles)
    {
        var set = roles as ICollection<string> ?? roles.ToList();
        var perms = new HashSet<string> { "ViewOwn" };

        bool Any(params string[] r) => r.Any(set.Contains);

        if (Any(Roles.TeamLeader, Roles.Manager, Roles.GeneralManager, Roles.Ceo, Roles.Admin))
            perms.Add("ApproveReports");
        if (Any(Roles.TeamLeader, Roles.Manager, Roles.GeneralManager, Roles.Ceo, Roles.Admin, Roles.CeoSupport))
            perms.Add("ExportReports");
        if (Any(Roles.Manager, Roles.GeneralManager, Roles.Ceo, Roles.Admin, Roles.CeoSupport))
            perms.Add("ViewAnalytics");
        if (Any(Roles.Admin, Roles.CeoSupport, Roles.Ceo, Roles.GeneralManager))
            perms.Add("ViewGovernance");
        // حوكمة القوالب وKPI متاحة للمستوى الإداري الأعلى (Admin/CEO/GM) — تطابق سياسة TemplateGovernance بالخادم.
        // (+ HR / المساعد الإداري لاحقًا عند تعريفهم). إدارة المستخدمين تبقى للأدمن فقط.
        if (Any(Roles.Admin, Roles.Ceo, Roles.GeneralManager))
            perms.Add("ManageTemplates");
        if (Any(Roles.Admin))
            perms.Add("ManageUsers");
        if (Any(Roles.Admin, Roles.Ceo))
            perms.Add("ViewAudit");

        return perms.ToList();
    }

    /// <summary>هل تملك مجموعة الأدوار صلاحية معيّنة؟</summary>
    public static bool Has(IEnumerable<string> roles, string permission) =>
        PermissionsFor(roles).Contains(permission);

    /// <summary>صلاحية رؤية بيانات الحوكمة المؤسسية (مخاطر/قرارات/ملخص حوكمة).</summary>
    public static bool CanViewGovernance(IEnumerable<string> roles) => Has(roles, "ViewGovernance");

    /// <summary>صلاحية رؤية التحليلات والمقارنات على مستوى أوسع من النطاق الشخصي.</summary>
    public static bool CanViewAnalytics(IEnumerable<string> roles) => Has(roles, "ViewAnalytics");

    /// <summary>تسميات عربية للصلاحيات لعرضها في الواجهة.</summary>
    public static readonly IReadOnlyDictionary<string, string> PermissionLabelsAr = new Dictionary<string, string>
    {
        ["ViewOwn"] = "عرض بياناته الشخصية",
        ["ApproveReports"] = "اعتماد التقارير",
        ["ExportReports"] = "تصدير التقارير",
        ["ViewAnalytics"] = "التحليلات والمقارنات",
        ["ViewGovernance"] = "الحوكمة والمتابعة",
        ["ManageTemplates"] = "إدارة القوالب",
        ["ManageUsers"] = "إدارة المستخدمين والصلاحيات",
        ["ViewAudit"] = "سجل التدقيق",
    };

    public static string PermissionLabelAr(string permission) =>
        PermissionLabelsAr.GetValueOrDefault(permission, permission);
}
