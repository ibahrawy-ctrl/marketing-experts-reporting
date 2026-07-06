using Reporting.Domain.Enums;

namespace Reporting.Application.Reports;

/// <summary>
/// مرشّح محرّك التجميع الرقمي (ERDS Phase 4) — قراءة فقط.
/// <b>PeriodKey</b> = مفتاح الأسبوع (مثل 2026-W33) — التصفية بالتطابق التام (لا نطاق تواريخ بعد).
/// <b>Item</b> = الدورة (B2C) أو الخدمة (B2B) لتصفية صفوف الجدول بالاسم (case-insensitive).
/// </summary>
public record AggregationFilter(
    PeriodType? PeriodType = null,
    string? PeriodKey = null,
    Guid? EmployeeId = null,
    Guid? TeamId = null,
    Guid? DepartmentId = null,
    string? Item = null);

/// <summary>
/// صفّ تجميع B2C لكل (أسبوع، موظّف، دورة). القيم الرقمية مجموع خلايا TableGrid المقروءة بأمان.
/// المؤشرات المحسوبة: التحويل/التأهيل/التواصل/الضياع كنِسب مئوية؛ الإيراد والمبيعات لكل ساعة كنِسب مباشرة.
/// </summary>
public record B2cCourseAggregateRow(
    string PeriodKey,
    Guid EmployeeId,
    string EmployeeName,
    string Course,
    Guid? TeamId,
    Guid? DepartmentId,
    decimal WorkHours,
    decimal Leads,
    decimal Contacted,
    decimal Qualified,
    decimal FollowUps,
    decimal Sales,
    decimal Revenue,
    decimal Lost,
    decimal ConversionRate,
    decimal QualificationRate,
    decimal ContactRate,
    decimal RevenuePerHour,
    decimal SalesPerHour,
    decimal LostRate);

/// <summary>نتيجة تجميع B2C حسب الدورة + بيانات تشخيصية للتوافق الخلفي (المتجاهَل بلا فشل).</summary>
public record B2cAggregationReport(
    string? PeriodKey,
    int RowCount,
    int SubmissionsConsidered,
    int SubmissionsIgnored,
    int RowsIgnored,
    string ViewLevel,
    IReadOnlyList<B2cCourseAggregateRow> Rows);

/// <summary>
/// تفصيل موظّف داخل مجموعة دورة (Drill-down): مساهمة الموظّف في إجماليات الدورة.
/// نفس الدورة عبر عدّة موظّفين تُدمَج في مجموعة واحدة، وهذا الصفّ يفصّل حصّة كل موظّف.
/// الحقول القياسية (WorkHours..ConversionRate) = الإجمالي Total (New + Old). دلوَا New/Old
/// يفصّلان مساهمة البيانات الجديدة/القديمة (Phase 7.1 — استعادة drill-down مع فصل الدلاء).
/// </summary>
public record B2cCourseEmployeeRow(
    Guid EmployeeId,
    string EmployeeName,
    Guid? TeamId,
    Guid? DepartmentId,
    decimal WorkHours,
    decimal Leads,
    decimal Contacted,
    decimal Qualified,
    decimal FollowUps,
    decimal Sales,
    decimal Revenue,
    decimal Lost,
    decimal ConversionRate,
    B2cNewOldBucket New,
    B2cNewOldBucket Old);

/// <summary>
/// صفّ تجميع «حسب الدورة» (العرض الافتراضي للمدير): إجماليات الدورة عبر كل الموظّفين
/// (Course يظهر مرّة واحدة) + قائمة تفصيل لكل موظّف (Drill-down). الإجماليات = مجموع مساهمات الموظّفين.
/// </summary>
public record B2cCourseGroupRow(
    string Course,
    decimal WorkHours,
    decimal Leads,
    decimal Contacted,
    decimal Qualified,
    decimal FollowUps,
    decimal Sales,
    decimal Revenue,
    decimal Lost,
    decimal ConversionRate,
    decimal QualificationRate,
    decimal ContactRate,
    decimal RevenuePerHour,
    decimal SalesPerHour,
    decimal LostRate,
    int EmployeeCount,
    IReadOnlyList<B2cCourseEmployeeRow> Employees);

/// <summary>نتيجة تجميع B2C مجموعةً حسب الدورة + بيانات تشخيصية للتوافق الخلفي (المتجاهَل بلا فشل).</summary>
public record B2cCourseGroupedReport(
    string? PeriodKey,
    int CourseCount,
    int SubmissionsConsidered,
    int SubmissionsIgnored,
    int RowsIgnored,
    string ViewLevel,
    IReadOnlyList<B2cCourseGroupRow> Courses);

/// <summary>
/// صفّ تجميع B2B لكل (أسبوع، موظّف، خدمة). القيم الرقمية مجموع خلايا TableGrid المقروءة بأمان.
/// المؤشرات المحسوبة: الاجتماعات/العروض/الفوز/الضياع كنِسب مئوية؛ الإيراد والفوز لكل ساعة كنِسب مباشرة.
/// </summary>
public record B2bServiceAggregateRow(
    string PeriodKey,
    Guid EmployeeId,
    string EmployeeName,
    string Service,
    Guid? TeamId,
    Guid? DepartmentId,
    decimal WorkHours,
    decimal Leads,
    decimal Meetings,
    decimal Proposals,
    decimal Negotiation,
    decimal Won,
    decimal Lost,
    decimal Revenue,
    decimal MeetingRate,
    decimal ProposalRate,
    decimal WinRate,
    decimal RevenuePerHour,
    decimal WonPerHour,
    decimal LostRate);

/// <summary>نتيجة تجميع B2B حسب الخدمة + بيانات تشخيصية للتوافق الخلفي (المتجاهَل بلا فشل).</summary>
public record B2bAggregationReport(
    string? PeriodKey,
    int RowCount,
    int SubmissionsConsidered,
    int SubmissionsIgnored,
    int RowsIgnored,
    string ViewLevel,
    IReadOnlyList<B2bServiceAggregateRow> Rows);

// ===== Phase 7 — تفصيل مبيعات B2C إلى بيانات جديدة New / بيانات CRM قديمة Old =====

/// <summary>
/// حزمة مؤشرات لدلو واحد (New أو Old) — نفس بنية مؤشرات B2C لكن بمعنى مختلف حسب الدلو:
/// <b>New</b>: Leads=New Leads، ConversionRate=المبيعات ÷ New Leads (معدّل التحويل الجديد).
/// <b>Old</b>: Leads=Old Leads Worked (بيانات CRM قديمة تمّت معالجتها)، ConversionRate=المبيعات ÷ Old Leads Worked (معدّل الاسترجاع Recovery).
/// </summary>
public record B2cNewOldBucket(
    decimal WorkHours,
    decimal Leads,
    decimal Contacted,
    decimal Qualified,
    decimal FollowUps,
    decimal Sales,
    decimal Revenue,
    decimal Lost,
    decimal ConversionRate,
    decimal QualificationRate,
    decimal ContactRate,
    decimal RevenuePerHour,
    decimal SalesPerHour,
    decimal LostRate);

/// <summary>صفّ تجميع لكل دورة مع دلوَي البيانات الجديدة والقديمة جنبًا إلى جنب.</summary>
public record B2cNewOldCourseRow(
    string Course,
    B2cNewOldBucket New,
    B2cNewOldBucket Old);

/// <summary>
/// نتيجة تجميع B2C بفصل البيانات الجديدة/القديمة. تقرأ ثلاثة جداول:
/// (1) قالب B2C أحادي الجدول القديم (Legacy) ⇒ دلو New (البيانات القديمة تُعامَل كـNew مؤقّتًا لعدم وجود مصدر).
/// (2) جدول «أداء البيانات الجديدة New Leads» من القالب الجديد ⇒ دلو New.
/// (3) جدول «أداء بيانات CRM القديمة Old CRM Data» من القالب الجديد ⇒ دلو Old.
/// </summary>
public record B2cNewOldReport(
    string? PeriodKey,
    int CourseCount,
    int SubmissionsConsidered,
    int SubmissionsIgnored,
    int RowsIgnored,
    string ViewLevel,
    B2cNewOldBucket NewTotals,
    B2cNewOldBucket OldTotals,
    IReadOnlyList<B2cNewOldCourseRow> Courses);
