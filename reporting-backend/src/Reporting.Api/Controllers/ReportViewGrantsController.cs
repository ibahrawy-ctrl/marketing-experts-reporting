using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Common;
using Reporting.Application.Submissions;

namespace Reporting.Api.Controllers;

/// <summary>
/// منح رؤية التقارير المخفيّ (REPORT-VIEW-GRANTS-R1). إدارة المنح للأدمن فقط (Policies.AdminOnly).
/// معزولة تمامًا: تُستهلك فقط في مسار قراءة التقارير (SubmissionService) ولا تمسّ
/// ScopeResolver/KPI/Dashboard/المشاريع/العملاء/عضوية الفِرق. نقطة "effective/me" مفتوحة لأي مستخدم
/// مصادَق ليرى ما أُتيح له من تقارير الآخرين (عرض فقط).
/// </summary>
[Authorize]
[Route("api/report-view-grants")]
public class ReportViewGrantsController : ApiControllerBase
{
    private readonly IReportViewGrantService _service;

    public ReportViewGrantsController(IReportViewGrantService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> List([FromQuery] bool includeRevoked = false, CancellationToken ct = default)
        => FromResult(await _service.ListAsync(includeRevoked, ct));

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Create([FromBody] CreateReportViewGrantRequest req, CancellationToken ct)
        => FromResult(await _service.CreateAsync(req, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
        => FromResult(await _service.RevokeAsync(id, ct));

    [HttpGet("effective/me")]
    public async Task<IActionResult> EffectiveForMe(CancellationToken ct)
        => FromResult(await _service.EffectiveForMeAsync(ct));
}
