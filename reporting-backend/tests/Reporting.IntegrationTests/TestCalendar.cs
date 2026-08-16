using Reporting.Application.Common;

namespace Reporting.IntegrationTests;

/// <summary>
/// RECONCILE-PROD-DEVELOP-LINEAGE — مولّد مفاتيح فترات **صالحة** للاختبارات.
///
/// خلفيّة: كانت اختبارات كثيرة تُصلِّب مفاتيح فترات بعيدة في المستقبل (<c>2028-05-03</c>،
/// <c>2099-W01</c>، <c>2026-W41</c>…) لتفادي التصادم على قاعدة الاختبار المشتركة. ثمّ أضاف خطّ
/// الإنتاج بوّابات تقويم خادميّة حيّة (ROLE-AWARE-REPORTING-CALENDAR §2.4 و
/// DAILY-BUSINESS-DAY-COMPLIANCE-R1) تمنع الإنشاء على فترة لم تبدأ بعد:
/// <c>calendar.cycle_not_open</c> و<c>calendar.future_day_locked</c>. لذلك صار كلّ مفتاح مستقبليّ
/// مرفوضًا، وهذه البوّابات **سلوك حيّ يجب الحفاظ عليه** — فالخطأ في توقّع الاختبار لا في المنتج.
///
/// هذا المولّد يشتقّ المفاتيح من <see cref="ReportingCalendarPolicy.RiyadhToday"/> — وهو العرف الذي
/// اتّبعه خطّ الإنتاج نفسه حيث حُدِّثت الاختبارات. التفرّد لم يعد يعتمد على بُعد التاريخ لأنّ كلّ
/// اختبار ينشئ مستخدميه الخاصّين، والقياس صار على قواعد نظيفة معزولة.
/// </summary>
internal static class TestCalendar
{
    /// <summary>اليوم بتوقيت الرياض — مرجع كلّ الاشتقاقات.</summary>
    public static DateOnly Today => ReportingCalendarPolicy.RiyadhToday();

    /// <summary>
    /// مفتاح دورة أسبوعيّة **مفتوحة** (بدأت فعلًا): <paramref name="weeksBack"/> = 0 الدورة الحاليّة،
    /// 1 الدورة السابقة، وهكذا. لا تُرجِع أبدًا دورة مستقبليّة.
    /// </summary>
    public static string Cycle(int weeksBack = 0)
        => ReportingCalendarPolicy.CycleKeyFor(Today.AddDays(-7 * Math.Max(0, weeksBack)));

    /// <summary>بداية الدورة (السبت) لمفتاح دورة.</summary>
    public static DateOnly CycleStart(string cycleKey)
        => ReportingCalendarPolicy.CycleRange(cycleKey).Start;

    /// <summary>
    /// مفتاح يوم **صالح للإنشاء**: ليس في المستقبل وليس يوم جمعة.
    /// <paramref name="daysBack"/> يُعدّ بأيّام العمل المسموح بها (تُتخطّى الجمعة) رجوعًا من اليوم.
    /// </summary>
    public static string Day(int daysBack = 0)
    {
        var d = Today;
        var remaining = Math.Max(0, daysBack);
        while (true)
        {
            if (!ReportingCalendarPolicy.IsDailySubmissionBlockedDay(d))
            {
                if (remaining == 0) return ReportingCalendarPolicy.DayKey(d);
                remaining--;
            }
            d = d.AddDays(-1);
        }
    }

    /// <summary>عدد <paramref name="count"/> من مفاتيح الأيّام الصالحة، الأحدث أوّلًا.</summary>
    public static string[] Days(int count, int startDaysBack = 0)
        => Enumerable.Range(0, count).Select(i => Day(startDaysBack + i)).ToArray();

    /// <summary>
    /// أحدث <paramref name="count"/> يومًا صالحًا داخل **الشهر المنقضي**، الأقدم أوّلًا.
    /// يضمن انتماء كلّ الأيّام إلى نفس المفتاح الشهريّ ونفس المفتاح الربعيّ (مفيد لاختبارات التجميع).
    /// </summary>
    public static string[] DaysInPreviousMonth(int count = 2)
    {
        var d = new DateOnly(Today.Year, Today.Month, 1).AddDays(-1); // آخر يوم في الشهر المنقضي
        var picked = new List<DateOnly>();
        while (picked.Count < count)
        {
            if (!ReportingCalendarPolicy.IsDailySubmissionBlockedDay(d)) picked.Add(d);
            d = d.AddDays(-1);
        }
        picked.Reverse();
        return picked.Select(ReportingCalendarPolicy.DayKey).ToArray();
    }

    /// <summary>المفتاح الشهريّ (<c>yyyy-MM</c>) لمفتاح يوم.</summary>
    public static string MonthKeyOf(string dayKey) => dayKey[..7];

    /// <summary>المفتاح الربعيّ (<c>yyyy-Qn</c>) لمفتاح يوم.</summary>
    public static string QuarterKeyOf(string dayKey)
    {
        var d = ReportingCalendarPolicy.ParseDayKey(dayKey);
        return $"{d.Year:D4}-Q{(d.Month - 1) / 3 + 1}";
    }

    /// <summary>أوّل يوم صالح داخل دورة معيّنة لا يتجاوز اليوم (مفيد للتجميع داخل دورة واحدة).</summary>
    public static string DayInCycle(string cycleKey, int indexFromStart = 0)
    {
        var (start, end) = ReportingCalendarPolicy.CycleRange(cycleKey);
        var cap = end < Today ? end : Today;
        var found = 0;
        for (var d = start; d <= cap; d = d.AddDays(1))
        {
            if (ReportingCalendarPolicy.IsDailySubmissionBlockedDay(d)) continue;
            if (found == indexFromStart) return ReportingCalendarPolicy.DayKey(d);
            found++;
        }
        throw new InvalidOperationException(
            $"لا يوجد يوم صالح رقم {indexFromStart} داخل الدورة {cycleKey} حتّى {cap:yyyy-MM-dd}.");
    }
}
