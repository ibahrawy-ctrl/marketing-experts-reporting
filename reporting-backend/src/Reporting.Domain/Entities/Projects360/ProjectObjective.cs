using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Projects360;

/// <summary>
/// هدف من أهداف المشروع (CPW-R3 · D-02 · §5-4) — **الأب الإلزاميّ لكلّ مؤشّر**.
///
/// <para>
/// **حالة يدويّة بالكامل**: <see cref="Status"/> و<see cref="Weight"/> يُعلَنان يدويًّا ولا يُشتقّان
/// من أيّ محرّك مهامّ. لا يوجد — ولا يُفترَض وجود — أيّ ارتباط بطبقة تنفيذ.
/// </para>
///
/// <para>
/// **قرار الأوزان**: مجموع الأوزان **لا يُفرَض** أن يساوي 100؛ الاحتساب يُطبِّع تلقائيًّا <c>w_i ÷ Σw</c>.
/// السبب: فرض المجموع يجعل إضافة هدف واحد عمليّة تحرير جماعيّ إجباريّ لكلّ الأهداف القائمة.
/// إذا كانت كلّ الأوزان صفرًا ⟶ يُعامَل الجميع بوزن متساوٍ.
/// </para>
///
/// <para>
/// **حارس مكافحة اليُتم (Anti-Orphan)**: بما أنّ المؤشّر لا يجوز أن يوجد بلا هدف، فحذف هدف
/// يحمل مؤشّرات يُرفَض بـ<c>project_objective.has_kpis.conflict</c> (409) في طبقة الخدمة.
/// الحذف الناعم (<see cref="IsActive"/> = false) متاح دائمًا بلا قيد.
/// </para>
/// </summary>
public class ProjectObjective : BaseEntity
{
    /// <summary>المشروع صاحب الهدف (حذف تعاقبيّ Cascade).</summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// جسر **اختياريّ** إلى تيّار عمل قائم — لا إلزام، ولا تغيير بحرف على <c>ProjectWorkstream</c>.
    /// وجوده لا يجعل الهدف تابعًا لطبقة التنفيذ؛ هو مرجع عرضٍ فقط.
    /// </summary>
    public Guid? WorkstreamId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>الأولويّة — يُعاد استخدام التعداد القائم <see cref="DeliverablePriority"/> (صفر تعداد جديد).</summary>
    public DeliverablePriority Priority { get; set; } = DeliverablePriority.Medium;

    /// <summary>
    /// وزن الهدف في احتساب صحّة المشروع. لا يُفرَض مجموع محدَّد — يُطبَّع وقت الاحتساب.
    ///
    /// <para>
    /// **قرار المالك · ملحق W1-A بند 4 — الصحّة موزونة لا متوسّطة**: الوزن مُنشأ من اليوم حتى لو
    /// بقيت كلّ القيم صفرًا في المرحلة الأولى. كلّها صفر ⟹ توزيع متساوٍ (يُحاكي المتوسّط تمامًا)،
    /// فأوّل مدير يُدخِل 40/30/30 يحصل على صحّة موزونة **بلا هجرة وبلا تغيير معادلة**.
    /// </para>
    /// </summary>
    public decimal Weight { get; set; }

    public ProjectObjectiveStatus Status { get; set; } = ProjectObjectiveStatus.NotStarted;

    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }

    /// <summary>مالك الهدف (مرجع مستخدم اختياريّ بلا مفتاح أجنبيّ صلب).</summary>
    public Guid? OwnerUserId { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// ترتيب العرض المعلَن (تصاعديّ) — **قرار المالك · ملحق W1-A بند 2**.
    ///
    /// <para>
    /// **لماذا من البداية لا لاحقًا؟** ترتيب الأهداف معنًى إداريّ لا تفصيل عرض: «الهدف الأوّل»
    /// عبارة يقولها المدير ويقصدها. الترتيب البديل (الاسم/تاريخ الإنشاء) يفرض ترتيبًا لم يقصده أحد.
    /// وإضافته اليوم عمود واحد بلا هجرة إضافيّة، بينما إضافته بعد التشغيل هجرة + Backfill لكلّ صفّ.
    /// </para>
    ///
    /// <para>
    /// **ليس Drag &amp; Drop**: هذا حقل تخزين جاهز للترتيب اليدويّ متى طُلب؛ لا واجهة سحب اليوم،
    /// ولا يمنع التصميم إضافتها لاحقًا لأنّها ستكتب في هذا العمود نفسه بلا تغيير بنية.
    /// </para>
    ///
    /// <para>
    /// **التسمية — قرار المالك المُقفَل**: الاسم التقنيّ موحَّد على اصطلاح <c>SortOrder</c> القائم
    /// في النظام (Service · ExecutionTaxonomyValue · ClientContact · ProjectWorkstream وغيرها)،
    /// فلا يُخلَق اسم ثانٍ لنفس المفهوم في الدومين وقاعدة البيانات وواجهة الـAPI.
    /// أمّا «ترتيب العرض» فهو تسمية العرض للمستخدم في الواجهة فقط.
    /// </para>
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>التعطيل بدل الحذف. BaseEntity لا يحمل IsActive فيُعرَّف هنا.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>المؤشّرات التابعة لهذا الهدف (تبعيّة إلزاميّة — D-02).</summary>
    public ICollection<ProjectKpi> Kpis { get; set; } = new List<ProjectKpi>();
}
