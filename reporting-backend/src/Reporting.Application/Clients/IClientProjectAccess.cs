namespace Reporting.Application.Clients;

/// <summary>
/// نطاق رؤية العميل/المشروع للمستخدم الحالي (Phase 6) — مفروض خادمًا.
/// • SeesAll: يرى كل العملاء/المشاريع (Admin/CEO/GM/CeoSupport).
/// • غير ذلك: المشروع مرئي إن كان المستخدم مدير حسابه، أو قائد فريقه المسؤول داخل نطاقه،
///   أو وُجد له تسليم على المشروع من داخل نطاقه (own/team/department).
///   والعميل مرئي إن كان المستخدم مدير حسابه أو له مشروع مرئي.
/// </summary>
public sealed record ClientProjectVisibility(
    bool SeesAll,
    IReadOnlySet<Guid> ProjectIds,
    IReadOnlySet<Guid> ClientIds)
{
    public bool CanViewProject(Guid projectId) => SeesAll || ProjectIds.Contains(projectId);
    public bool CanViewClient(Guid clientId) => SeesAll || ClientIds.Contains(clientId);
}

/// <summary>يحسب نطاق رؤية العملاء/المشاريع للمستخدم الحالي.</summary>
public interface IClientProjectAccess
{
    Task<ClientProjectVisibility> ResolveAsync(CancellationToken ct = default);
}
