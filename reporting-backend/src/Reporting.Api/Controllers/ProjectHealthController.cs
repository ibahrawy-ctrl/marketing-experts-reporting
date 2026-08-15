using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Projects360;

namespace Reporting.Api.Controllers;

/// <summary>
/// **إعادة احتساب صحّة المشروع** (CPW-R3 · R2 §6-6 · قرار المالك FINDING-W6-03).
///
/// <para>
/// **صفر منطق هنا**: المتحكّم قناة نقل فقط — التخويل والاحتساب والتخزين كلّها داخل
/// <see cref="IProjectHealthService"/>، ولا معادلة صحّة واحدة تُنسَخ إلى هذه الطبقة.
/// </para>
///
/// <para>
/// **صفر Policy جديدة**: <c>[Authorize]</c> وحدها، والبوّابة الحقيقيّة
/// <c>IProject360Authorization</c> كما في باقي مسارات Project 360. المشروع غير الموجود
/// والمشروع خارج نطاق المستخدم يعودان بعقد **واحد لا يُميَّز** (404 · FINDING-W6-04)،
/// بينما من يرى المشروع ولا يملك القدرة التشغيليّة يعود بـ403.
/// </para>
///
/// <para>**قراءة الصحّة** تتمّ عبر <c>GET /api/projects/{id}/overview</c> القائم — لا مسار قراءة منفصل.</para>
/// </summary>
[Authorize]
[Route("api/projects/{projectId:guid}/health")]
public class ProjectHealthController : ApiControllerBase
{
    private readonly IProjectHealthService _service;

    public ProjectHealthController(IProjectHealthService service) => _service = service;

    /// <summary>
    /// إعادة احتساب الصحّة وتخزين الأعمدة الثلاثة. **Idempotent وظيفيًّا**: مدخلات لم تتغيّر
    /// ⟹ نفس النتيجة ونفس الحالة. الأسباب تعود في الاستجابة **ولا تُخزَّن**.
    /// </summary>
    [HttpPost("recompute")]
    public async Task<IActionResult> Recompute(Guid projectId, CancellationToken ct)
        => FromResult(await _service.RecomputeAsync(projectId, ct));
}
