using Microsoft.AspNetCore.Identity;

namespace Reporting.Infrastructure.Identity;

/// <summary>دور النظام — IdentityRole بمفتاح GUID.</summary>
public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() { }
    public ApplicationRole(string name) : base(name) { }

    public string? DisplayNameAr { get; set; }
}
