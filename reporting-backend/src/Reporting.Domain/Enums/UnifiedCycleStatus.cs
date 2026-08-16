namespace Reporting.Domain.Enums;

/// <summary>
/// REPORTING-CYCLE-SUBMISSION-STATUS-CONSISTENCY-R1 — الحالة الموحّدة لدورة تقرير واحدة.
/// مصدر الحقيقة الوحيد لعرض حالة الدورة عبر كل الشاشات (منتقي الأسبوع، لوحة الموظف، الشاشات الأخرى).
/// تُشتَقّ حصريًّا عبر <see cref="Reporting.Domain.Common.UnifiedCycleStatusPolicy"/> (دالّة نقيّة).
/// لا تُحسَب حالة أو تأخّر في الواجهة إطلاقًا — الواجهة تقرأ هذه القيمة فقط.
/// </summary>
public enum UnifiedCycleStatus
{
    /// <summary>لا Assignment صالح للقالب/الدور/الفترة — الموظّف غير مطالَب أصلًا.</summary>
    NotAssigned = 0,

    /// <summary>مُستثنى كامل الدورة (إجازة معتمَدة) — قيمة محجوزة، لا تُوسَّع في هذه المرحلة.</summary>
    NotRequired = 1,

    /// <summary>مُسنَد، الدورة مستقبليّة/قبل نافذة الاستحقاق، لا تسليم.</summary>
    NotDue = 2,

    /// <summary>مُسنَد، نافذة الاستحقاق مفتوحة، اليوم ≤ الموعد، لا تسليم.</summary>
    DueNow = 3,

    /// <summary>مسودّة (Draft) قبل الموعد — ليست تأخّرًا.</summary>
    Draft = 4,

    /// <summary>مسودّة بعد الموعد — تأخّر لا «مُسلَّم».</summary>
    OverdueDraft = 5,

    /// <summary>تسليم Submitted، SubmittedAt ≤ الموعد، بلا خطوة اعتماد بعد.</summary>
    SubmittedOnTime = 6,

    /// <summary>تسليم Submitted، SubmittedAt &gt; الموعد.</summary>
    SubmittedLate = 7,

    /// <summary>عالق عند معتمِد (Submitted متحرّك/اعتماد جزئيّ/تصعيد) — لا إجراء على الموظّف.</summary>
    PendingApproval = 8,

    /// <summary>Returned، اليوم ≤ الموعد — يحتاج إجراء الموظّف ضمن المهلة.</summary>
    ReturnedForChanges = 9,

    /// <summary>Returned، اليوم &gt; الموعد — معاد وفات موعده.</summary>
    OverdueReturned = 10,

    /// <summary>
    /// اسم اشتقاقيّ محجوز: اكتمل الاعتماد قبل الإغلاق النهائيّ. النظام terminal=Closed ولا يملك حالة
    /// Approved طرفيّة مستقلّة، لذا لا تُصدِرها السياسة (تُطوى في <see cref="Closed"/>). محجوزة للاكتمال فقط.
    /// </summary>
    Approved = 11,

    /// <summary>Status ∈ {Closed, Visible} — الحالة الطرفيّة الفعليّة.</summary>
    Closed = 12,

    /// <summary>مُسنَد، اليوم &gt; الموعد، لا تسليم صالح (بعد استبعاد Soft-deleted) — جوهر الإصلاح.</summary>
    OverdueNotSubmitted = 13
}

/// <summary>شدّة الحالة الموحّدة — تُستخدَم لاشتقاق اللون/النبرة في الواجهة (لا تُحسَب في الواجهة).</summary>
public enum CycleStatusSeverity
{
    None = 0,
    Info = 1,
    Success = 2,
    Warn = 3,
    Alert = 4
}
