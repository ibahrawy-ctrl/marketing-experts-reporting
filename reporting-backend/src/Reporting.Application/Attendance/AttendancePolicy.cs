using Reporting.Domain.Enums;

namespace Reporting.Application.Attendance;

/// <summary>
/// P2-ATT-004 — سياسات الحضور النقيّة: تاريخ الرياض، حساب المدّة، ومواعيد SLA.
///
/// **أيّام عمل الحضور: الأحد → الخميس**، وهي **مستقلّة تمامًا** عن أسبوع KPI (السبت → الجمعة)
/// في مرحلة 1. لا يُخترَع هنا أيّ يوم عطلة رسميّة ولا تقويم إجازات: الجمعة والسبت فقط تُستثنى،
/// وما عداهما يُعدّ يوم عمل حتّى تصل سياسة عطل رسميّة حقيقيّة من مصدرها.
/// </summary>
public static class AttendancePolicy
{
    /// <summary>معرّف منطقة الرياض — الحضور واقعة يوم محلّيّ لا لحظة عالميّة.</summary>
    public const string TimeZoneId = "Asia/Riyadh";

    private static TimeZoneInfo Riyadh => TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

    /// <summary>تاريخ الواقعة بتقويم الرياض من لحظة UTC.</summary>
    public static DateOnly RiyadhDate(DateTimeOffset utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utc, Riyadh).DateTime);

    /// <summary>
    /// **الفارق بين بلاغ وواقعة مؤكَّدة** — تعريف واحد يقرأه كلّ من يعرض الحضور
    /// (خدمة الحضور، وعرض الموظّف 360، ولوحة الموارد البشريّة) فلا يتفرّق المعنى بينهم.
    ///
    /// <para>المؤكَّد هو ما قرّرته الموارد البشريّة صراحةً: <c>Confirmed</c>، أو ما أُغلِق/صُعِّد
    /// بعد قرار تأكيد. الإغلاق بذاته ليس تأكيدًا — الواقعة تُغلَق مرفوضةً أيضًا.</para>
    /// </summary>
    public static bool IsOfficialIncident(AttendanceIncidentStatus status, AttendanceHrDecision decision) =>
        status == AttendanceIncidentStatus.Confirmed
        || (status is AttendanceIncidentStatus.Closed or AttendanceIncidentStatus.Escalated
            && decision == AttendanceHrDecision.Confirm);

    /// <summary>يوم عمل حضور = الأحد حتّى الخميس. الجمعة والسبت خارج أيّام العمل.</summary>
    public static bool IsWorkingDay(DateOnly date) =>
        date.DayOfWeek is not (DayOfWeek.Friday or DayOfWeek.Saturday);

    /// <summary>
    /// مدّة الحادثة بالدقائق مشتقّة من الوقتين لا مُدخَلة يدويًّا.
    /// غياب أحد الوقتين أو عودةٌ قبل البداية ⟵ <c>null</c> (لا رقم مُختلَق ولا سالب).
    /// </summary>
    public static int? ComputeDurationMinutes(TimeOnly? start, TimeOnly? returnTime)
    {
        if (start is null || returnTime is null) return null;

        // طرح TimeOnly يلتفّ عبر منتصف الليل ولا ينتج سالبًا أبدًا، فـ«عودة قبل البداية»
        // كانت ستُنتج ~23 ساعة بدل رفضها. المقارنة الصريحة هي الحارس الصحيح.
        if (returnTime.Value < start.Value) return null;

        return (int)(returnTime.Value - start.Value).TotalMinutes;
    }

    /// <summary>
    /// موعد انتهاء نافذة ردّ الموظّف — ساعات تقويميّة (48 افتراضيًّا، قابلة للضبط من الإعدادات).
    /// ساعات لا أيّام عمل، لأنّ النافذة حقّ للموظّف لا التزام إداريّ.
    /// </summary>
    public static DateTime EmployeeResponseDeadlineUtc(DateTime fromUtc, int hours) =>
        fromUtc.AddHours(hours <= 0 ? 48 : hours);

    /// <summary>
    /// موعد انتهاء مراجعة الموارد البشريّة — **أيّام عمل** (5 افتراضيًّا)، تُعدّ بتقويم الرياض
    /// وتتخطّى الجمعة والسبت. تُحسَب من اليوم التالي لا من يوم الإحالة نفسه.
    /// </summary>
    public static DateTime HrReviewDeadlineUtc(DateTime fromUtc, int workingDays)
    {
        var remaining = workingDays <= 0 ? 5 : workingDays;
        var cursor = TimeZoneInfo.ConvertTimeFromUtc(fromUtc, Riyadh);

        while (remaining > 0)
        {
            cursor = cursor.AddDays(1);
            if (IsWorkingDay(DateOnly.FromDateTime(cursor))) remaining--;
        }

        return TimeZoneInfo.ConvertTimeToUtc(cursor, Riyadh);
    }

    /// <summary>عدد أيّام العمل بين تاريخين (شاملًا الطرفين) — لقياس التقادم في الطوابير.</summary>
    public static int WorkingDaysBetween(DateOnly from, DateOnly to)
    {
        if (to < from) return 0;
        var count = 0;
        for (var d = from; d <= to; d = d.AddDays(1))
            if (IsWorkingDay(d)) count++;
        return count;
    }
}
