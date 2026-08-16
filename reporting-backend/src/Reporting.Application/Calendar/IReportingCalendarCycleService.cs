using Reporting.Application.Common;

namespace Reporting.Application.Calendar;

/// <summary>
/// خدمة تقويم الدورات المُدرِكة للأدوار (ROLE-AWARE-REPORTING-CALENDAR — Phase 2.3).
/// تُرجِع الدورات المتاحة للمستخدم الحاليّ (نافذة السبت→الجمعة موحّدة، تاريخ الاستحقاق بحسب الدور).
/// الدور يُستخرَج خادميًّا من دور المستخدم الأساسيّ — لا يُرسَل من الواجهة. كل الحسابات عبر
/// <see cref="ReportingCalendarPolicy"/>. قراءة/حساب فقط: لا تعديل بيانات، لا هجرة.
/// </summary>
public interface IReportingCalendarCycleService
{
    /// <summary>
    /// دورات المستخدم الحاليّ حول الدورة الحالية (ماضٍ محدود + الحالية + مستقبل محدود).
    /// <paramref name="context"/> تقرير/KPI (تسمية فقط)، <paramref name="templateId"/> اختياريّ (سياق فقط)،
    /// <paramref name="past"/>/<paramref name="future"/> مُقيَّدان بحدود آمنة.
    /// </summary>
    Task<Result<MyCyclesDto>> GetMyCyclesAsync(
        ReportingCalendarContext context,
        Guid? templateId,
        int? past,
        int? future,
        CancellationToken ct = default);

    /// <summary>
    /// حلّ دورة واحدة لمفتاح معطى بحسب دور المستخدم الحاليّ (تشخيص/تحقّق إداريّ). يرفض المفاتيح غير الصالحة بنيويًّا.
    /// </summary>
    Task<Result<ReportingCycleDto>> ResolveAsync(
        string cycleKey,
        ReportingCalendarContext context,
        CancellationToken ct = default);

    /// <summary>
    /// نافذة الأيام اليومية للمستخدم الحاليّ (ماضٍ قريب + اليوم + مستقبل مسموح) مُدرِكة لحالة تسليماته.
    /// <paramref name="anchorDate"/> نقطة ارتكاز اختيارية (YYYY-MM-DD) للتنقّل، وإلّا اليوم بتوقيت الرياض.
    /// <paramref name="previousCount"/>/<paramref name="nextCount"/> مُقيَّدان بحدود آمنة.
    /// حالة كل يوم تُقرأ من قاعدة البيانات (قراءة فقط) لا من الواجهة.
    /// </summary>
    Task<Result<MyDaysDto>> GetMyDaysAsync(
        string? anchorDate,
        int? previousCount,
        int? nextCount,
        Guid? templateId,
        CancellationToken ct = default);
}
