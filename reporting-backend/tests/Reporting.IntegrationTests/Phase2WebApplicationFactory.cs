using Microsoft.AspNetCore.Hosting;

namespace Reporting.IntegrationTests;

/// <summary>
/// مصنع اختبارات المرحلة الثانية. يختلف عن <see cref="CustomWebApplicationFactory"/> في أمرين فقط:
/// 1) قاعدة بيانات **مستقلّة** خاصّة بالمرحلة الثانية — لا تُستعمل <c>reporting_test</c> المشتركة الملوَّثة (§11).
/// 2) أعلام المرحلة الثانية مرفوعة **محلّيًّا في الاختبار فقط**؛ تبقى <c>false</c> افتراضيًّا في كلّ مكان آخر (§9).
/// رفع العلم ليس تفويضًا: كلّ فحوص الصلاحيّة تعمل كاملة تحته.
/// </summary>
public class Phase2WebApplicationFactory : CustomWebApplicationFactory
{
    public const string DefaultConnection =
        "Host=localhost;Database=reporting_p2_20260825;Username=ibrahimelbahrawi";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("ConnectionStrings:Default",
            System.Environment.GetEnvironmentVariable("PHASE2_TEST_DB_CONNECTION") ?? DefaultConnection);

        builder.UseSetting("Phase2:Employee360Enabled", "true");
        builder.UseSetting("Phase2:AttendanceEnabled", "true");
        builder.UseSetting("Phase2:HrOperationsEnabled", "true");
        builder.UseSetting("Phase2:EmployeeChecklistEnabled", "true");
    }
}

[CollectionDefinition("Phase2")]
public class Phase2Collection : ICollectionFixture<Phase2WebApplicationFactory> { }
