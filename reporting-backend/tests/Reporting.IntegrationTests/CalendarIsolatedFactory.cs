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
        // قابلة للتجاوز عبر TEST_DB_CONNECTION_CAL لتشغيل بروتوكول القياس على قاعدة معزولة
        // جديدة بلا تعديل الشيفرة؛ الافتراضي يبقى reporting_calendar_iso بلا تغيير سلوك.
        builder.UseSetting("ConnectionStrings:Default",
            System.Environment.GetEnvironmentVariable("TEST_DB_CONNECTION_CAL")
                ?? "Host=localhost;Database=reporting_calendar_iso;Username=ibrahimelbahrawi");
    }
}

[CollectionDefinition("CalendarIsolated")]
public class CalendarIsolatedCollection : ICollectionFixture<CalendarIsolatedFactory> { }
