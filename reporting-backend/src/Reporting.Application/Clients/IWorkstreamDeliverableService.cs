using Reporting.Application.Common;

namespace Reporting.Application.Clients;

/// <summary>
/// إدارة مخرَجات خطّة الإنتاج داخل تيار العمل (P2 — منصّة التنفيذ العامة). قراءة مُنَطَّقة عبر نطاق رؤية
/// المشروع، وكتابة محكومة بسياسة الإدارة. التعطيل بدل الحذف — لا حذف نهائيّ. **تخطيط فقط، بلا تنفيذ.**
/// </summary>
public interface IWorkstreamDeliverableService
{
    Task<Result<IReadOnlyList<WorkstreamDeliverableDto>>> ListAsync(Guid projectId, Guid workstreamId, bool includeInactive, CancellationToken ct = default);
    Task<Result<WorkstreamDeliverableDto>> CreateAsync(Guid projectId, Guid workstreamId, CreateWorkstreamDeliverableRequest request, CancellationToken ct = default);
    Task<Result<WorkstreamDeliverableDto>> UpdateAsync(Guid projectId, Guid workstreamId, Guid id, UpdateWorkstreamDeliverableRequest request, CancellationToken ct = default);
    Task<Result<WorkstreamDeliverableDto>> SetActiveAsync(Guid projectId, Guid workstreamId, Guid id, bool isActive, CancellationToken ct = default);
}
