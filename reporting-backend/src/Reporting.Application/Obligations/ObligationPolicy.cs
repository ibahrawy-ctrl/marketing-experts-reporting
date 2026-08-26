using Reporting.Application.Common;

namespace Reporting.Application.Obligations;

/// <summary>
/// P2-HR-008 — المنطق النقيّ لمحرّك الالتزامات (بلا قاعدة بيانات، قابل للاختبار الوحدويّ مباشرةً).
/// <para>
/// <b>لا يكرّر</b> منطق التقويم (<see cref="ReportingCalendarPolicy"/>) ولا منطق الحالة المتوقّعة
/// للتقارير (<c>IExpectedSubmissionStatusResolver</c>) — يفوّض إليهما ويضيف فوقهما طبقتين غائبتين
/// عنهما: <b>إعفاء الإجازة المعتمَدة</b>، و<b>التزامات تقييم KPI</b>.
/// </para>
/// </summary>
public static class ObligationPolicy
{
    /// <summary>الحدّ الأقصى للدورات في نداء واحد — سقف أداء بنيويّ لا إعداد قابل للتجاوز.</summary>
    public const int MaxCycles = 26;

    /// <summary>عدد الدورات الافتراضيّ حين لا يُحدَّد مدى صريح.</summary>
    public const int DefaultRecentCycles = 8;

    /// <summary>
    /// مهلة إنجاز تقييم KPI الربعيّ بعد نهاية الربع (بالأيّام). ثابت معلَن هنا لأنّ التقويم
    /// القائم لا يعرّف موعد استحقاق ربعيًّا؛ الأسبوعيّ يستعمل <see cref="ReportingCalendarPolicy.RoleDueDate"/>.
    /// </summary>
    public const int QuarterlyEvaluationGraceDays = 7;

    /// <summary>مدى تاريخيّ مغلق ليوم واحد أو أكثر (شامل الطرفين).</summary>
    public readonly record struct DateSpan(DateOnly Start, DateOnly End);

    /// <summary>
    /// هل تغطّي الإجازات المعتمَدة <b>كامل</b> المدى من بداية الفترة حتّى موعد الاستحقاق؟
    /// <para>
    /// القاعدة متعمَّدة التشدّد: الإعفاء لا يُمنَح إلّا إذا لم تبقَ للموظّف أيّ فرصة عمل واحدة
    /// داخل المهلة. تغطية جزئيّة <b>لا</b> تُعفي — لأنّ إعفاءً سخيًّا يخفي نقصًا حقيقيًّا،
    /// وهو ضرر أكبر من مطالبة موظّف كان غائبًا بعض المدّة.
    /// </para>
    /// <para>الاستئذانات (ساعات داخل يوم) لا تُعفي إطلاقًا — المتّصل يرشّحها قبل الاستدعاء.</para>
    /// </summary>
    public static bool IsCoveredByApprovedLeave(DateOnly periodStart, DateOnly dueAt, IEnumerable<DateSpan> approvedLeaves)
    {
        if (dueAt < periodStart) return false;

        // ندمج المدَيات المتداخلة/المتلاصقة ثمّ نتحقّق من تغطية [periodStart, dueAt] بلا ثغرة.
        var spans = approvedLeaves
            .Where(s => s.End >= periodStart && s.Start <= dueAt)
            .OrderBy(s => s.Start)
            .ToList();
        if (spans.Count == 0) return false;

        var cursor = periodStart;
        foreach (var s in spans)
        {
            if (s.Start > cursor) return false;      // ثغرة قبل بداية هذا المدى ⇒ لا تغطية كاملة.
            if (s.End >= cursor) cursor = s.End.AddDays(1);
            if (cursor > dueAt) return true;
        }
        return cursor > dueAt;
    }

    /// <summary>
    /// اشتقاق الحالة الحصريّة والعدّادات من مدخلات نقيّة. هذه هي <b>النقطة الوحيدة</b> التي
    /// تُقرَّر فيها Missing/Late في المنظومة كلّها لهذا المحرّك.
    /// </summary>
    /// <param name="isAssigned">هل يوجد إسناد فعليّ (قالب مُسنَد للموظّف)؟</param>
    /// <param name="isUserActive">هل الموظّف نشط؟</param>
    /// <param name="isWithinApplicability">هل الفترة داخل أرضيّة الانطباق؟</param>
    /// <param name="isExemptByLeave">هل تغطّيه إجازة معتمَدة كاملة؟</param>
    /// <param name="isFulfilled">هل أُنجِز فعلًا؟</param>
    /// <param name="dueAt">موعد الاستحقاق.</param>
    /// <param name="fulfilledOn">تاريخ الإنجاز بتوقيت الرياض إن وُجد.</param>
    /// <param name="today">تاريخ اليوم بتوقيت الرياض (يُحقَن كي تكون الاختبارات حتميّة).</param>
    public static ObligationOutcome Derive(
        bool isAssigned,
        bool isUserActive,
        bool isWithinApplicability,
        bool isExemptByLeave,
        bool isFulfilled,
        DateOnly dueAt,
        DateOnly? fulfilledOn,
        DateOnly today)
    {
        // ① لا إسناد ⇒ لا التزام. لا يُعَدّ ناقصًا ولا متأخّرًا أبدًا (قاعدة غير قابلة للتفاوض).
        if (!isAssigned)
            return new ObligationOutcome(false, false, false, false, 0,
                ObligationState.NotApplicable, ObligationExemptionReason.NotAssigned, "لا إسناد");

        // ② الإعفاءات — تُفحَص قبل أيّ حساب تأخّر، فلا يتولّد نقصٌ ثمّ يُعفى.
        if (!isUserActive)
            return new ObligationOutcome(false, isFulfilled, false, false, 0,
                ObligationState.Exempt, ObligationExemptionReason.InactiveUser, "معفى — موظّف غير نشط");

        if (!isWithinApplicability)
            return new ObligationOutcome(false, isFulfilled, false, false, 0,
                ObligationState.Exempt, ObligationExemptionReason.BeforeApplicabilityFloor,
                "غير منطبِق — قبل أرضيّة الانطباق");

        if (isExemptByLeave)
            return new ObligationOutcome(false, isFulfilled, false, false, 0,
                ObligationState.Exempt, ObligationExemptionReason.ApprovedLeave, "معفى — إجازة معتمَدة");

        // ③ مطلوب فعلًا.
        if (isFulfilled)
        {
            var on = fulfilledOn ?? today;
            var lateDays = on > dueAt ? on.DayNumber - dueAt.DayNumber : 0;
            return new ObligationOutcome(true, true, false, lateDays > 0, lateDays,
                ObligationState.Fulfilled, ObligationExemptionReason.None,
                lateDays > 0 ? "مُنجَز متأخّرًا" : "مُنجَز في الموعد");
        }

        if (today <= dueAt)
            return new ObligationOutcome(true, false, false, false, 0,
                ObligationState.Pending, ObligationExemptionReason.None, "مطلوب — ضمن المهلة");

        var overdueDays = today.DayNumber - dueAt.DayNumber;
        return new ObligationOutcome(true, false, true, true, overdueDays,
            ObligationState.Missing, ObligationExemptionReason.None, "ناقص — تجاوز المهلة");
    }

    /// <summary>ناتج الاشتقاق النقيّ.</summary>
    public readonly record struct ObligationOutcome(
        bool Expected,
        bool Fulfilled,
        bool Missing,
        bool Late,
        int LateByDays,
        ObligationState State,
        ObligationExemptionReason ExemptionReason,
        string Label);

    /// <summary>مفتاح الربع الذي تنتمي إليه دورة أسبوعيّة (عبر مرجع الثلاثاء، فلا تُحتسب لربعين).</summary>
    public static string QuarterKeyForCycle(string cycleKey)
    {
        var start = ReportingCalendarPolicy.CycleRange(cycleKey).Start;
        var tuesday = ReportingCalendarPolicy.TuesdayReference(start);
        var quarter = ((tuesday.Month - 1) / 3) + 1;
        return $"{tuesday.Year:D4}-Q{quarter}";
    }

    /// <summary>حدود ربع من مفتاحه <c>YYYY-Qn</c>.</summary>
    public static (DateOnly Start, DateOnly End) QuarterRange(string quarterKey)
    {
        var year = int.Parse(quarterKey[..4]);
        var quarter = int.Parse(quarterKey[6..]);
        return ReportingCalendarPolicy.QuarterRange(year, quarter);
    }

    /// <summary>موعد استحقاق تقييم KPI الربعيّ = نهاية الربع + مهلة ثابتة.</summary>
    public static DateOnly QuarterlyDueDate(string quarterKey) =>
        QuarterRange(quarterKey).End.AddDays(QuarterlyEvaluationGraceDays);
}
