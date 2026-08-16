using Microsoft.AspNetCore.Hosting;

namespace Reporting.IntegrationTests;

/// <summary>
/// PROJECT-FIRST-EXECUTION-AGGREGATION-CONTRACT-R1 (Phase 13) — مصنع اختبارات تكامل معزول.
/// قاعدة بيانات نظيفة مخصّصة (reporting_pfe_iso) كي لا تتأثّر بتراكم القاعدة المشتركة
/// (reporting_test) ولا تلوّثها، وليكون تجميع محرّك Project-First حتميًّا (Deterministic).
/// نبذر التسليمات مباشرةً عبر AppDbContext بمفاتيح v5 الحقيقية داخل قسم المشاريع المتكرّر.
/// </summary>
public class ProjectFirstIsolatedFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ConnectionStrings:Default",
            "Host=localhost;Database=reporting_pfe_iso;Username=ibrahimelbahrawi");
    }
}

[CollectionDefinition("ProjectFirstIsolated")]
public class ProjectFirstIsolatedCollection : ICollectionFixture<ProjectFirstIsolatedFactory> { }
