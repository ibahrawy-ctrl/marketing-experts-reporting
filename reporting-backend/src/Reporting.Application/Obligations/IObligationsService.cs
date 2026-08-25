using Reporting.Application.Common;

namespace Reporting.Application.Obligations;

/// <summary>
/// P2-HR-008 — <b>المصدر الخادميّ الوحيد</b> لاشتقاق الالتزامات (مطلوب/مُنجَز/ناقص/متأخّر).
/// <para>
/// أيّ سطح آخر (HR Operations، Employee 360، قائمة التحقّق، الواجهة) <b>يستهلك</b> هذه الخدمة
/// ولا يعيد حساب النقص أو التأخّر بنفسه. لا جدول موازٍ للنتائج: الاشتقاق لحظيّ من مصادره
/// الأصليّة في كلّ نداء.
/// </para>
/// </summary>
public interface IObligationsService
{
    /// <summary>
    /// الاشتقاق الخام لمجموعة معرّفات ودورات. <b>لا يتحقّق من التخويل</b> — المتّصل مسؤول عن
    /// فرض النطاق قبل النداء. مخصَّص للاستهلاك الداخليّ من خدمات المرحلة الثانية.
    /// </summary>
    Task<IReadOnlyList<ObligationDto>> ComputeAsync(ObligationQuery query, CancellationToken ct = default);

    /// <summary>
    /// نقطة النهاية النطاقيّة: تحسب نطاق المُشاهِد ثمّ تشتقّ داخله.
    /// موظّف مطلوب خارج النطاق ⇒ فشل بسبب «غير موجود» (تترجمه الحافّة إلى 404 لا 403).
    /// </summary>
    Task<Result<ObligationsResultDto>> GetForScopeAsync(ObligationsFilter filter, CancellationToken ct = default);

    /// <summary>التزامات المستخدم الحاليّ عن نفسه — المعرّف من التوكن حصرًا ولا يُقبَل من العميل.</summary>
    Task<Result<ObligationsResultDto>> GetForSelfAsync(ObligationsFilter filter, CancellationToken ct = default);
}
