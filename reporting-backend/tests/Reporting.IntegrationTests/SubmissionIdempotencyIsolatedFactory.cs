using Microsoft.AspNetCore.Hosting;

namespace Reporting.IntegrationTests;

/// <summary>
/// R22B/DEFECT-IDEMPOTENCY-01 — مصنع معزول لعقد «هويّة التقرير = نَسَب القالب» على قاعدة PostgreSQL
/// منفصلة (<c>reporting_idem_iso</c>). العزل شرط صحّة: هذه الاختبارات تنشر إصدارات جديدة وتقلب حالات
/// نشر وتزرع ازدواجًا تاريخيًّا عمدًا، فلو جرت على <c>reporting_test</c> المشتركة لأفسدت غيرها.
/// </summary>
public class SubmissionIdempotencyIsolatedFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ConnectionStrings:Default",
            System.Environment.GetEnvironmentVariable("TEST_DB_CONNECTION_IDEM_ISO")
                ?? "Host=localhost;Database=reporting_idem_iso;Username=ibrahimelbahrawi");
    }
}

[CollectionDefinition("SubmissionIdempotencyIsolated")]
public class SubmissionIdempotencyIsolatedCollection : ICollectionFixture<SubmissionIdempotencyIsolatedFactory> { }
