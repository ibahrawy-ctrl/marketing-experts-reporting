using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Domain.Entities.Attendance;

namespace Reporting.Infrastructure.Persistence;

/// <summary>
/// بذر كتالوج أنواع حوادث الحضور (P2-ATT-005). **مُتكافئ التنفيذ** بمطابقة <c>Code</c>:
/// يضيف الناقص ولا يعدّل ولا يحذف أيّ صفّ قائم، فتشغيله مرارًا لا يغيّر شيئًا بعد أوّل مرّة.
/// لا يمسّ أيّ بيانات موظّفين ولا أيّ جدول آخر.
/// </summary>
public static class AttendanceSeeder
{
    private record TypeDef(
        string Code, string NameAr, bool RequiresTimes, bool RequiresPolicyReference,
        bool AllowsMultiplePerDay, int Order);

    private static readonly TypeDef[] Defs =
    {
        new("Late", "تأخّر عن بداية الدوام", RequiresTimes: true, RequiresPolicyReference: false, AllowsMultiplePerDay: false, Order: 10),
        new("Absence", "غياب", RequiresTimes: false, RequiresPolicyReference: false, AllowsMultiplePerDay: false, Order: 20),
        new("UnauthorizedAbsence", "غياب بدون إذن", RequiresTimes: false, RequiresPolicyReference: true, AllowsMultiplePerDay: false, Order: 30),
        new("Disconnection", "انقطاع أثناء الدوام", RequiresTimes: true, RequiresPolicyReference: false, AllowsMultiplePerDay: true, Order: 40),
        new("EarlyLeave", "انصراف مبكّر", RequiresTimes: true, RequiresPolicyReference: false, AllowsMultiplePerDay: false, Order: 50),
        new("Other", "أخرى", RequiresTimes: false, RequiresPolicyReference: false, AllowsMultiplePerDay: true, Order: 90),
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();

        var existing = (await db.AttendanceIncidentTypes.Select(t => t.Code).ToListAsync())
            .Select(c => c.Trim().ToLowerInvariant()).ToHashSet();

        foreach (var def in Defs)
        {
            if (existing.Contains(def.Code.Trim().ToLowerInvariant())) continue;
            db.AttendanceIncidentTypes.Add(new AttendanceIncidentType
            {
                Code = def.Code,
                NameAr = def.NameAr,
                RequiresTimes = def.RequiresTimes,
                RequiresPolicyReference = def.RequiresPolicyReference,
                AllowsMultiplePerDay = def.AllowsMultiplePerDay,
                IsActive = true,
                Order = def.Order
            });
        }

        await db.SaveChangesAsync();
    }
}
