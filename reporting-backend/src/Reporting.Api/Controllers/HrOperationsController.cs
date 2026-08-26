using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.HrOperations;
using Reporting.Application.Security;

namespace Reporting.Api.Controllers;

/// <summary>
/// P2-HR-009 — سطح لوحة عمليّات الموارد البشريّة وطوابير الإجراءات.
///
/// <para><b>مفتاحان منفصلان لا واحد:</b> الرؤية بـ<see cref="Policies.HrOperationsView"/>
/// والتصدير بـ<see cref="Policies.HrOperationsExport"/>. من يرى اللوحة لا يُصدِّرها تلقائيًّا —
/// التصدير إخراج للبيانات خارج النظام فيلزمه قرار منح مستقلّ.</para>
///
/// <para><b>404 لا 403 عند مغادرة النطاق:</b> طابور غير معروف، أو موظّف خارج نطاق الرؤية،
/// يُرجِعان «غير موجود» بنفس النصّ فلا يُستدلّ على وجود شيء. غياب المفتاح العامّ قبل تحديد
/// أيّ مورد يبقى 403 عند البوّابة — لا كشف فيه عن مورد بعينه.</para>
///
/// <para><b>كلّ تصدير مُدقَّق:</b> يُكتب في <c>AuditLog</c> قبل تسليم البايتات، بالطابور
/// والمرشِّح وعدد الصفوف وعنوان الطلب. التدقيق هنا لا في الخدمة لأنّ العنوان لا يُعرَف إلّا عند الحافّة.</para>
///
/// <para>علم <c>Phase2:HrOperationsEnabled</c> ليس تخويلًا؛ إطفاؤه يُخفي السطح كلّه (404).</para>
/// </summary>
[Authorize]
[Route("api/hr-operations")]
public class HrOperationsController : ApiControllerBase
{
    private readonly IHrOperationsService _service;
    private readonly IAuditService _audit;
    private readonly Phase2FeatureOptions _flags;

    public HrOperationsController(
        IHrOperationsService service, IAuditService audit, IOptions<Phase2FeatureOptions> flags)
    {
        _service = service;
        _audit = audit;
        _flags = flags.Value;
    }

    private bool Disabled => !_flags.HrOperationsEnabled;

    /// <summary>بطاقات الطوابير الأحد عشر داخل نطاق المُشاهِد.</summary>
    [HttpGet("dashboard")]
    [Authorize(Policy = Policies.HrOperationsView)]
    public async Task<IActionResult> Dashboard([FromQuery] HrOperationsFilter filter, CancellationToken ct)
    {
        if (Disabled) return NotFound();
        return FromResult(await _service.GetDashboardAsync(filter, ct));
    }

    /// <summary>تفصيل طابور واحد مُصفَّحًا — عدده هو عين عدد بطاقته تحت المرشِّح نفسه.</summary>
    [HttpGet("queues/{key}")]
    [Authorize(Policy = Policies.HrOperationsView)]
    public async Task<IActionResult> Queue(
        string key, [FromQuery] HrOperationsFilter filter,
        [FromQuery] int page = 1, [FromQuery] int pageSize = HrOperationsPolicy.DefaultPageSize,
        CancellationToken ct = default)
    {
        if (Disabled) return NotFound();
        if (HrOperationsCatalog.FromKey(key) is not HrOperationsQueue queue) return NotFound();
        return FromResult(await _service.GetQueueAsync(queue, filter, page, pageSize, ct));
    }

    /// <summary>
    /// تصدير طابور واحد (CSV). لا يمرّ من هنا صفّ لم يكن ليظهر في التفصيل — نفس المصدر ونفس النطاق.
    /// </summary>
    [HttpGet("queues/{key}/export")]
    [Authorize(Policy = Policies.HrOperationsExport)]
    public async Task<IActionResult> Export(
        string key, [FromQuery] HrOperationsFilter filter, CancellationToken ct)
    {
        if (Disabled) return NotFound();
        if (HrOperationsCatalog.FromKey(key) is not HrOperationsQueue queue) return NotFound();

        var result = await _service.ExportQueueAsync(queue, filter, ct);
        if (!result.Succeeded) return ToProblem(result);

        var export = result.Value!;

        // التدقيق قبل التسليم: لا تخرج بايتة واحدة دون أثر يُسأل عنه لاحقًا.
        await _audit.LogAsync(
            CurrentUserId, "HrOperations.Export", "HrOperationsQueue", null,
            JsonSerializer.Serialize(new
            {
                queue = HrOperationsCatalog.Key(queue),
                rowCount = export.RowCount,
                fileName = export.FileName,
                filter.DepartmentId,
                filter.TeamId,
                filter.UserId,
                filter.Type,
                filter.Status,
                filter.OverdueOnly,
                filter.FromCycleKey,
                filter.ToCycleKey
            }),
            HttpContext.Connection.RemoteIpAddress?.ToString(), ct);

        return File(export.Content, export.ContentType, export.FileName);
    }
}
