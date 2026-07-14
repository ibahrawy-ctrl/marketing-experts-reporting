namespace Reporting.Application.Calendar;

// ===== ROLE-AWARE-REPORTING-CALENDAR — نماذج واجهة تقويم الدورات المُدرِكة للأدوار (Phase 2.3) =====
// كل الحسابات خادميّة عبر ReportingCalendarPolicy (مصدر الحقيقة الوحيد). الدور لا يُرسَل من الواجهة إطلاقًا —
// يُستخرَج من دور المستخدم الأساسيّ (RoleAccess.PrimaryRole). لا تعديل بيانات، لا هجرة، قراءة/حساب فقط.

/// <summary>سياق التقويم المطلوب: تقرير أسبوعي أو تقييم KPI أسبوعي (يؤثّران على التسمية فقط، لا على الترقيم).</summary>
public enum ReportingCalendarContext
{
    Report = 0,
    Kpi = 1
}

/// <summary>
/// صفّ دورة تقريرية واحدة مُدرِكة للدور — نافذة السبت→الجمعة موحّدة لكل المستويات،
/// وتاريخ الاستحقاق يختلف بحسب دور المستخدم الأساسيّ فقط. كل الحقول محسوبة خادميًّا.
/// </summary>
public record ReportingCycleDto(
    // ----- هوية الدورة (موحّدة لكل الأدوار لنفس الفترة) -----
    string CycleKey,                 // مثل 2026-W27
    int CycleNumber,                 // 27
    int CycleYear,                   // 2026
    DateOnly CycleStart,             // السبت (بداية الدورة)
    DateOnly CycleEnd,               // الجمعة (نهاية الدورة)
    DateOnly TuesdayReference,       // مرجع الثلاثاء (أساس الترقيم)
    string CycleLabel,               // «الأسبوع 27 — 2026 (السبت 27 يونيو — الجمعة 3 يوليو)»
    string ShortLabel,               // «الأسبوع 27 — 2026»

    // ----- نطاق تغطية البيانات (نقطة توسّع؛ الآن = نافذة الدورة) -----
    DateOnly DataCoverageStart,
    DateOnly DataCoverageEnd,

    // ----- البُعد المُدرِك للدور (يختلف تاريخ الاستحقاق فقط) -----
    string Role,                     // الدور الأساسيّ الخادميّ (لا يُرسَل من الواجهة)
    string RoleLabel,                // التسمية العربية للدور
    int RoleDueOffset,               // إزاحة الاستحقاق بالأيام من السبت
    DateOnly RoleDueDate,            // تاريخ الاستحقاق بحسب الدور
    string RoleDueDateLabel,         // «الأربعاء 1 يوليو»

    // ----- الموضع الزمنيّ والحالة -----
    int Offset,                      // 0 = الحالية، سالب = ماضية، موجب = مستقبلية
    bool IsCurrent,
    bool IsPast,
    bool IsFuture,
    string Status,                   // "current" | "past" | "locked"
    bool IsOpen,                     // هل يُسمح بالإنشاء/التسليم على هذه الدورة؟
    bool IsLocked,                   // عكس IsOpen (دورة مستقبلية لم تبدأ بعد)
    string? LockReason,              // سبب القفل (عربي) عند الإغلاق
    bool IsOverdue,                  // هل تجاوز اليوم تاريخ الاستحقاق للدور؟
    bool RequiresReason,             // دورة ماضية بعيدة تتطلّب سببًا للتسليم المتأخّر

    // ----- مرجع اليوم -----
    DateOnly Today,
    ReportingCalendarContext Context);

/// <summary>غلاف نتيجة my-cycles: بيانات الدور + الدورة الحالية + قائمة الدورات المتاحة للمستخدم.</summary>
public record MyCyclesDto(
    ReportingCalendarContext Context,
    Guid? TemplateId,
    string Role,
    string RoleLabel,
    string CurrentCycleKey,
    DateOnly Today,
    IReadOnlyList<ReportingCycleDto> Cycles);
