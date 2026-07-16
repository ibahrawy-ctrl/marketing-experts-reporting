using Microsoft.AspNetCore.Identity;

namespace Reporting.Infrastructure.Identity;

/// <summary>
/// مستخدم النظام — يرث IdentityUser بمفتاح GUID (أفضل ممارسات ASP.NET Core Identity).
/// لا يوجد عمود Role هنا؛ الأدوار تُدار عبر جداول Identity.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // روابط تنظيمية (تُملأ في المراحل اللاحقة)
    public Guid? DepartmentId { get; set; }
    public Guid? TeamId { get; set; }
    public Guid? JobRoleId { get; set; }
    public Guid? ManagerId { get; set; }

    // تجاوز خطوة قائد الفريق في مسارات الاعتماد (Direct Reporting Override) — قاعدة عامة قابلة لإعادة
    // الاستخدام لأي موظّف يتبع مديره مباشرةً رغم بقائه ضمن فريق له قائد. القيمة الافتراضية false لجميع
    // المستخدمين؛ عند true تُتخطّى خطوة قائد الفريق (إجازة/استئذان/تقرير) ويبدأ المسار من المدير المباشر
    // النشط ثم fallback المعتمد (GM ← CEO/Admin). لا يمسّ الانتماء التشغيلي للفريق ولا مسار KPI.
    public bool BypassTeamLeaderApproval { get; set; }
}
