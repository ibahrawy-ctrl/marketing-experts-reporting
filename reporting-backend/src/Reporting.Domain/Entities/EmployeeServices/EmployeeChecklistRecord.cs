using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.EmployeeServices;

/// <summary>
/// P2-HR-010 — سجلّ بند **يدويّ** في قائمة خدمة الموظّف والالتزام.
///
/// <para><b>هذا الجدول لا يُخزِّن أيّ بند مشتَقّ إطلاقًا.</b> بنود التقارير وKPI والحضور
/// والطلبات والملاحظات والخطط تُقرأ لحظيًّا من مصادرها في كلّ نداء ⇒ نسخها هنا كان سيُنتج
/// حقيقتين متنافستين، فتُصبح القائمة مصدرًا يناقض المصدر. الصفّ هنا لا يقوم إلّا حيث
/// **لا يوجد مصدر أصلًا** (توقيع عقد، إقرار سياسة، تسليم عهدة، إخلاء طرف).</para>
///
/// <para><see cref="ItemKey"/> مقيَّد بفهرس البنود اليدويّة المغلق في طبقة التطبيق؛
/// مفتاح خارجه يُرفَض عند الحافّة ولا يُكتَب.</para>
/// </summary>
public class EmployeeChecklistRecord : BaseEntity
{
    /// <summary>الموظّف صاحب البند.</summary>
    public Guid SubjectUserId { get; set; }

    /// <summary>مفتاح البند من الفهرس اليدويّ المغلق (مثل <c>employment-contract-signed</c>).</summary>
    public string ItemKey { get; set; } = string.Empty;

    public EmployeeChecklistStatus Status { get; set; } = EmployeeChecklistStatus.NotStarted;

    /// <summary>الموعد المستهدف — تاريخ محلّيّ (الرياض) لا لحظة عالميّة.</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>من يقع عليه الإنجاز. <c>null</c> ⇒ لم يُسنَد بعد.</summary>
    public Guid? OwnerUserId { get; set; }

    /// <summary>
    /// إشارة إلى الدليل (رقم مستند/مرجع أرشيف) — <b>إشارة لا نسخة</b>:
    /// لا يُخزَّن هنا محتوى مستند ولا مرفق، فالمرفقات لها مخزنها وضوابط وصولها.
    /// </summary>
    public string? EvidenceReference { get; set; }

    /// <summary>ملاحظة تنفيذيّة قصيرة. مصنَّفة <c>HrOnly</c> في طبقة العرض.</summary>
    public string? Note { get; set; }

    public DateTime? LastActionAtUtc { get; set; }
    public Guid? LastActionByUserId { get; set; }

    /// <summary>تفاؤليّة التزامن — تعديلان متزامنان على البند نفسه لا يدهس أحدهما الآخر صامتًا.</summary>
    public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString("N");
}
