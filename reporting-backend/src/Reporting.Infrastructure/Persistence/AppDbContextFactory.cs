using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Reporting.Infrastructure.Persistence;

/// <summary>
/// مصنع وقت التصميم لـ `dotnet ef` (يتجاوز Web Host).
/// يقرأ سلسلة الاتصال من المتغيّر REPORTING_DB_CONNECTION أو يستخدم افتراضي التطوير.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("REPORTING_DB_CONNECTION")
                   ?? "Host=localhost;Database=reporting_dev;Username=ibrahimelbahrawi";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conn)
            .Options;
        return new AppDbContext(options);
    }
}
