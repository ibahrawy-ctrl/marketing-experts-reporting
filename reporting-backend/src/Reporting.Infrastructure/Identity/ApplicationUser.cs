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
}
