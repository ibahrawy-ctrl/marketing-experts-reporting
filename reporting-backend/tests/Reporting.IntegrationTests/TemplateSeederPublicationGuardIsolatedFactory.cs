using Microsoft.AspNetCore.Hosting;

namespace Reporting.IntegrationTests;

/// <summary>
/// REPORT_TEMPLATE_PUBLICATION_GUARD_HOTFIX_R1 — مصنع اختبارات معزول.
/// قاعدة نظيفة مخصّصة كي يكون «قاعدة فارغة تُبذَر مرّة» و«الإقلاع الثاني/الثالث لا يغيّر شيئًا»
/// حتميًّا وغير متأثّر بتراكم <c>reporting_test</c> المشتركة.
/// </summary>
public class TemplateSeederPublicationGuardIsolatedFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ConnectionStrings:Default",
            System.Environment.GetEnvironmentVariable("TEST_DB_CONNECTION_TPLGUARD")
                ?? "Host=localhost;Database=reporting_tplguard_iso;Username=ibrahimelbahrawi");
    }
}

[CollectionDefinition("TemplateSeederPublicationGuardIsolated")]
public class TemplateSeederPublicationGuardIsolatedCollection
    : ICollectionFixture<TemplateSeederPublicationGuardIsolatedFactory> { }
