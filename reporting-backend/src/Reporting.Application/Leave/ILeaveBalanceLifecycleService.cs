using Reporting.Domain.Entities.Leave;
using Reporting.Domain.Enums;

namespace Reporting.Application.Leave;

/// <summary>نتيجة محاولة تسجيل حركة رصيد ضمن دورة حياة طلب الإجازة/الاستئذان.</summary>
public enum LeaveBalanceLifecycleOutcome
{
    /// <summary>أُدرجت الحركة الآن في المعاملة الجارية (لم تُحفظ بعد — الحفظ مسؤولية المستدعي).</summary>
    Created = 0,

    /// <summary>الحركة موجودة سلفًا لهذا الطلب — لا تكرار (idempotent).</summary>
    AlreadyApplied = 1,

    /// <summary>لا يوجد خصم أصليّ لعكسه — لا حركة.</summary>
    NoDebitToReverse = 2
}

/// <summary>
/// حالة دورة حياة رصيد طلب واحد كما هي مُسجَّلة في دفتر الحركات (قراءة فقط، مشتقّة من الصفوف لا من ذاكرة مؤقّتة).
/// </summary>
public sealed record LeaveBalanceLifecycleState(
    bool HasDebit,
    bool HasReversal,
    decimal DebitAmount,
    decimal ReversalAmount,
    BalanceType? BalanceType,
    int? Year)
{
    /// <summary>صافي أثر الطلب على الرصيد (خصم لم يُعكَس ⇒ موجب).</summary>
    public decimal NetDebit => DebitAmount - ReversalAmount;
}

/// <summary>
/// خدمة دورة حياة رصيد الإجازات/الأذونات (LEAVE-DEDUCTION-ON-TL-APPROVAL-R1).
/// مسؤوليّة واحدة: تحويل قرارات سير العمل إلى حركات دفتر (Debit/Reversal) بلا تكرار وبلا حذف.
///
/// عقد المعاملة (مُلزِم): كلّ الدوال تُدرِج الحركة في متتبّع التغيير فقط ولا تستدعي SaveChanges إطلاقًا.
/// يملك المعاملةَ مستدعيها، فيُحفظ تغيير حالة الطلب والحركة معًا في SaveChanges واحد ⇒ ذرّيّة تامّة:
/// إن فشلت الحركة فشل الانتقال كلّه، وإن فشل الانتقال لم تُكتب حركة. لا SQL مباشر، ولا تعديل رصيد
/// خارج الدفتر (الرصيد مشتقّ: Σ Credit − Σ Debit).
/// </summary>
public interface ILeaveBalanceLifecycleService
{
    /// <summary>
    /// يُدرِج حركة الخصم (Debit) لطلب اعتُمد عند خطوة قائد الفريق.
    /// idempotent عبر (RelatedRequestId, Source): يُستدعى عند كلّ انتقال اعتماد فيقع فعليًّا مرّة واحدة
    /// عند أوّل اعتماد، ويصير بلا أثر عند اعتماد المدير أو الموارد البشرية لاحقًا.
    /// </summary>
    Task<LeaveBalanceLifecycleOutcome> ApplyDebitOnTeamLeaderApprovalAsync(
        LeaveRequest entity, Guid actor, CancellationToken ct = default);

    /// <summary>
    /// يُدرِج حركة عكس (Credit/Reversal) مطابِقة للخصم الأصليّ دون حذفه، عند رفض أو إلغاء أو إرجاع
    /// أو إبطال لاحق. idempotent عبر (RelatedRequestId, Reversal): عكس واحد لكلّ طلب على الأكثر.
    /// </summary>
    Task<LeaveBalanceLifecycleOutcome> ReverseDebitAsync(
        LeaveRequest entity, Guid actor, string reason, CancellationToken ct = default);

    /// <summary>يقرأ حالة دورة حياة الرصيد لطلب واحد من الدفتر (قراءة فقط، بلا كتابة).</summary>
    Task<LeaveBalanceLifecycleState> GetCurrentBalanceLifecycleStateAsync(
        Guid leaveRequestId, CancellationToken ct = default);
}
