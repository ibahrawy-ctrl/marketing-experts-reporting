using Reporting.Domain.Common;

namespace Reporting.Domain.Entities.Projects360;

/// <summary>
/// قراءة مؤرَّخة لمؤشّر مشروع (CPW-R3 · §5-6) — السجلّ التاريخيّ الذي تُبنى عليه الاتّجاهات.
///
/// <para>
/// **الفهرس الفريد** <c>(ProjectKpiId, ReadingDate)</c>: قراءة واحدة لكلّ تاريخ لكلّ مؤشّر،
/// وإلّا <c>project_kpi_reading.duplicate_date.conflict</c> (409). التصحيح يتمّ بتحديث القراءة
/// لا بإضافة ثانية — فلا تتضاعف نقاط المنحنى.
/// </para>
///
/// <para>
/// **لماذا اللقطتان <see cref="TargetSnapshot"/> و<see cref="AchievementSnapshot"/>؟**
/// لأنّ هدف المؤشّر قابل للتعديل لاحقًا؛ وبدون اللقطة يُعيد تعديلُ الهدف كتابةَ التاريخ بأثر رجعيّ
/// فيبدو أداء الماضي مختلفًا عمّا اعتُمِد فعلًا. هذا **نفس مبدأ لقطة نسخة القالب**
/// (<c>KpiTemplateVersionId</c>) في منظومة مؤشّرات الموظّفين القائمة — اتّساق معماريّ لا اختراع جديد.
/// </para>
/// </summary>
public class ProjectKpiReading : BaseEntity
{
    /// <summary>المؤشّر صاحب القراءة (حذف تعاقبيّ Cascade).</summary>
    public Guid ProjectKpiId { get; set; }
    public ProjectKpi? ProjectKpi { get; set; }

    /// <summary>تاريخ القراءة.</summary>
    public DateOnly ReadingDate { get; set; }

    /// <summary>القيمة المرصودة.</summary>
    public decimal Value { get; set; }

    /// <summary>لقطة القيمة المستهدَفة وقت القراءة — تحفظ التاريخ من التعديل الرجعيّ.</summary>
    public decimal? TargetSnapshot { get; set; }

    /// <summary>لقطة نسبة الإنجاز المحتسَبة وقت القراءة.</summary>
    public decimal? AchievementSnapshot { get; set; }

    /// <summary>من سجّل القراءة (مساءلة). مرجع مستخدم بلا مفتاح أجنبيّ صلب.</summary>
    public Guid RecordedByUserId { get; set; }

    public string? Notes { get; set; }
}
