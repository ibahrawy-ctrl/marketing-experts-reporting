using Reporting.Domain.Enums;

namespace Reporting.Application.Projects360;

/// <summary>
/// عقود **جسر التنفيذ الهجين** (P360-WF-R2 §10).
///
/// <para>
/// **لماذا ملفّ مستقلّ؟** <c>Project360Models.cs</c> عقد لوحة المشروع وكياناته البنيويّة؛ والجسر
/// طبقة عبورٍ بين منظومتين (التقارير والمخرَجات التعاقديّة). فصله يُبقي كلّ عقد مقروءًا وحده،
/// ويجعل التزام الجسر قابلًا للبناء منفردًا.
/// </para>
/// </summary>
public record ProjectExecutionProposalDto(
    Guid Id,
    Guid ProjectId,
    Guid DeliverableId,
    string DeliverableName,

    // النسبة والحالة الحاليّتان للمخرَج **لحظة القراءة**: المراجِع يقارن المُدَّعى بالقائم
    // لا بالذاكرة، ولا يقبل نسبةً أدنى ممّا بلغه المخرَج فعلًا وهو لا يدري.
    decimal DeliverableCurrentProgressPercent,
    ProjectDeliverableStatus DeliverableCurrentStatus,

    Guid? SubmissionId,
    Guid ProposedById,
    string? ProposedByFullName,
    decimal ProposedProgressPercent,
    ProjectDeliverableStatus ProposedStatus,
    string? ExecutionNote,

    ExecutionProposalStatus Status,
    Guid? ReviewedById,
    string? ReviewedByFullName,
    DateTime? ReviewedAtUtc,
    string? ReviewNote,

    // لقطة ما قبل التطبيق — تُملأ عند القبول وحده، فتبقى null للمعلَّق والمرفوض.
    decimal? PreviousProgressPercent,

    DateTime CreatedAtUtc,

    // هل يملك المستخدم الحاليّ حسم هذا المقترح؟ **مرآة الحارس نفسه** لا نسخة موازية منه —
    // الواجهة لا تُظهر زرّ قبولٍ سيردّه الخادم.
    bool CanReview);

/// <summary>
/// رفع ادّعاء تنفيذ. <c>SubmissionId</c> اختياريّ عمدًا: تنفيذٌ واقعيّ لم يُوثَّق في تقرير
/// يبقى قابلًا للتسجيل بدل أن يضيع.
/// </summary>
public record CreateProjectExecutionProposalRequest(
    Guid DeliverableId,
    decimal ProposedProgressPercent,
    ProjectDeliverableStatus ProposedStatus,
    string? ExecutionNote,
    Guid? SubmissionId = null);

/// <summary>
/// قرار المراجِع. <c>Accept=false</c> يوجب <c>ReviewNote</c> — الرفض الصامت يوقف المنفِّذ
/// بلا ما يصحّحه (§10).
/// </summary>
public record ReviewProjectExecutionProposalRequest(
    bool Accept,
    string? ReviewNote);
