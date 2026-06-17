using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Reporting.IntegrationTests;

/// <summary>
/// مصنع اختبارات التكامل — بيئة Testing + قاعدة بيانات PostgreSQL محلية مخصّصة (reporting_test).
/// لا Testcontainers/Docker التزامًا بقاعدة منع Docker.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default",
            "Host=localhost;Database=reporting_test;Username=ibrahimelbahrawi");
        builder.UseSetting("Jwt:Key", "testing-only-signing-key-not-for-production-32chars!!");
        builder.UseSetting("Jwt:Issuer", "reporting-api");
        builder.UseSetting("Jwt:Audience", "reporting-spa");
        builder.UseSetting("Seed:AdminEmail", "admin@marketingexperts.local");
        builder.UseSetting("Seed:AdminPassword", "Admin#12345");
        // رفع حدّ المعدّل حتى لا تُخنق مجموعة الاختبارات.
        builder.UseSetting("RateLimiting:AuthPermitLimit", "100000");
    }
}

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<CustomWebApplicationFactory> { }
