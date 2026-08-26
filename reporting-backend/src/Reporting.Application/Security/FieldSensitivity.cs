namespace Reporting.Application.Security;

/// <summary>
/// تصنيف حسّاسيّة الحقل/القسم (P2-SEC-001) — مركزيّ وقابل للتوسّع.
/// الترتيب تصاعديّ في الحسّاسيّة، لكن **لا** تُبنى القرارات على المقارنة الرقميّة:
/// كلّ تصنيف له قاعدة صريحة في <see cref="FieldVisibilityRules"/> لأنّ المسارات ليست خطّيّة
/// (مثلًا HR يرى <c>HrOnly</c> ولا يرى <c>MedicalSensitive</c> بلا صلاحيّة نوعيّة).
/// </summary>
public enum FieldSensitivity
{
    /// <summary>بيانات تشغيليّة عامّة داخل النطاق (الاسم، الفريق، حالة التقرير…).</summary>
    PublicOperational = 0,

    /// <summary>بيانات شورك مع الموظّف نفسه صراحةً (ملاحظة مشتركة، خطّة تطوير، ردّ على واقعة).</summary>
    SharedWithEmployee = 1,

    /// <summary>بيانات داخليّة إداريّة لا تُشارَك مع الموظّف (ملاحظة داخليّة على الأداء).</summary>
    Internal = 2,

    /// <summary>بيانات موارد بشريّة صرفة (سبب الإجازة الحسّاس، قرار HR الداخليّ).</summary>
    HrOnly = 3,

    /// <summary>سرّيّ إداريّ (تقييم خلافة، ملاحظة قياديّة سرّيّة).</summary>
    ManagementConfidential = 4,

    /// <summary>ماليّ حسّاس (راتب، حساب بنكيّ). لا يوجد في النظام حاليًّا — السياسة جاهزة للتوسّع.</summary>
    FinancialSensitive = 5,

    /// <summary>طبّيّ حسّاس (تقرير طبّيّ، تشخيص). لا يوجد في النظام حاليًّا — السياسة جاهزة للتوسّع.</summary>
    MedicalSensitive = 6
}

/// <summary>
/// علاقة المُشاهِد بصاحب الملفّ — تُحسَب خادميًّا من <c>IScopeResolver</c> وشجرة الإدارة،
/// ولا تُقبَل أبدًا من العميل.
/// </summary>
public enum SubjectRelation
{
    /// <summary>خارج النطاق تمامًا ⇒ الوصول = 404 (لا 403 كي لا يُسرَّب وجود المورد).</summary>
    None = 0,

    /// <summary>المُشاهِد هو صاحب الملفّ نفسه.</summary>
    Self = 1,

    /// <summary>مرؤوس مباشر (فريق قائد الفريق).</summary>
    DirectTeam = 2,

    /// <summary>داخل شجرة الإدارة (مرؤوس غير مباشر).</summary>
    Department = 3,

    /// <summary>رؤية على مستوى الشركة (تنفيذيّ/حوكمة/HR واسع).</summary>
    Company = 4
}

/// <summary>أقسام ملفّ الموظّف 360 (P2-EMP-002) — القسم غير المصرّح **يغيب** من الاستجابة.</summary>
public enum Employee360Section
{
    Identity = 1,
    OperationalSummary = 2,
    Reports = 3,
    Kpi = 4,
    LeaveAndPermissions = 5,
    RequestsAndBalances = 6,
    AttendanceAndCompliance = 7,
    Notes = 8,
    Governance = 9,
    DevelopmentAndTraining = 10,
    Timeline = 11
}
