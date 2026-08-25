namespace Reporting.Domain.Enums;

/// <summary>
/// حالة حادثة الحضور (P2-ATT-005/006). ملفّ مستقلّ إضافيّ — لا يمسّ <c>Enums.cs</c> القائم.
///
/// **مبدأ حاكم:** <see cref="Reported"/> بلاغ مبدئيّ لا واقعة رسميّة. لا تصير الحادثة واقعة
/// إلّا بـ<see cref="Confirmed"/> بعد ردّ الموظّف أو انقضاء نافذته الموثَّقة. ولا يترتّب على
/// أيّ انتقال هنا أثر ماليّ ولا خصم ولا ربط برواتب إطلاقًا.
/// </summary>
public enum AttendanceIncidentStatus
{
    /// <summary>مسودّة لدى المُبلِّغ — قابلة للتعديل والإلغاء والحذف قبل الإرسال.</summary>
    Draft = 0,

    /// <summary>أُرسِلت. بلاغ مبدئيّ. لا حذف عاديّ بعد هذه النقطة.</summary>
    Reported = 1,

    /// <summary>بانتظار ردّ الموظّف ضمن نافذة SLA.</summary>
    AwaitingEmployee = 2,

    /// <summary>أقرّ الموظّف بالحادثة.</summary>
    Acknowledged = 3,

    /// <summary>اعترض الموظّف وقدّم روايته.</summary>
    Disputed = 4,

    /// <summary>انقضت نافذة ردّ الموظّف بلا ردّ — واقعة موثَّقة لا عقوبة.</summary>
    EmployeeResponseTimedOut = 5,

    /// <summary>بانتظار مراجعة الموارد البشريّة.</summary>
    AwaitingHr = 6,

    /// <summary>أكّدتها الموارد البشريّة. لا تُحذف بعد الإغلاق؛ التصحيح بحدث لا بحذف.</summary>
    Confirmed = 7,

    /// <summary>رفضتها الموارد البشريّة — البلاغ غير صحيح.</summary>
    Rejected = 8,

    /// <summary>صُحِّحت بياناتها؛ تعود إلى الموظّف عند التغيير الجوهريّ ولا تُؤكَّد مباشرة.</summary>
    Corrected = 9,

    /// <summary>سُوِّيت بإجازة/استئذان معتمد يغطّي الواقعة.</summary>
    Reconciled = 10,

    /// <summary>صُعِّدت إلى الحوكمة بإذن صريح.</summary>
    Escalated = 11,

    /// <summary>أُغلِقت.</summary>
    Closed = 12,

    /// <summary>أُلغِيت وهي مسودّة.</summary>
    Cancelled = 13,

    /// <summary>سحبها مُنشِئها قبل إقرار الموظّف، بسبب موثَّق.</summary>
    Withdrawn = 14,

    /// <summary>أُبطِلت بعد التأكيد بقرار موارد بشريّة موثَّق — بديل الحذف.</summary>
    Voided = 15
}

/// <summary>مصدر رصد الحادثة. لا يوجد تكامل آليّ في هذا الإصدار؛ القيمة توثيقيّة.</summary>
public enum AttendanceDetectionSource
{
    /// <summary>رصد بشريّ من قائد الفريق/المدير.</summary>
    Manual = 0,

    /// <summary>استيراد من ملفّ خارجيّ (غير مُفعَّل بعد).</summary>
    Import = 1,

    /// <summary>اشتقاق آليّ من النظام (غير مُفعَّل بعد).</summary>
    System = 2
}

/// <summary>قرار الموارد البشريّة النهائيّ على الحادثة.</summary>
public enum AttendanceHrDecision
{
    None = 0,
    Confirm = 1,
    Reject = 2,
    Correct = 3,
    Reconcile = 4,
    Void = 5
}
