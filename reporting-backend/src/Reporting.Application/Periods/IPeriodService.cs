using Reporting.Application.Common;

namespace Reporting.Application.Periods;

/// <summary>
/// P1-KPI-002 — **مصدر الحقيقة الوحيد** لحدود الفترات في تحليلات KPI.
/// كل الحدود بتوقيت <c>Asia/Riyadh</c> (السبت 00:00 → الجمعة 23:59:59.999 للأسبوع)،
/// ولا تُعيد الواجهة حساب أيّ حدّ بـUTC أو بالتوقيت المحلّيّ للمتصفّح (B-1).
/// الخدمة **لا تكتب شيئًا ولا تمسّ أيّ PeriodKey مخزَّن** — دوال حلّ خالصة فقط.
/// </summary>
public interface IPeriodService
{
    /// <summary>يحلّ طلب فترة إلى حدود قانونيّة، أو يفشل برسالة عربيّة ورمز خطأ.</summary>
    Result<ResolvedPeriod> Resolve(PeriodRequest request);

    /// <summary>آخر أسبوع **مكتمل** (انتهت جمعته) — الافتراضيّ التنظيميّ.</summary>
    ResolvedPeriod LastCompletedWeek();

    /// <summary>
    /// الفترة السابقة المقارِنة لفترة محلولة (أسبوع−7 أيام، شهر−1، ربع−1، سنة−1،
    /// والمخصّص يُزاح بطول المدى نفسه). تُستعمل لحساب <c>delta</c>/<c>trend</c>.
    /// </summary>
    ResolvedPeriod PreviousComparable(ResolvedPeriod current);

    /// <summary>
    /// مفاتيح الدورات الأسبوعيّة (YYYY-Www) الواقعة داخل الفترة — الأسبوع هو وحدة تخزين
    /// <c>KpiEvaluation.PeriodKey</c>، فالتجميع الشهريّ/الربعيّ/السنويّ يُعبَّر عنه كمجموعة مفاتيح أسبوع.
    /// انتماء الدورة يُحسم بـ**مرجع الثلاثاء** فلا تُحتسب دورة واحدة لفترتين.
    /// </summary>
    IReadOnlyList<string> WeekKeysWithin(ResolvedPeriod period);
}
