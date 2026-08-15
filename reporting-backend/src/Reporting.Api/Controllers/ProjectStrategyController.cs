using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Projects360;

namespace Reporting.Api.Controllers;

/// <summary>
/// استراتيجيّة المشروع ومخطَّطها (CPW-R3 · W5 · §8) — استراتيجيّة نشطة **واحدة** لكلّ مشروع.
///
/// <para>
/// **DEC-W4-02 — المخطَّط مُعطى بالبيانات**: <c>GET .../strategy/schema</c> يُرجِع النواة الثابتة
/// والحقول الشرطيّة المنطبقة على نوع خدمة المشروع وأقسام عرضها من الكتالوج. تبني الواجهة نموذجها
/// من هذه الاستجابة حصرًا — **بلا <c>if seo</c> ولا <c>if ads</c> ولا صنف استراتيجيّة لكلّ خدمة**،
/// وإضافة حقل أو قسم لاحقًا = صفّ كتالوج بلا كود ولا هجرة ولا تعديل واجهة.
/// </para>
/// </summary>
[Authorize]
[Route("api/projects/{projectId:guid}/strategy")]
public class ProjectStrategyController : ApiControllerBase
{
    private readonly IProjectStrategyService _service;

    public ProjectStrategyController(IProjectStrategyService service) => _service = service;

    /// <summary>الاستراتيجيّة النشطة — <c>null</c> داخل استجابة 200 إن لم تُنشأ بعد (لا 404).</summary>
    [HttpGet]
    public async Task<IActionResult> Get(Guid projectId, CancellationToken ct)
        => FromResult(await _service.GetAsync(projectId, ct));

    /// <summary>مخطَّط النموذج (نواة + حقول شرطيّة + أقسام) — قراءة متاحة لكلّ من يرى المشروع.</summary>
    [HttpGet("schema")]
    public async Task<IActionResult> Schema(Guid projectId, CancellationToken ct)
        => FromResult(await _service.GetSchemaAsync(projectId, ct));

    /// <summary>
    /// حفظ الاستراتيجيّة (إنشاء أو تحديث) بمزامنة تفاضليّة للسمات. كتابة **بنيويّة** ⟹ أدوار
    /// الإدارة حصرًا، ويُطبَّق الحكم داخل الخدمة بعد التحقّق من الرؤية.
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> Upsert(Guid projectId, [FromBody] UpsertProjectStrategyRequest request, CancellationToken ct)
        => FromResult(await _service.UpsertAsync(projectId, request, ct));
}
