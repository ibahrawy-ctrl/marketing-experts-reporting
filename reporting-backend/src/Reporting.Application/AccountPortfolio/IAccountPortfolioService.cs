using Reporting.Application.Common;

namespace Reporting.Application.AccountPortfolio;

/// <summary>
/// محفظة مدير الحساب (عرض فقط). النطاق مفروض خادمًا على مشاريع المستخدم الحالي نفسه:
/// Project.AccountManagerId == المستخدم الحالي — بلا توسعة عبر منح الرؤية أو عضوية الفِرق
/// أو المسمّى الوظيفي أو Client.AccountManagerId. لا إنشاء/تعديل/حذف/اعتماد. لا KPI/تقييمات.
/// </summary>
public interface IAccountPortfolioService
{
    /// <summary>مشاريع المستخدم الحالي (AccountManagerId == المستخدم) — كل الحالات مع إحصاء المخرجات.</summary>
    Task<Result<IReadOnlyList<PortfolioProjectDto>>> GetMyProjectsAsync(CancellationToken ct = default);

    /// <summary>مشروع واحد للمستخدم — 404 إن غير موجود، 403 إن خارج نطاقه.</summary>
    Task<Result<PortfolioProjectDto>> GetMyProjectAsync(Guid id, CancellationToken ct = default);

    /// <summary>عملاء المستخدم — مشتقّون حصرًا من مشاريعه المرئية (لا من Client.AccountManagerId).</summary>
    Task<Result<IReadOnlyList<PortfolioClientDto>>> GetMyClientsAsync(CancellationToken ct = default);

    /// <summary>عميل واحد + مشاريع المستخدم المرئية التابعة له — 404 إن غير موجود، 403 إن بلا مشروع مرئيّ تابع له.</summary>
    Task<Result<PortfolioClientDetailDto>> GetMyClientAsync(Guid id, CancellationToken ct = default);

    /// <summary>مخرجات مشروع معتمَدة (المسموح: مُسلَّم/معتمَد/مُصعَّد/مُغلق/ظاهر) — تُستثنى المسودّة/المُعادة. 403 إن خارج النطاق.</summary>
    Task<Result<IReadOnlyList<PortfolioOutputDto>>> GetProjectOutputsAsync(Guid projectId, CancellationToken ct = default);
}
