using Reporting.Application.Common;

namespace Reporting.Application.Governance;

/// <summary>
/// مصدر القوائم/الدليل الموحّد للحوكمة (GOV-DIRECTORY-SCOPE-FIX-R1): مصدر وحيد لقوائم اختيار المستخدمين/الإدارات/الفِرق
/// في كل من ورشة الحوكمة وإجراءات الحوكمة والتصعيدات. يطبّق المستخدم الحالي + الأدوار + ScopeResolver + سياسة الحسابات
/// الحسّاسة الموحّدة، ويختلف السلوك حسب الغرض (Purpose). قراءة فقط ولا يكشف أيّ بند/إجراء/تصعيد — مجرّد مراجع اختيار.
/// </summary>
public interface IGovernanceDirectoryService
{
    Task<Result<GovernanceDirectoryDto>> GetDirectoryAsync(GovernanceDirectoryPurpose purpose, CancellationToken ct = default);
}
