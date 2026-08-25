using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Reporting.Application.Common;
using Reporting.Application.Obligations;
using Reporting.Application.Security;

namespace Reporting.Api.Controllers;

/// <summary>
/// P2-HR-008 — سطح محرّك الالتزامات. مساران **جديدان** لا يستبدلان شيئًا:
/// <c>/api/obligations</c> (نطاقيّ) و<c>/api/obligations/me</c> (ذاتيّ).
///
/// <para><b>التخويل:</b> المسار النطاقيّ محكوم بسياسة صريحة
/// <see cref="Policies.HrOperationsView"/> المبنيّة على مطالبة <c>HrOperations.View</c> —
/// لا يمنحها أيّ دور ضمنًا ولا حتّى Admin. المسار الذاتيّ لا يحتاج مفتاحًا لأنّ موضوعه
/// يُشتقّ من التوكن حصرًا ولا يُقبَل من العميل ⇒ لا سطح تجاوز فيه أصلًا.</para>
///
/// <para><b>404 لا 403:</b> طلب موظّف خارج نطاق الرؤية يُرجِع «غير موجود» بنفس نصّ ورمز
/// الموظّف المعدوم، فلا يُستدلّ على وجوده. غياب المفتاح العامّ قبل تحديد أيّ مورد يبقى 403
/// عند البوّابة (لا كشف عن مورد بعينه) — وهو التمييز المعتمَد في P2-SEC-011.</para>
///
/// <para>علم <c>Phase2:HrOperationsEnabled</c> ليس تخويلًا؛ إطفاؤه يُخفي السطح كلّه (404).</para>
/// </summary>
[Authorize]
public class ObligationsController : ApiControllerBase
{
    private readonly IObligationsService _service;
    private readonly Phase2FeatureOptions _flags;

    public ObligationsController(IObligationsService service, IOptions<Phase2FeatureOptions> flags)
    {
        _service = service;
        _flags = flags.Value;
    }

    private bool Disabled => !_flags.HrOperationsEnabled;

    /// <summary>
    /// التزامات نطاق المُشاهِد. <c>userId</c> اختياريّ؛ خارج النطاق ⇒ 404.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.HrOperationsView)]
    public async Task<IActionResult> List([FromQuery] ObligationsFilter filter, CancellationToken ct)
    {
        if (Disabled) return NotFound();
        return FromResult(await _service.GetForScopeAsync(filter, ct));
    }

    /// <summary>
    /// التزامات المستخدم الحاليّ عن نفسه. حقّ أصيل لا يحتاج مفتاح HR:
    /// الموظّف يرى ما هو مطالَب به. أيّ <c>userId</c> في الاستعلام يُتجاهَل خادميًّا.
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> Mine([FromQuery] ObligationsFilter filter, CancellationToken ct)
    {
        if (Disabled) return NotFound();
        if (CurrentUserId == Guid.Empty) return Unauthorized();
        return FromResult(await _service.GetForSelfAsync(filter, ct));
    }
}
