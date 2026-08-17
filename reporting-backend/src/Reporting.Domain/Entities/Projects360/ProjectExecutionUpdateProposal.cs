using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Projects360;

/// <summary>
/// **جسر التنفيذ الهجين** (P360-WF-R2 §10): ادّعاء تنفيذٍ يرفعه المنفِّذ على مخرَج تعاقديّ،
/// يقرّه قائد الفريق أو يرفضه، فينتقل — عند الإقرار وحده — إلى رقم المخرَج ومنه إلى تقدّم
/// المشروع وصحّته.
///
/// <para>
/// **ما لم يُبنَ عمدًا**: ليست هذه مهامّ ولا مهامّ فرعيّة ولا تبعيّات ولا سجلّات عمل. المطلوب
/// إيصال ما نُفِّذ إلى المشروع **بالمعرّف لا بالاسم**، وبناء محرّك مهامّ كامل لهذا الغرض
/// كان سيُنشئ نظامًا ثانيًا للتشغيل بجوار التقارير القائمة بدل أن يصلها به.
/// </para>
///
/// <para>
/// **لماذا كيان مستقلّ ولا يُكتَب في `ReportSubmission`؟** التسليم دورة اعتماد أدائيّة لها
/// قواعدها وأصحابها؛ وحشر قرار تعاقديّ داخلها كان سيربط تقدّم المشروع بدورة لا تملكه. الكيان
/// المستقلّ يجعل الادّعاء قابلًا للمراجعة والرفض والتدقيق بلا أن يمسّ دورة التقارير بحرف.
/// </para>
///
/// <para>
/// **التعادليّة (Idempotency)** محفوظة بالحالة نفسها لا بعلَم منفصل: التطبيق مشروط بأن يكون
/// المقترح <see cref="ExecutionProposalStatus.Pending"/>، والقبول ينقله فورًا إلى
/// <see cref="ExecutionProposalStatus.Accepted"/> داخل نفس المعاملة ⟹ نداء القبول مرّتين لا
/// يضاعف أثرًا، ويعيد النتيجة نفسها.
/// </para>
/// </summary>
public class ProjectExecutionUpdateProposal : BaseEntity
{
    /// <summary>المشروع صاحب المخرَج — مخزَّن صراحةً كي تُرشَّح المقترحات بلا ضمّ.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>المخرَج التعاقديّ المُدَّعى تقدّمه. إلزاميّ: مقترحٌ بلا هدفٍ محدَّد لا يُطبَّق.</summary>
    public Guid DeliverableId { get; set; }
    public ProjectDeliverable? Deliverable { get; set; }

    /// <summary>
    /// التسليم الذي حمل الادّعاء — **اختياريّ**. المقترح قد يأتي من تقرير وقد يأتي مباشرةً من
    /// المنفِّذ، وإلزام التسليم كان سيمنع تسجيل تنفيذٍ واقعيّ لمجرّد أنّه لم يُوثَّق في تقرير.
    /// </summary>
    public Guid? SubmissionId { get; set; }

    /// <summary>من رفع الادّعاء.</summary>
    public Guid ProposedById { get; set; }

    /// <summary>النسبة المقترحة (0–100).</summary>
    public decimal ProposedProgressPercent { get; set; }

    /// <summary>الحالة المقترحة للمخرَج.</summary>
    public ProjectDeliverableStatus ProposedStatus { get; set; } = ProjectDeliverableStatus.InProgress;

    /// <summary>وصف ما نُفِّذ — دليل المراجِع، لا حقل تزيين.</summary>
    public string? ExecutionNote { get; set; }

    public ExecutionProposalStatus Status { get; set; } = ExecutionProposalStatus.Pending;

    public Guid? ReviewedById { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }

    /// <summary>سبب القبول أو الرفض. الرفض بلا سبب يترك المنفِّذ بلا ما يصحّحه.</summary>
    public string? ReviewNote { get; set; }

    /// <summary>
    /// النسبة التي كان عليها المخرَج **قبل** التطبيق — لقطة تجعل الأثر قابلًا للمراجعة
    /// وللعكس اليدويّ. تُملأ عند القبول وحده.
    /// </summary>
    public decimal? PreviousProgressPercent { get; set; }
}
