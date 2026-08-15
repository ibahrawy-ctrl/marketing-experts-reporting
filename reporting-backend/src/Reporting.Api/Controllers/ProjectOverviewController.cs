using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Projects360;

namespace Reporting.Api.Controllers;

/// <summary>
/// لوحة النظرة العامّة للمشروع (CPW-R3 · W5 · §6).
///
/// <para>
/// **صفر Policy جديدة**: <c>[Authorize]</c> وحدها هنا، والتخويل الحقيقيّ يقع في
/// <c>IProject360Authorization</c> داخل طبقة الخدمة — فالرؤية مصدرها الوحيد
/// <c>IClientProjectAccess</c> القائم، ولم يُنشأ دور ولا صلاحيّة ولا Policy في W5.
/// </para>
/// </summary>
[Authorize]
[Route("api/projects/{projectId:guid}/overview")]
public class ProjectOverviewController : ApiControllerBase
{
    private readonly IProjectOverviewService _service;

    public ProjectOverviewController(IProjectOverviewService service) => _service = service;

    /// <summary>
    /// اللوحة كاملة في **رحلة واحدة بعدد استعلامات ثابت** لا يتغيّر بعدد الأهداف أو المؤشّرات (§7).
    /// نتيجة المؤشّرات فيها **موزونة على مستويين** (DEC-W4-03) لا متوسّطًا مسطَّحًا.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(Guid projectId, CancellationToken ct)
        => FromResult(await _service.GetProjectOverviewAsync(projectId, ct));
}
