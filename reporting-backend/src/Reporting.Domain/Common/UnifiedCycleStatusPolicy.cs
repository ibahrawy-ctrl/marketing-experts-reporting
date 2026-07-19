using Reporting.Domain.Enums;

namespace Reporting.Domain.Common;

/// <summary>
/// REPORTING-CYCLE-SUBMISSION-STATUS-CONSISTENCY-R1 — مدخلات اشتقاق الحالة الموحّدة لدورة تقرير واحدة.
/// كلّها قيم مُمرَّرة من الأعلى (الخدمة الموحّدة) — لا يقرأ هذا العقد قاعدة بيانات ولا وقتًا ولا منطقة زمنية.
/// الوقت الحاليّ ومواعيد الدورة تُمرَّر <c>DateTimeOffset</c> مضبوطةً على منطقة النظام (الرياض) قبل الاستدعاء.
/// </summary>
/// <param name="IsAssigned">هل يوجد Assignment صالح للموظّف/القالب/الفترة؟ (false ⇒ NotAssigned).</param>
/// <param name="IsRequired">هل الدورة مطلوبة فعلًا؟ (false مع IsAssigned ⇒ NotRequired — إجازة/استثناء).</param>
/// <param name="CycleStartsAt">بداية نافذة استحقاق الدورة (فتح التسليم).</param>
/// <param name="DueAt">الموعد النهائيّ للدورة.</param>
/// <param name="CurrentTime">الوقت الحاليّ (منطقة النظام).</param>
/// <param name="HasSubmission">هل يوجد تسليم مرتبط بالدورة؟ (قبل فحص الحذف الناعم).</param>
/// <param name="SubmissionStatus">حالة التسليم إن وُجد.</param>
/// <param name="SubmittedAt">لحظة التسليم (Submitted) إن وُجدت.</param>
/// <param name="ApprovedAt">لحظة اكتمال الاعتماد إن وُجدت (إعلاميّة).</param>
/// <param name="ClosedAt">لحظة الإغلاق النهائيّ إن وُجدت (إعلاميّة).</param>
/// <param name="IsDeleted">هل التسليم محذوف ناعمًا (ملغى)؟ (true ⇒ يُعامَل كأنّه بلا تسليم).</param>
public readonly record struct CycleStatusInput(
    bool IsAssigned,
    bool IsRequired,
    DateTimeOffset CycleStartsAt,
    DateTimeOffset DueAt,
    DateTimeOffset CurrentTime,
    bool HasSubmission,
    SubmissionStatus? SubmissionStatus,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? ClosedAt,
    bool IsDeleted);

/// <summary>نتيجة اشتقاق الحالة الموحّدة — قيمة نقيّة بلا آثار جانبية.</summary>
/// <param name="Status">الحالة الموحّدة المشتقّة.</param>
/// <param name="Severity">شدّة الحالة (مصدر اللون/النبرة في الواجهة).</param>
/// <param name="IsLate">هل التسليم متأخّر (SubmittedAt &gt; DueAt) أو الدورة متجاوِزة موعدها بلا تسليم؟</param>
/// <param name="DelayDays">عدد أيّام التأخّر الكاملة (≥ 0).</param>
public readonly record struct CycleStatusResult(
    UnifiedCycleStatus Status,
    CycleStatusSeverity Severity,
    bool IsLate,
    int DelayDays);

/// <summary>
/// REPORTING-CYCLE-SUBMISSION-STATUS-CONSISTENCY-R1 — السياسة النقيّة الوحيدة لاشتقاق حالة دورة تقرير واحدة.
/// دالّة نقيّة (deterministic) بلا I/O ولا حالة عامّة — مصدر الحقيقة الوحيد للحالة عبر كل الطبقات.
/// الواجهة لا تحسب أيّ حالة أو تأخّر — تستهلك النتيجة فقط. ترتيب الاشتقاق يتبع تقرير التصميم §4-أ.
/// </summary>
public static class UnifiedCycleStatusPolicy
{
    /// <summary>يشتقّ الحالة الموحّدة + الشدّة + علم التأخّر + أيّام التأخّر من المدخلات.</summary>
    public static CycleStatusResult Derive(in CycleStatusInput input)
    {
        // (1) غير مُسنَد ⇒ الموظّف غير مطالَب أصلًا.
        if (!input.IsAssigned)
            return new(UnifiedCycleStatus.NotAssigned, CycleStatusSeverity.None, false, 0);

        // (2) مُسنَد لكنّه غير مطلوب (استثناء/إجازة معتمَدة كامل الدورة).
        if (!input.IsRequired)
            return new(UnifiedCycleStatus.NotRequired, CycleStatusSeverity.None, false, 0);

        // تسليم فعّال = موجود وغير محذوف ناعمًا وله حالة. الحذف الناعم يُلغي التسليم من الاعتبار.
        var hasActiveSubmission = input.HasSubmission && !input.IsDeleted && input.SubmissionStatus.HasValue;

        // الحدّان صارمان: الوقت == الموعد ليس تجاوزًا، والتسليم == الموعد ليس تأخّرًا.
        var overdue = input.CurrentTime > input.DueAt;
        var submittedLate = input.SubmittedAt.HasValue && input.SubmittedAt.Value > input.DueAt;

        // (3) التسليم الفعّال يسبق الحكم على الموعد.
        if (hasActiveSubmission)
        {
            switch (input.SubmissionStatus!.Value)
            {
                case Enums.SubmissionStatus.Draft:
                    return overdue
                        ? new(UnifiedCycleStatus.OverdueDraft, CycleStatusSeverity.Warn, false, WholeDaysBetween(input.DueAt, input.CurrentTime))
                        : new(UnifiedCycleStatus.Draft, CycleStatusSeverity.Info, false, 0);

                case Enums.SubmissionStatus.Returned:
                    return overdue
                        ? new(UnifiedCycleStatus.OverdueReturned, CycleStatusSeverity.Alert, false, WholeDaysBetween(input.DueAt, input.CurrentTime))
                        : new(UnifiedCycleStatus.ReturnedForChanges, CycleStatusSeverity.Warn, false, 0);

                case Enums.SubmissionStatus.Submitted:
                    return submittedLate
                        ? new(UnifiedCycleStatus.SubmittedLate, CycleStatusSeverity.Warn, true, WholeDaysBetween(input.DueAt, input.SubmittedAt!.Value))
                        : new(UnifiedCycleStatus.SubmittedOnTime, CycleStatusSeverity.Success, false, 0);

                // عالق عند معتمِد (اعتماد جزئيّ / تصعيد) — لا إجراء على الموظّف.
                case Enums.SubmissionStatus.ApprovedByDirectManager:
                case Enums.SubmissionStatus.ApprovedByNextLevel:
                case Enums.SubmissionStatus.Escalated:
                    return new(UnifiedCycleStatus.PendingApproval, CycleStatusSeverity.Info, submittedLate, submittedLate ? WholeDaysBetween(input.DueAt, input.SubmittedAt!.Value) : 0);

                // الحالة الطرفيّة الفعليّة — لا تُوصَف أبدًا كمتجاوِزة-بلا-تسليم.
                case Enums.SubmissionStatus.Closed:
                case Enums.SubmissionStatus.Visible:
                    return new(UnifiedCycleStatus.Closed, CycleStatusSeverity.Success, submittedLate, submittedLate ? WholeDaysBetween(input.DueAt, input.SubmittedAt!.Value) : 0);
            }
        }

        // (4) لا تسليم فعّال: الحكم على الموعد.
        if (overdue)
            return new(UnifiedCycleStatus.OverdueNotSubmitted, CycleStatusSeverity.Alert, true, WholeDaysBetween(input.DueAt, input.CurrentTime));

        // نافذة الاستحقاق مفتوحة (اليوم ≥ بداية الدورة، واليوم ≤ الموعد).
        if (input.CurrentTime >= input.CycleStartsAt)
            return new(UnifiedCycleStatus.DueNow, CycleStatusSeverity.Info, false, 0);

        // الدورة مستقبليّة — قبل نافذة الاستحقاق.
        return new(UnifiedCycleStatus.NotDue, CycleStatusSeverity.None, false, 0);
    }

    /// <summary>
    /// عدد الأيّام الكاملة (تقريبًا لأعلى) بين لحظتَين — يُستخدَم لأيّام التأخّر (≥ 0).
    /// أيّ جزء من يوم يُحتسَب يومًا كاملًا (مثال: 25 ساعة ⇒ يومان). الفرق غير الموجب ⇒ 0.
    /// </summary>
    private static int WholeDaysBetween(DateTimeOffset from, DateTimeOffset to)
    {
        var span = to - from;
        if (span <= TimeSpan.Zero)
            return 0;
        return (int)Math.Ceiling(span.TotalDays);
    }
}
