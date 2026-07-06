using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Application.Courses;

namespace Reporting.Api.Controllers;

// قراءة الدورات النشطة لتغذية منتقي «الدورة» في قالب مبيعات B2C.
// متاحة لأي مستخدم مصادَق (الموظّف يحتاجها عند تعبئة تقريره). قراءة فقط، النشطة فقط.
[Authorize]
[Route("api/courses")]
public class CoursesController : ApiControllerBase
{
    private readonly ICourseService _service;

    public CoursesController(ICourseService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> ListActive(CancellationToken ct)
        => Ok(await _service.ListAsync(includeInactive: false, ct));
}
