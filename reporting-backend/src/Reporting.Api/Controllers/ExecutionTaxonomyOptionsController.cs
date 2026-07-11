using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.ExecutionTaxonomy;

namespace Reporting.Api.Controllers;

/// <summary>
/// RC-4 Task 4D3 — قراءة خيارات تصنيفات التنفيذ النشطة لملء التقارير (مصادقة فقط، لا إدارة).
/// منفصل عمدًا عن ExecutionTaxonomyController المحميّ بـ TemplateGovernance: أيّ موظّف مصادَق يقرأ
/// الخيارات النشطة لمجال واحد فقط (NameAr مرتّبة)، دون كشف أيّ عملية إدارة (إنشاء/تعطيل/معطّلة).
/// </summary>
[Authorize]
[Route("api/execution-taxonomy/options")]
public class ExecutionTaxonomyOptionsController : ApiControllerBase
{
    private readonly IExecutionTaxonomyService _service;
    public ExecutionTaxonomyOptionsController(IExecutionTaxonomyService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string domain, CancellationToken ct)
        => Ok(await _service.ListActiveOptionsAsync(domain, ct));

    // P1 — خيارات مُفصَّلة (Code + NameAr) للقوائم التي ترسل الرمز (نوع تيار العمل).
    [HttpGet("detailed")]
    public async Task<IActionResult> Detailed([FromQuery] string domain, CancellationToken ct)
        => Ok(await _service.ListActiveOptionDetailsAsync(domain, ct));
}
