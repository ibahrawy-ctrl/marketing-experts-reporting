using Microsoft.AspNetCore.Hosting;

namespace Reporting.IntegrationTests;

/// <summary>
/// P1-KPI-TRUTH — مصنع اختبارات معزول تمامًا لهذه المرحلة، على قاعدة PostgreSQL محلّيّة منفصلة
/// (<c>reporting_kpi_truth_iso</c>). <b>لا يمسّ <c>reporting_test</c> المشتركة إطلاقًا</b>، وهذا شرط
/// صريح في التذكرة: القاعدة المشتركة متراكمة الحسابات فلا يصحّ قياس توسيط ذي مرحلتين عليها.
/// يرث كامل إعداد المصنع القياسيّ ثمّ يستبدل سلسلة الاتصال فقط (آخر <c>UseSetting</c> يفوز).
/// </summary>
public class KpiTruthIsolatedFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ConnectionStrings:Default",
            System.Environment.GetEnvironmentVariable("TEST_DB_CONNECTION_KPI_TRUTH")
                ?? "Host=localhost;Database=reporting_kpi_truth_iso;Username=ibrahimelbahrawi");
    }
}

[CollectionDefinition("KpiTruthIsolated")]
public class KpiTruthIsolatedCollection : ICollectionFixture<KpiTruthIsolatedFactory> { }
