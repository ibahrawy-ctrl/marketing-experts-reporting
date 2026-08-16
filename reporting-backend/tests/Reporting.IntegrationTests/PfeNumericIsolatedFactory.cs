using Microsoft.AspNetCore.Hosting;

namespace Reporting.IntegrationTests;

/// <summary>
/// PROJECT-REPEATABLE-NUMERIC-VALIDATION-R1 — مصنع اختبارات معزول لتحقّق الحقول الرقميّة داخل
/// قسم المشاريع المتكرّر. قاعدة PostgreSQL منفصلة مؤقّتة (reporting_pfe_iso) لا تمسّ قاعدة
/// الاختبارات المشتركة reporting_test إطلاقًا (شرط العزل في التذكرة). يرث كامل الإعداد من المصنع
/// القياسيّ ثم يستبدل سلسلة الاتصال فقط (آخر UseSetting يفوز). الهجرة والبذور تعمل عند الإقلاع.
/// </summary>
public class PfeNumericIsolatedFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        // قابلة للتجاوز عبر TEST_DB_CONNECTION_PFE؛ الافتراضي يبقى reporting_pfe_iso بلا تغيير سلوك.
        builder.UseSetting("ConnectionStrings:Default",
            System.Environment.GetEnvironmentVariable("TEST_DB_CONNECTION_PFE")
                ?? "Host=localhost;Database=reporting_pfe_iso;Username=ibrahimelbahrawi");
    }
}

[CollectionDefinition("PfeNumericIsolated")]
public class PfeNumericIsolatedCollection : ICollectionFixture<PfeNumericIsolatedFactory> { }
