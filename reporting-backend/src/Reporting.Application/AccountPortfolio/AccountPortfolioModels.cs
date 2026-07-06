using Reporting.Domain.Enums;

namespace Reporting.Application.AccountPortfolio;

// ===== محفظة مدير الحساب (ACCOUNT-MANAGER-PORTFOLIO) — DTOs عرض فقط =====
// كل الأنواع هنا قراءة فقط ومبسّطة (بطاقات/جداول). لا تحمل أيّ بيانات حسّاسة من قيم حقول التقارير،
// ولا تقييمات KPI، ولا مسودّات. النطاق مفروض خادمًا على مشاريع المستخدم نفسه (AccountManagerId).

/// <summary>بطاقة مشروع في المحفظة — اسم/عميل/خدمة/حالة + عدد المخرجات المعتمَدة وآخرها (إحصاء فقط).</summary>
public record PortfolioProjectDto(
    Guid Id,
    string Name,
    Guid ClientId,
    string? ClientName,
    ServiceType ServiceType,
    ProjectStatus Status,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int OutputCount,
    DateTime? LastOutputAtUtc);

/// <summary>بطاقة عميل في المحفظة — مشتقّ حصرًا من مشاريع المستخدم المرئية (لا من Client.AccountManagerId).</summary>
public record PortfolioClientDto(
    Guid Id,
    string Name,
    ClientStatus Status,
    int ProjectCount,
    int ActiveProjectCount);

/// <summary>تفاصيل عميل في المحفظة — العميل + مشاريع المستخدم المرئية التابعة له فقط.</summary>
public record PortfolioClientDetailDto(
    PortfolioClientDto Client,
    IReadOnlyList<PortfolioProjectDto> Projects);

/// <summary>
/// مخرَج معتمَد لمشروع — حقول ملخّص آمنة فقط (مَن سلّم/الفترة/الحالة/تاريخ التسليم).
/// لا تتضمّن قيم حقول التقرير الخام ولا أيّ محتوى داخلي. تُستثنى المسودّات والمُعادة.
/// </summary>
public record PortfolioOutputDto(
    Guid SubmissionId,
    Guid SubmitterId,
    string? SubmitterName,
    PeriodType PeriodType,
    string PeriodKey,
    SubmissionStatus Status,
    DateTime? SubmittedAtUtc);
