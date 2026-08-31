using Microsoft.AspNetCore.Hosting;

namespace Reporting.IntegrationTests;

/// <summary>
/// R5/DEC-01 — مصنع معزول لعقد رحلة KPI على قاعدة PostgreSQL منفصلة (<c>reporting_dec_one_iso</c>).
/// العزل شرط صحّة لا رفاهية: حسمُ التواتر يفحص **كلّ** القوالب المنشورة في القاعدة، فأيّ قالب عامّ
/// ينشره اختبار آخر يطابق كلّ المستخدمين ويُفسِد قياس «التواتر غير مُهيّأ» وترتيبَ الأولويّة.
/// لا تُمسّ <c>reporting_test</c> المشتركة ولا قاعدة <c>KpiTruthIsolatedFactory</c>.
/// </summary>
public class DecOneIsolatedFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ConnectionStrings:Default",
            System.Environment.GetEnvironmentVariable("TEST_DB_CONNECTION_DEC_ONE")
                ?? "Host=localhost;Database=reporting_dec_one_iso;Username=ibrahimelbahrawi");
    }
}

[CollectionDefinition("DecOneIsolated")]
public class DecOneIsolatedCollection : ICollectionFixture<DecOneIsolatedFactory> { }
