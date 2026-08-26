namespace Reporting.Application.Security;

/// <summary>
/// أعلام ميزات المرحلة الثانية (§9). **كلّها <c>false</c> افتراضيًّا** ولا تُغيَّر في أيّ بيئة حيّة.
/// <para>
/// العلم **ليس تفويضًا**: إطفاؤه يُخفي السطح، وإشعاله لا يمنح أحدًا شيئًا —
/// التفويض يبقى بالسياسات وطبقة الرؤية والنطاق خادميًّا في كلّ الأحوال.
/// </para>
/// </summary>
public sealed class Phase2FeatureOptions
{
    public const string SectionName = "Phase2";

    /// <summary>ملفّ الموظّف 360 الموحّد ووضع «ملفي».</summary>
    public bool Employee360Enabled { get; set; }

    /// <summary>وقائع الحضور والالتزام (بلاغ/ردّ/مراجعة HR).</summary>
    public bool AttendanceEnabled { get; set; }

    /// <summary>لوحة عمليّات الموارد البشريّة وطوابير الإجراءات.</summary>
    public bool HrOperationsEnabled { get; set; }

    /// <summary>قائمة تحقّق خدمة الموظّف والالتزام.</summary>
    public bool EmployeeChecklistEnabled { get; set; }

    // ===== إعدادات SLA لوقائع الحضور (قابلة للضبط — §P2-ATT-006) =====

    /// <summary>نافذة ردّ الموظّف على البلاغ بالساعات (الافتراضي 48).</summary>
    public int AttendanceEmployeeResponseHours { get; set; } = 48;

    /// <summary>مهلة مراجعة HR بأيّام العمل (الافتراضي 5 — الأحد→الخميس).</summary>
    public int AttendanceHrReviewWorkingDays { get; set; } = 5;

    /// <summary>
    /// هل تُغلَق الواقعة تلقائيًّا عند تطابقها مع إجازة/استئذان معتمد؟
    /// <b>الافتراضي false = اقتراح لا قرار</b> — القرار النهائيّ لـHR دائمًا.
    /// </summary>
    public bool AttendanceAutoReconcile { get; set; }
}
