using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Reporting.Application.Audit;
using Reporting.Application.Checklist;
using Reporting.Application.Common;
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
    private readonly IEmployeeChecklistService _checklist;
    private readonly IAuditService _audit;
    private readonly Phase2FeatureOptions _flags;

    public EmployeesController(
        IEmployee360Service service,
        IEmployeeChecklistService checklist,
        IAuditService audit,
        IOptions<Phase2FeatureOptions> flags)
    {
        _service = service;
        _checklist = checklist;
        _audit = audit;
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

    // ===== P2-HR-010 — قائمة خدمة الموظّف والالتزام =====

    /// <summary>
    /// قائمة الالتزام لموظّف بعينه. القراءة محكومة بالنطاق وحسّاسيّة كلّ بند وحدهما —
    /// **لا** بمفتاح <c>EmployeeChecklist.Manage</c>؛ فمن يرى بندًا لا يحقّ له بالضرورة إغلاقه.
    /// خارج النطاق يُرجِع 404 لا 403 كي لا يتسرّب وجود الموظّف.
    /// </summary>
    [HttpGet("{userId:guid}/checklist")]
    public async Task<IActionResult> Checklist([FromRoute] Guid userId, CancellationToken ct)
    {
        if (!_flags.EmployeeChecklistEnabled) return NotFound();
        return FromResult(await _checklist.GetAsync(userId, ct));
    }

    /// <summary>قائمة التزام المستخدم الحاليّ — يُحَلّ المعرّف خادميًّا لا من الواجهة.</summary>
    [HttpGet("me/checklist")]
    public async Task<IActionResult> MyChecklist(CancellationToken ct)
    {
        if (!_flags.EmployeeChecklistEnabled) return NotFound();
        if (CurrentUserId == Guid.Empty) return Unauthorized();
        return FromResult(await _checklist.GetForSelfAsync(ct));
    }

    /// <summary>
    /// تحرير بند **يدويّ** واحد. البنود المحسوبة غير قابلة للكتابة إطلاقًا (تُرفَض بـ400)،
    /// إذ لا صفَّ لها في أيّ جدول ومصدرها الوحيد هو مصدرها الأصليّ.
    /// </summary>
    [HttpPut("{userId:guid}/checklist/{itemKey}")]
    [Authorize(Policy = Policies.EmployeeChecklistManage)]
    public async Task<IActionResult> UpdateChecklistItem(
        [FromRoute] Guid userId,
        [FromRoute] string itemKey,
        [FromBody] UpdateChecklistItemCommand command,
        CancellationToken ct)
    {
        if (!_flags.EmployeeChecklistEnabled) return NotFound();

        var result = await _checklist.UpdateManualItemAsync(userId, itemKey, command, ct);
        if (result.Succeeded)
        {
            await _audit.LogAsync(
                CurrentUserId == Guid.Empty ? null : CurrentUserId,
                "EmployeeChecklist.Update",
                nameof(Reporting.Domain.Entities.EmployeeServices.EmployeeChecklistRecord),
                userId,
                JsonSerializer.Serialize(new
                {
                    subjectUserId = userId,
                    itemKey,
                    status = result.Value!.Status.ToString(),
                    dueDate = result.Value.DueDate,
                    ownerUserId = result.Value.OwnerUserId
                }),
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                ct);
        }

        return FromResult(result);
    }
}
