using Microsoft.AspNetCore.Hosting;

namespace Reporting.IntegrationTests;

/// <summary>
/// مصنع اختبارات معزول للتقويم اليوميّ — قاعدة بيانات PostgreSQL منفصلة مؤقّتة (reporting_calendar_iso).
/// لا يمسّ قاعدة الاختبارات المشتركة reporting_test إطلاقًا (شرط العزل في حزمة ROLE-AWARE-REPORTING-CALENDAR).
/// يرث بقيّة الإعداد من المصنع القياسيّ ثم يستبدل سلسلة الاتصال فقط (آخر UseSetting يفوز).
/// </summary>
public class CalendarIsolatedFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ConnectionStrings:Default",
            "Host=localhost;Database=reporting_calendar_iso;Username=ibrahimelbahrawi");
    }
}

[CollectionDefinition("CalendarIsolated")]
public class CalendarIsolatedCollection : ICollectionFixture<CalendarIsolatedFactory> { }
