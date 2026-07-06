namespace Reporting.Application.Common;

/// <summary>
/// قائمة ثابتة لمفاتيح صلاحيات المناصب المرنة (Phase 1A — رؤية فقط).
/// مصدر الحقيقة في الكود (لا جدول صلاحيات). أي مفتاح خارج هذه القائمة يُرفَض عند الإضافة.
/// كل المفاتيح هنا رؤية محضة — لا اعتماد/كتابة. صلاحيات الاعتماد/الإدارة ممنوعة في هذه المرحلة.
/// </summary>
public static class PositionPermissions
{
    /// <summary>رؤية التقارير ضمن نطاق المنصب (لا اعتماد/إرجاع/رفض).</summary>
    public const string ReportsView = "reports.view";

    /// <summary>رؤية لوحة المعلومات/التحليلات ضمن نطاق المنصب.</summary>
    public const string DashboardView = "dashboard.view";

    /// <summary>المفاتيح المسموح بها في هذه المرحلة. القائمة هي مصدر الحقيقة للتحقّق.</summary>
    public static readonly IReadOnlyList<string> Allowed = new[]
    {
        ReportsView,
        DashboardView
    };

    public static bool IsValid(string key) => Allowed.Contains(key);

    public static readonly IReadOnlyDictionary<string, string> LabelsAr = new Dictionary<string, string>
    {
        [ReportsView] = "رؤية التقارير",
        [DashboardView] = "رؤية لوحة المعلومات"
    };
}
