namespace Reporting.Application.Services;

public record ServiceDto(Guid Id, string NameAr, string? NameEn, bool IsActive, int SortOrder, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);
public record CreateServiceRequest(string NameAr, string? NameEn, int SortOrder);
public record UpdateServiceRequest(string NameAr, string? NameEn, int SortOrder);
public record ServiceDeleteResult(bool HardDeleted, ServiceDto? Service, string Message);
