using Reporting.Application.Common;

namespace Reporting.Application.Submissions;

/// <summary>
/// خدمة منح رؤية التقارير المخفيّ (REPORT-VIEW-GRANTS-R1). إدارة المنح (Admin فقط) + اشتقاق معرّفات
/// المُرسِلين المُصرَّح برؤية تقاريرهم للمستخدم الحالي. معزولة تمامًا: لا تدخل ScopeResolver/KPI/Dashboard/المشاريع؛
/// تُستهلك حصرًا في مسار قراءة التقارير لإضافة تقارير القراءة-فقط بحالات معتمدة (لا مسودّات/مُعادة للتعديل).
/// </summary>
public interface IReportViewGrantService
{
    /// <summary>قائمة كل المنح (Admin) — افتراضيًّا النشطة فقط، أو الكل مع المُلغاة عند includeRevoked.</summary>
    Task<Result<IReadOnlyList<ReportViewGrantDto>>> ListAsync(bool includeRevoked = false, CancellationToken ct = default);

    /// <summary>إنشاء منح جديد (Admin) — يتحقّق من النطاق والهدف ويمنع تكرار منح نشط (أو يعيد تفعيل مُلغًى).</summary>
    Task<Result<ReportViewGrantDto>> CreateAsync(CreateReportViewGrantRequest request, CancellationToken ct = default);

    /// <summary>إلغاء منح (soft) — IsActive=false + RevokedAtUtc، لا حذف صلب.</summary>
    Task<Result> RevokeAsync(Guid grantId, CancellationToken ct = default);

    /// <summary>المنح الفعّالة للمستخدم الحالي (لعرض ما يُتيحه له النظام من تقارير الآخرين).</summary>
    Task<Result<IReadOnlyList<ReportViewGrantDto>>> EffectiveForMeAsync(CancellationToken ct = default);

    /// <summary>
    /// معرّفات المُرسِلين الذين يحقّ للمستفيد رؤية تقاريرهم عبر المنح الفعّالة فقط
    /// (نطاق المستخدم ⇒ TargetUserId؛ نطاق الفريق ⇒ أعضاء الفريق الأساسيّون). تُستهلك داخل مسار القراءة فقط.
    /// لا تطبّق فلتر الحالة — تصفية الحالة (استبعاد المسودّة/المُعادة) مسؤولية مستهلك القراءة.
    /// </summary>
    Task<IReadOnlySet<Guid>> ResolveGrantedSubmitterIdsAsync(Guid granteeUserId, CancellationToken ct = default);
}
