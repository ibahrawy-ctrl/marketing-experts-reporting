namespace Reporting.Application.Security;

/// <summary>
/// مفاتيح الصلاحيّات الدقيقة (P2) — تُحمَل كمطالبات <c>perm</c> على المستخدم في ASP.NET Identity.
/// <para>
/// **لا تُمنَح أيّ منها لأيّ دور مخزَّن تلقائيًّا**: التعريف والسياسات فقط تُضاف هنا، والتعيين الفعليّ
/// للأدوار/المستخدمين قرار نشر لاحق خارج نطاق Phase 2. غياب المطالبة ⇒ الحجب.
/// </para>
/// <para>الدور وحده لا يكفي أبدًا لهذه المفاتيح — حتّى <c>Admin</c> لا يكتسبها ضمنًا.</para>
/// </summary>
public static class AppPermissions
{
    /// <summary>نوع المطالبة الحامل لمفتاح الصلاحيّة داخل JWT وIdentity.</summary>
    public const string ClaimType = "perm";

    // ===== لوحة عمليّات الموارد البشريّة =====
    /// <summary>رؤية لوحة HR Operations وطوابير الإجراءات. النطاق الفعليّ يحدّده ScopeResolver.</summary>
    public const string HrOperationsView = "HrOperations.View";

    /// <summary>تصدير لوحة/طوابير HR Operations — **مستقلّة تمامًا** عن الرؤية، وكلّ تصدير يُدقَّق.</summary>
    public const string HrOperationsExport = "HrOperations.Export";

    // ===== وقائع الحضور =====
    /// <summary>تسجيل بلاغ حضور. تُمنَح ضمنًا لقائد الفريق/المدير داخل نطاقه (انظر AttendanceAccess).</summary>
    public const string AttendanceReport = "Attendance.Report";

    /// <summary>مراجعة HR لواقعة الحضور (تأكيد/رفض/تصحيح/مصالحة/إلغاء).</summary>
    public const string AttendanceReview = "Attendance.Review";

    /// <summary>تصدير وقائع الحضور — مستقلّة عن الرؤية والمراجعة.</summary>
    public const string AttendanceExport = "Attendance.Export";

    /// <summary>تصعيد واقعة حضور إلى الحوكمة.</summary>
    public const string AttendanceEscalate = "Attendance.Escalate";

    // ===== حسّاسيّة الحقول =====
    /// <summary>قراءة الحقول المصنّفة <c>HrOnly</c> (سبب الإجازة الحسّاس، ملاحظات HR الداخليّة).</summary>
    public const string HrSensitiveRead = "Sensitivity.HrOnly.Read";

    /// <summary>قراءة الحقول المصنّفة <c>ManagementConfidential</c>.</summary>
    public const string ManagementConfidentialRead = "Sensitivity.ManagementConfidential.Read";

    /// <summary>قراءة الحقول المصنّفة <c>FinancialSensitive</c> — صلاحيّة نوعيّة لا يمنحها أيّ دور.</summary>
    public const string FinancialSensitiveRead = "Sensitivity.Financial.Read";

    /// <summary>قراءة الحقول المصنّفة <c>MedicalSensitive</c> — صلاحيّة نوعيّة لا يمنحها أيّ دور.</summary>
    public const string MedicalSensitiveRead = "Sensitivity.Medical.Read";

    /// <summary>كلّ المفاتيح المعرّفة — للتحقّق والاختبارات فقط.</summary>
    public static readonly string[] All =
    {
        HrOperationsView, HrOperationsExport,
        AttendanceReport, AttendanceReview, AttendanceExport, AttendanceEscalate,
        HrSensitiveRead, ManagementConfidentialRead, FinancialSensitiveRead, MedicalSensitiveRead
    };
}
