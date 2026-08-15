using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Projects360;

/// <summary>
/// **المخرَج التعاقديّ (Contract Deliverable)** — CPW-R3 · D-03 · §5-7 — التزام متّفق عليه مع العميل.
///
/// <para>
/// **المصطلح رسميّ ومقفل — قرار المالك · ملحق W1-A بند 6**: في هذا النظام لا تُستعمل كلمة
/// <c>Deliverable</c> مجرَّدةً أبدًا — لا في كود، ولا في واجهة، ولا في تقرير، ولا في محادثة.
/// المصطلحان المعتمدان اثنان لا ثالث لهما:
/// <list type="bullet">
///   <item><description>**Contract Deliverable** = هذا الكيان <see cref="ProjectDeliverable"/> — ماذا **وعدنا العميل** بتسليمه.</description></item>
///   <item><description>**Workstream Deliverable** = الكيان القائم <c>WorkstreamDeliverable</c> — ماذا **سيُنتَج** داخل تيّار عمل.</description></item>
/// </list>
/// سبب التثبيت: الاسمان المختصران متطابقان بصريًّا، والخلط بينهما يخلط **الالتزام التعاقديّ**
/// بـ**التخطيط الإنتاجيّ**، وهو خلط لا يُكتشَف في المراجعة بل في الفاتورة.
/// </para>
///
/// <para>
/// **ما هو ليس عليه — حدّ فاصل مقصود ومُلزِم**:
/// هذا الكيان **ليس** مهمّة (Task)، و**ليس** تيّار عمل (Workstream)، و**ليس** مرحلة (Milestone)،
/// و**لا يرتبط بأيّ محرّك تنفيذ** لا اليوم ولا ضمنيًّا. هو تمثيل لالتزام تعاقديّ فقط.
/// </para>
///
/// <para>
/// **الفصل عن <c>WorkstreamDeliverable</c>**: ذاك كيان **تخطيط إنتاجيّ** أبوه <c>ProjectWorkstream</c>
/// (كم منشورًا/ريلًا سيُنتَج داخل تيّار عمل)، وهذا كيان **التزام تعاقديّ** أبوه المشروع مباشرة
/// (ماذا وعدنا العميل بتسليمه). الكيانان منفصلان تمامًا: لا وراثة، ولا مرجع، ولا مشاركة جدول،
/// و<c>WorkstreamDeliverable</c> **لم يُمَسّ بحرف** في CPW-R3.
/// </para>
///
/// <para>
/// **التوسّع المستقبليّ بلا إعادة تصميم**: حين تُبنى طبقة المهامّ لاحقًا يصبح التسلسل
/// <c>Deliverable ↓ Tasks ↓ SubTasks</c> بإضافة كيان مهمّة يحمل مفتاحًا لهذا الكيان — **إضافة بحتة**
/// بلا أيّ تعديل على الأعمدة هنا ولا على العلاقات القائمة.
/// </para>
/// </summary>
public class ProjectDeliverable : BaseEntity
{
    /// <summary>المشروع صاحب الالتزام (حذف تعاقبيّ Cascade).</summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// ربط **اختياريّ** بهدف. المخرَج التزام قائم بذاته (مثل «تقرير شهريّ») قد يخدم أهدافًا متعدّدة
    /// أو لا يخدم هدفًا بعينه؛ وإلزامه بهدف يفرض أهدافًا صوريّة. حين يوجد ربط يُفرَض حارس اتّساق
    /// <c>objective.ProjectId == deliverable.ProjectId</c> وإلّا <c>project_deliverable.objective_mismatch.conflict</c> (409).
    /// </summary>
    public Guid? ObjectiveId { get; set; }
    public ProjectObjective? Objective { get; set; }

    /// <summary>
    /// جسر **اختياريّ** إلى تيّار العمل المنفِّذ — مرجع عرضٍ فقط، لا تبعيّة ولا تغيير على تيّار العمل.
    /// يخضع لنفس حارس الاتّساق مقابل <c>workstream.ProjectId</c>.
    /// </summary>
    public Guid? WorkstreamId { get; set; }

    /// <summary>
    /// رمز نوع المخرَج من كتالوج التصنيفات (Domain=<c>contract_deliverable</c>) — نصّ فقط بلا مفتاح أجنبيّ،
    /// و**لقطة ثابتة لا تتغيّر بعد الإنشاء** (نفس قاعدة <c>WorkstreamDeliverable</c>).
    /// </summary>
    public string DeliverableTypeCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>الكمّيّة المتعاقَد عليها.</summary>
    public int PlannedQuantity { get; set; } = 1;

    /// <summary>الكمّيّة المُسلَّمة فعلًا.</summary>
    public int CompletedQuantity { get; set; }

    /// <summary>الحالة — يدويّة بالكامل؛ <see cref="ProjectDeliverableStatus.Delayed"/> تُعلَن ولا تُشتقّ من التاريخ.</summary>
    public ProjectDeliverableStatus Status { get; set; } = ProjectDeliverableStatus.NotStarted;

    /// <summary>
    /// نسبة الإنجاز المعلَنة **يدويًّا**. منفصلة عمدًا عن <see cref="CompletedQuantity"/> لأنّ الكمّيّة
    /// قد لا تصف التقدّم (خطّة محتوى واحدة قد تكون 60% منجَزة). لا اشتقاق آليّ بينهما — Manual-First.
    /// </summary>
    public decimal ProgressPercent { get; set; }

    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }

    /// <summary>ختم التسليم الفعليّ.</summary>
    public DateTime? DeliveredAtUtc { get; set; }

    /// <summary>الأولويّة — يُعاد استخدام التعداد القائم <see cref="DeliverablePriority"/> (صفر تعداد جديد).</summary>
    public DeliverablePriority Priority { get; set; } = DeliverablePriority.Medium;

    /// <summary>المسؤول عن المخرَج (مرجع مستخدم اختياريّ بلا مفتاح أجنبيّ صلب).</summary>
    public Guid? OwnerUserId { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// ترتيب العرض المعلَن (تصاعديّ) — **قرار المالك · ملحق W1-A بند 2**.
    /// ترتيب المخرجات التعاقديّة في العرض يجب أن يطابق ترتيبها في العقد أمام العميل،
    /// وهذا لا يُشتقّ من تاريخ الإدخال ولا من الاسم.
    /// الاسم التقنيّ موحَّد على اصطلاح <c>SortOrder</c> القائم؛ و«ترتيب العرض» تسمية الواجهة فقط.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>التعطيل بدل الحذف. BaseEntity لا يحمل IsActive فيُعرَّف هنا.</summary>
    public bool IsActive { get; set; } = true;
}
