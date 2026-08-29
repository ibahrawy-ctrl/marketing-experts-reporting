using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Common;
using Reporting.Application.Kpi;
using Reporting.Application.Periods;
using Reporting.Domain.Enums;

namespace Reporting.Api.Controllers;

/// <summary>
/// P1-KPI-004 — العقد التنظيميّ الموحّد لتحليلات KPI (v2).
///
/// كل نقطة نهاية هنا:
/// <list type="bullet">
/// <item>لها <b>سياسة صريحة</b> (<see cref="Policies.KpiAnalyticsView"/>) لا وراثة ضمنيّة من المتحكّم وحده.</item>
/// <item>تمرّ عبر <see cref="IKpiCalculationService"/> حصرًا — لا استعلام مباشر على <c>KpiEvaluations</c> هنا.</item>
/// <item>نطاقها مفروض خادميًّا داخل الخدمة عبر <c>IScopeResolver</c>؛ ما يُرسله العميل من
/// <c>teamId</c>/<c>departmentId</c>/<c>subjectUserId</c> هو <b>تضييق</b> فقط ولا يوسّع الرؤية أبدًا.</item>
/// <item>الطلب على مورد خارج النطاق يعود <b>404</b> (رمز <c>kpi.not_found</c>) لا 403 — فلا يُسرَّب وجوده.</item>
/// <item>الكادنس <b>إلزاميّ</b> (B-3): غيابه خطأ صريح لا سقوط صامت إلى النبض الأسبوعيّ.</item>
/// </list>
///
/// الإخفاء في الواجهة ليس طبقة حماية: كلّ ما سبق يُفرَض هنا وفي الخدمة بصرف النظر عن أيّ علم أو شاشة.
/// </summary>
[Authorize(Policy = Policies.KpiAnalyticsView)]
[Route("api/kpi")]
public class KpiAnalyticsController : ApiControllerBase
{
    private readonly IKpiCalculationService _kpi;
    private readonly IPeriodService _periods;

    public KpiAnalyticsController(IKpiCalculationService kpi, IPeriodService periods)
    {
        _kpi = kpi;
        _periods = periods;
    }

    /// <summary>
    /// الأداء التنظيميّ: شركة + إدارات + فرق + موظّفون، كلّها بنفس الفترة والكادنس والنطاق،
    /// وكلّ رقم يحمل تغطيته وجودة بياناته واتّجاهه (§5.5).
    /// </summary>
    [HttpGet("performance")]
    [Authorize(Policy = Policies.KpiAnalyticsView)]
    public async Task<IActionResult> Performance([FromQuery] KpiAnalyticsRequest request, CancellationToken ct)
        => FromResult(await _kpi.GetPerformanceAsync(request.ToQuery(), ct));

    /// <summary>
    /// الأفضل أداءً / المحتاجون للدعم — صفّ واحد لكلّ موظّف، بعد استبعاد ضعيفي التغطية (B-5)،
    /// وبكسر تعادل مستقرّ. عدد المستبعَدين يُعاد صراحةً (شفافيّة لا إخفاء).
    /// </summary>
    [HttpGet("rankings")]
    [Authorize(Policy = Policies.KpiAnalyticsView)]
    public async Task<IActionResult> Rankings(
        [FromQuery] KpiAnalyticsRequest request, [FromQuery] int take, CancellationToken ct)
        => FromResult(await _kpi.GetRankingsAsync(request.ToQuery(), take <= 0 ? 5 : take, ct));

    /// <summary>
    /// تفصيل الرقم إلى صفوف التقييمات التي بنته، بنفس النطاق والسياسة، مع إعادة حساب المتوسّط
    /// من الصفوف نفسها — فيمكن للمستخدم إعادة إنتاج الرقم يدويًّا.
    /// </summary>
    [HttpGet("drilldown")]
    [Authorize(Policy = Policies.KpiAnalyticsView)]
    public async Task<IActionResult> Drilldown([FromQuery] KpiAnalyticsRequest request, CancellationToken ct)
        => FromResult(await _kpi.GetDrilldownAsync(request.ToQuery(), ct));

    /// <summary>
    /// P1-KPI-002 — حلّ حدود الفترة خادميًّا بتوقيت <c>Asia/Riyadh</c>. الواجهة تستهلك هذه الحدود
    /// ولا تشتقّها بـUTC ولا بتوقيت المتصفّح (B-1). قراءة خالصة: لا تمسّ أيّ <c>PeriodKey</c> مخزَّن.
    /// </summary>
    [HttpGet("periods/resolve")]
    [Authorize(Policy = Policies.KpiAnalyticsView)]
    public IActionResult ResolvePeriod(
        [FromQuery] string? type, [FromQuery] string? periodKey,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var resolved = _periods.Resolve(new PeriodRequest(
            string.IsNullOrWhiteSpace(type) ? PeriodKinds.CurrentQuarter : type, periodKey, from, to));
        if (!resolved.Succeeded) return ToProblem(resolved);

        var period = resolved.Value!;
        return Ok(new
        {
            Current = KpiPeriodResolvedDto.From(period),
            Previous = KpiPeriodResolvedDto.From(_periods.PreviousComparable(period)),
            WeekKeys = _periods.WeekKeysWithin(period)
        });
    }
}

/// <summary>
/// مُدخَل الاستعلام المشترك لنقاط نهاية تحليلات KPI.
/// <see cref="Cadence"/> بلا قيمة افتراضيّة عمدًا (DEC-01/2): غيابه ⇒ يحسم الخادم تواتر كلّ موظّف
/// من قالبه الفعّال (DEC-01/5)، ووجوده ⇒ مسار صريح واحد (مثلًا «النبض الأسبوعيّ» — DEC-01/3).
/// لا سقوط صامت في الحالتين.
/// </summary>
public sealed class KpiAnalyticsRequest
{
    /// <summary>
    /// Week | Month | Quarter | Year | Custom | LastCompletedWeek | CurrentQuarter.
    /// الافتراضيّ <b>الربع الجاري</b> بتوقيت Asia/Riyadh (DEC-01/1) — لا يختار المستخدم الفترة الجارية.
    /// </summary>
    public string? PeriodType { get; init; }

    public KpiCadence? Cadence { get; init; }
    public string? PeriodKey { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }

    /// <summary>تضييق داخل النطاق المصرَّح به فقط — لا يوسّع الرؤية إطلاقًا.</summary>
    public Guid? DepartmentId { get; init; }

    public Guid? TeamId { get; init; }
    public Guid? SubjectUserId { get; init; }

    public KpiAnalyticsQuery ToQuery() => new(
        string.IsNullOrWhiteSpace(PeriodType) ? PeriodKinds.CurrentQuarter : PeriodType,
        Cadence, PeriodKey, From, To, DepartmentId, TeamId, SubjectUserId);
}
