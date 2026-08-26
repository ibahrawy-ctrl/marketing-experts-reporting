using Reporting.Application.Common;

namespace Reporting.Application.HrOperations;

/// <summary>
/// P2-HR-009 — لوحة عمليّات الموارد البشريّة وطوابير الإجراءات.
///
/// <para><b>ضوابط مُلزِمة على أيّ تنفيذ:</b></para>
/// <list type="bullet">
/// <item><b>البطاقة لا تخالف تفصيلها بنيويًّا</b>: عدد البطاقة = عدد صفوف طابورها تحت المرشِّح نفسه،
/// محسوبًا من المجموعة ذاتها لا من استعلام عدّ مستقلّ.</item>
/// <item><b>لا رقم خارج النطاق</b>: كلّ عدّ وكلّ صفّ محصوران بنطاق المُشاهِد المحسوب خادميًّا.
/// موظّف مطلوب خارج النطاق ⇒ «غير موجود» (404) لا 403.</item>
/// <item><b>لا إعادة حساب</b>: المطلوب/الناقص/المتأخّر يأتي من <c>IObligationsService</c> وحده.</item>
/// <item><b>التصدير صلاحيّة منفصلة</b> عن الرؤية، ويُدقَّق في <c>AuditLog</c> في كلّ مرّة.</item>
/// </list>
/// </summary>
public interface IHrOperationsService
{
    /// <summary>بطاقات الطوابير الأحد عشر داخل نطاق المُشاهِد.</summary>
    Task<Result<HrOperationsDashboardDto>> GetDashboardAsync(
        HrOperationsFilter filter, CancellationToken ct = default);

    /// <summary>تفصيل طابور واحد (Drill-down) مُصفَّحًا.</summary>
    Task<Result<HrOperationsQueueDto>> GetQueueAsync(
        HrOperationsQueue queue, HrOperationsFilter filter, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// تصدير طابور واحد. <b>لا يمرّ من هنا صفّ لم يكن ليظهر في التفصيل</b> — نفس المصدر ونفس النطاق.
    /// التدقيق مسؤوليّة المتّصل عند الحافّة (حيث يُعرَف عنوان الطلب).
    /// </summary>
    Task<Result<HrOperationsExportDto>> ExportQueueAsync(
        HrOperationsQueue queue, HrOperationsFilter filter, CancellationToken ct = default);
}
