using Reporting.Application.Security;
using Reporting.Domain.Enums;

namespace Reporting.Application.Checklist;

/// <summary>
/// P2-HR-010 — أصل البند: <b>محسوب من مصدره</b> أو <b>مسجَّل يدويًّا</b>.
/// الفصل ليس تسمية للعرض بل قاعدة تخزين: المحسوب لا يُكتَب في أيّ جدول إطلاقًا.
/// </summary>
public enum ChecklistItemSource
{
    /// <summary>مُشتَقّ لحظيًّا من مصدر الحقيقة المالك له. لا صفّ له في أيّ جدول.</summary>
    Computed = 1,

    /// <summary>مسجَّل يدويًّا لأنّه لا مصدر له في النظام أصلًا.</summary>
    Manual = 2
}

/// <summary>
/// P2-HR-010 — تعريف بند في الفهرس المغلق. التصنيف الحسّاس هنا يحكم <b>الإرسال</b> لا العرض:
/// البند غير المصرَّح به يغيب من الاستجابة كلّها لا يُرسَل مقنَّعًا.
/// </summary>
/// <param name="Key">مفتاح ثابت لا يُترجَم ولا يتغيّر (عقد الواجهة).</param>
/// <param name="TitleAr">العنوان العربيّ المعروض.</param>
/// <param name="GroupAr">المجموعة التي ينتمي إليها البند.</param>
/// <param name="Source">محسوب أم يدويّ.</param>
/// <param name="Sensitivity">تصنيف حسّاسيّة البند — يُفحَص قبل تركيبه في الاستجابة.</param>
/// <param name="SourceKind">اسم الكيان/الخدمة المصدر للمحسوب، أو <c>null</c> لليدويّ.</param>
public sealed record ChecklistItemDefinition(
    string Key,
    string TitleAr,
    string GroupAr,
    ChecklistItemSource Source,
    FieldSensitivity Sensitivity,
    string? SourceKind);

/// <summary>
/// P2-HR-010 — الفهرس المغلق لبنود قائمة خدمة الموظّف والالتزام.
///
/// <para><b>مغلق عمدًا:</b> كلّ بند جديد يستلزم إمّا مصدر اشتقاق موثَّقًا (محسوب) وإمّا إقرارًا
/// بأنّه لا مصدر له (يدويّ). البند بلا هذا التصنيف يفتح باب «قائمة تحقّق» تنسخ ما هو محفوظ
/// أصلًا فتتناقض معه بمرور الوقت.</para>
/// </summary>
public static class ChecklistCatalog
{
    // ===== المحسوبة: لكلّ واحد مصدر حقيقة مالك، ولا صفّ له في أيّ جدول =====

    public const string ReportsObligations = "reports-obligations";
    public const string KpiObligations = "kpi-obligations";
    public const string AttendanceAwaitingResponse = "attendance-awaiting-response";
    public const string AttendanceAwaitingHrReview = "attendance-awaiting-hr-review";
    public const string LeaveRequestsOpen = "leave-requests-open";
    public const string ServiceRequestsOpen = "service-requests-open";
    public const string NotesRequiringAction = "notes-requiring-action";
    public const string ImprovementPlansOpen = "improvement-plans-open";
    public const string ProfileCompleteness = "profile-completeness";

    // ===== اليدويّة: لا مصدر لها في النظام ⇒ الجدول الصغير الوحيد =====

    public const string EmploymentContractSigned = "employment-contract-signed";
    public const string PolicyAcknowledgement = "policy-acknowledgement";
    public const string OnboardingOrientation = "onboarding-orientation";
    public const string EquipmentHandover = "equipment-handover";
    public const string OffboardingClearance = "offboarding-clearance";

    private static readonly ChecklistItemDefinition[] Definitions =
    {
        new(ReportsObligations, "التقارير الدوريّة المطلوبة", "الالتزام التشغيليّ",
            ChecklistItemSource.Computed, FieldSensitivity.PublicOperational, "ObligationsService"),

        new(KpiObligations, "تقييمات الأداء المطلوبة", "الالتزام التشغيليّ",
            ChecklistItemSource.Computed, FieldSensitivity.PublicOperational, "ObligationsService"),

        new(AttendanceAwaitingResponse, "وقائع حضور تنتظر ردّ الموظّف", "الحضور والالتزام",
            ChecklistItemSource.Computed, FieldSensitivity.PublicOperational, "AttendanceIncident"),

        new(AttendanceAwaitingHrReview, "وقائع حضور تنتظر مراجعة الموارد البشريّة", "الحضور والالتزام",
            ChecklistItemSource.Computed, FieldSensitivity.PublicOperational, "AttendanceIncident"),

        new(LeaveRequestsOpen, "إجازات واستئذانات مفتوحة", "الطلبات",
            ChecklistItemSource.Computed, FieldSensitivity.PublicOperational, "LeaveRequest"),

        new(ServiceRequestsOpen, "طلبات خدمة موظّفين مفتوحة", "الطلبات",
            ChecklistItemSource.Computed, FieldSensitivity.PublicOperational, "EmployeeServiceRequest"),

        // الملاحظات الإداريّة تصنيفها Internal ⇒ الموظّف على نفسه لا يرى هذا البند أصلًا.
        new(NotesRequiringAction, "ملاحظات إداريّة تتطلّب إجراءً", "المتابعة الإداريّة",
            ChecklistItemSource.Computed, FieldSensitivity.Internal, "ManagementNote"),

        new(ImprovementPlansOpen, "خطط تحسين مفتوحة", "المتابعة الإداريّة",
            ChecklistItemSource.Computed, FieldSensitivity.Internal, "ImprovementPlan"),

        new(ProfileCompleteness, "اكتمال بيانات التعيين", "الملفّ الأساسيّ",
            ChecklistItemSource.Computed, FieldSensitivity.PublicOperational, "ApplicationUser"),

        // ===== اليدويّة =====

        new(EmploymentContractSigned, "توقيع عقد العمل", "الملفّ الأساسيّ",
            ChecklistItemSource.Manual, FieldSensitivity.HrOnly, null),

        new(PolicyAcknowledgement, "إقرار سياسات الشركة ولوائحها", "الملفّ الأساسيّ",
            ChecklistItemSource.Manual, FieldSensitivity.SharedWithEmployee, null),

        new(OnboardingOrientation, "إتمام التهيئة التعريفيّة", "التهيئة",
            ChecklistItemSource.Manual, FieldSensitivity.SharedWithEmployee, null),

        new(EquipmentHandover, "تسليم العهدة والأجهزة", "التهيئة",
            ChecklistItemSource.Manual, FieldSensitivity.SharedWithEmployee, null),

        new(OffboardingClearance, "إخلاء الطرف", "إنهاء الخدمة",
            ChecklistItemSource.Manual, FieldSensitivity.HrOnly, null)
    };

    public static IReadOnlyList<ChecklistItemDefinition> All => Definitions;

    public static IReadOnlyList<ChecklistItemDefinition> Manual =>
        Definitions.Where(d => d.Source == ChecklistItemSource.Manual).ToList();

    public static IReadOnlyList<ChecklistItemDefinition> Computed =>
        Definitions.Where(d => d.Source == ChecklistItemSource.Computed).ToList();

    public static ChecklistItemDefinition? Find(string? key) =>
        string.IsNullOrWhiteSpace(key)
            ? null
            : Definitions.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.Ordinal));

    /// <summary>هل المفتاح بند يدويّ قابل للكتابة؟ ما عداه لا يُكتَب في الجدول إطلاقًا.</summary>
    public static bool IsWritableManualKey(string? key) =>
        Find(key) is { Source: ChecklistItemSource.Manual };
}

/// <summary>
/// P2-HR-010 — بند واحد في القائمة كما يُسلَّم للواجهة.
/// </summary>
/// <param name="Key">مفتاح البند من الفهرس المغلق.</param>
/// <param name="TitleAr">العنوان العربيّ.</param>
/// <param name="GroupAr">المجموعة.</param>
/// <param name="Source">محسوب أم يدويّ — <b>معلَن للمستخدم</b> كي يعرف أين يُصحَّح البند.</param>
/// <param name="Status">الحالة الموحّدة عبر النوعين.</param>
/// <param name="StatusLabelAr">تسمية الحالة — تُحسَب خادميًّا لا في الواجهة.</param>
/// <param name="OpenCount">عدد البنود المفتوحة داخل البند المحسوب (0 لليدويّ).</param>
/// <param name="OwnerUserId">من يقع عليه الإنجاز، أو null إن لم يُحدَّد.</param>
/// <param name="OwnerFullName">اسم المسؤول.</param>
/// <param name="DueDate">الموعد المستهدف بتقويم الرياض.</param>
/// <param name="LastActionAtUtc">آخر إجراء.</param>
/// <param name="EvidenceReference">إشارة الدليل (لليدويّ)، أو ملخّص المصدر (للمحسوب).</param>
/// <param name="SourceKind">اسم الكيان/الخدمة المصدر — للتنقّل والتدقيق.</param>
/// <param name="SourceLink">مسار الواجهة الذي يفتح المصدر نفسه، أو null.</param>
/// <param name="RequiresMyAction">هل يقع الفعل على المستخدم الحاليّ الآن؟</param>
public sealed record ChecklistItemDto(
    string Key,
    string TitleAr,
    string GroupAr,
    string Source,
    EmployeeChecklistStatus Status,
    string StatusLabelAr,
    int OpenCount,
    Guid? OwnerUserId,
    string? OwnerFullName,
    DateOnly? DueDate,
    DateTime? LastActionAtUtc,
    string? EvidenceReference,
    string? SourceKind,
    string? SourceLink,
    bool RequiresMyAction);

/// <summary>
/// P2-HR-010 — ملخّص القائمة. <c>Applicable = Completed + Open</c>،
/// و<c>NotApplicable</c> خارج المقام كلّه.
/// </summary>
public sealed record ChecklistSummaryDto(
    int Applicable,
    int Completed,
    int Open,
    int NotApplicable,
    int RequiresMyAction,
    decimal CompletionRatio);

/// <summary>P2-HR-010 — ناتج القائمة لموظّف واحد.</summary>
public sealed record EmployeeChecklistDto(
    Guid SubjectUserId,
    bool IsSelf,
    string ViewerRelation,
    ChecklistSummaryDto Summary,
    IReadOnlyList<ChecklistItemDto> Items);

/// <summary>
/// P2-HR-010 — أمر تحديث بند يدويّ. المفتاح غير اليدويّ يُرفَض بـ400 لا يُكتَب صامتًا.
/// </summary>
public sealed record UpdateChecklistItemCommand(
    EmployeeChecklistStatus Status,
    DateOnly? DueDate = null,
    Guid? OwnerUserId = null,
    string? EvidenceReference = null,
    string? Note = null,
    string? ConcurrencyStamp = null);
