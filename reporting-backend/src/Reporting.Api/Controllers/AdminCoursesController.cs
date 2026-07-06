using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Common;
using Reporting.Application.Courses;

namespace Reporting.Api.Controllers;

// إدارة كتالوج الدورات (المصدر الرسمي لأسماء دورات مبيعات B2C).
// الكتابة عبر سياسة حوكمة القوالب (Admin/CEO/GM) لأن الكتالوج يغذّي قالب مبيعات B2C.
// إضافة بحتة — لا تمسّ التقارير/القوالب القائمة.
[Authorize(Policy = Policies.TemplateGovernance)]
[Route("api/admin/courses")]
public class AdminCoursesController : ApiControllerBase
{
    private readonly ICourseService _service;

    public AdminCoursesController(ICourseService service) => _service = service;

    // كل الدورات (نشطة ومعطّلة) لشاشة الإدارة.
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _service.ListAsync(includeInactive: true, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => FromResult(await _service.GetAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCourseRequest req, CancellationToken ct)
        => FromResult(await _service.CreateAsync(req, CurrentUserId, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCourseRequest req, CancellationToken ct)
        => FromResult(await _service.UpdateAsync(id, req, CurrentUserId, ct));

    [HttpPatch("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
        => FromResult(await _service.SetActiveAsync(id, true, CurrentUserId, ct));

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
        => FromResult(await _service.SetActiveAsync(id, false, CurrentUserId, ct));

    // حذف آمن: نهائيّ إن لم تُستخدَم، وإلّا أرشفة (تعطيل) — التقارير القديمة تبقى صالحة في الحالتين.
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => FromResult(await _service.DeleteAsync(id, CurrentUserId, ct));
}
