using Reporting.Application.Common;
using Reporting.Domain.Enums;

namespace Reporting.Application.Governance;

/// <summary>
/// طبقة الملاحظات الإدارية الموحّدة: تُعرض حسب السياق وتُقيَّد رؤيتها بمن يحق له رؤية الكيان المرتبط.
/// </summary>
public interface IManagementNoteService
{
    /// <summary>ملاحظات كيان معيّن (مرتبطة بالسياق). تُرجع 403 لمن لا يحق له رؤية الكيان.</summary>
    Task<Result<IReadOnlyList<ManagementNoteDto>>> ListAsync(
        ManagementNoteEntityType entityType, Guid entityId, CancellationToken ct = default);

    /// <summary>إنشاء ملاحظة إدارية على كيان (إدارة فقط، ضمن نطاق رؤيتها للكيان).</summary>
    Task<Result<ManagementNoteDto>> CreateAsync(CreateManagementNoteRequest request, CancellationToken ct = default);

    /// <summary>وضع علامة «تمّت معالجتها» على ملاحظة تتطلّب إجراءً.</summary>
    Task<Result<ManagementNoteDto>> ResolveAsync(Guid id, CancellationToken ct = default);
}
