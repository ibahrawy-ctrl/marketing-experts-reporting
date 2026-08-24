using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Reporting.Application.Employee360;
using Reporting.Application.Security;

namespace Reporting.Api.Controllers;

/// <summary>
/// P2-EMP-002 — سطح Employee 360. مسارات **جديدة** لا تُلغي ولا تُعيد تسمية أيّ مسار قائم؛
/// نقطة <c>/api/dashboard/employee-profile/{userId}</c> تبقى عاملة كما هي.
/// </summary>
[Authorize]
public class EmployeesController : ApiControllerBase
{
    private readonly IEmployee360Service _service;
    private readonly Phase2FeatureOptions _flags;

    public EmployeesController(IEmployee360Service service, IOptions<Phase2FeatureOptions> flags)
    {
        _service = service;
        _flags = flags.Value;
    }

    /// <summary>عرض الموظّف 360 لموظّف بعينه — خارج النطاق يُرجِع 404 لا 403.</summary>
    [HttpGet("{userId:guid}/profile-360")]
    public async Task<IActionResult> Profile360(
        [FromRoute] Guid userId,
        [FromQuery] string? sections,
        [FromQuery] string? period,
        CancellationToken ct)
    {
        if (!_flags.Employee360Enabled) return NotFound();
        return FromResult(await _service.GetProfileAsync(userId, sections, period, ct));
    }

    /// <summary>
    /// اسم بديل ذاتيّ يُحَلّ خادميًّا. لا يستبدل المسار القائم على المعرّف، بل يُضاف إليه،
    /// كي لا تحتاج الواجهة إلى معرفة معرّف المستخدم مسبقًا.
    /// </summary>
    [HttpGet("me/profile-360")]
    public async Task<IActionResult> MyProfile360(
        [FromQuery] string? sections,
        [FromQuery] string? period,
        CancellationToken ct)
    {
        if (!_flags.Employee360Enabled) return NotFound();
        if (CurrentUserId == Guid.Empty) return Unauthorized();
        return FromResult(await _service.GetProfileAsync(CurrentUserId, sections, period, ct));
    }
}
