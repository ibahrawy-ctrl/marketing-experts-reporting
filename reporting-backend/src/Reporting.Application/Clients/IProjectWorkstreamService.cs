using Reporting.Application.Common;

namespace Reporting.Application.Clients;

/// <summary>
/// إدارة تيّارات العمل داخل المشروع (P1). قراءة مُنَطَّقة عبر نطاق رؤية المشروع، وكتابة محكومة بسياسة الإدارة.
/// التعطيل بدل الحذف — لا حذف نهائيّ.
/// </summary>
public interface IProjectWorkstreamService
{
    Task<Result<IReadOnlyList<ProjectWorkstreamDto>>> ListAsync(Guid projectId, bool includeInactive, CancellationToken ct = default);
    Task<Result<ProjectWorkstreamDto>> CreateAsync(Guid projectId, CreateProjectWorkstreamRequest request, CancellationToken ct = default);
    Task<Result<ProjectWorkstreamDto>> UpdateAsync(Guid projectId, Guid id, UpdateProjectWorkstreamRequest request, CancellationToken ct = default);
    Task<Result<ProjectWorkstreamDto>> SetActiveAsync(Guid projectId, Guid id, bool isActive, CancellationToken ct = default);
}
