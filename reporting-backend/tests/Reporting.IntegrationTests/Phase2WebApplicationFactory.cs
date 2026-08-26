using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Reporting.Infrastructure.Persistence;

namespace Reporting.IntegrationTests;

/// <summary>
/// عدّاد أوامر SQL الفعليّة — أداة قياس N+1 الوحيدة الصادقة هنا.
///
/// عدّ الاستدعاءات في طبقة الخدمة كان سيغفل التوسيعات التي يولّدها EF نفسه، وقياس الزمن وحده
/// كان سيخفي N+1 على عيّنة صغيرة (عشرة استعلامات سريعة تبدو سريعة). العدّ على مستوى الأمر
/// يكشف **النمط** لا العَرَض: إن كان العدد يكبر مع عدد الموظّفين فذاك N+1 مهما كان الزمن.
/// </summary>
public sealed class SqlCommandCounter
{
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public void Reset() => Interlocked.Exchange(ref _count, 0);

    internal void Increment() => Interlocked.Increment(ref _count);
}

internal sealed class CountingCommandInterceptor(SqlCommandCounter counter) : DbCommandInterceptor
{
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        counter.Increment();
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        counter.Increment();
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        counter.Increment();
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        counter.Increment();
        return ValueTask.FromResult(result);
    }
}

/// <summary>
/// مصنع اختبارات المرحلة الثانية. يختلف عن <see cref="CustomWebApplicationFactory"/> في ثلاثة أمور:
/// 1) قاعدة بيانات **مستقلّة** خاصّة بالمرحلة الثانية — لا تُستعمل <c>reporting_test</c> المشتركة الملوَّثة (§11).
/// 2) أعلام المرحلة الثانية مرفوعة **محلّيًّا في الاختبار فقط**؛ تبقى <c>false</c> افتراضيًّا في كلّ مكان آخر (§9).
///    رفع العلم ليس تفويضًا: كلّ فحوص الصلاحيّة تعمل كاملة تحته.
/// 3) اعتراض أوامر SQL للعدّ وحده — لا يغيّر أمرًا ولا يمنع تنفيذه، فالقياس هنا مراقب لا وسيط.
/// </summary>
public class Phase2WebApplicationFactory : CustomWebApplicationFactory
{
    public const string DefaultConnection =
        "Host=localhost;Database=reporting_p2_20260825;Username=ibrahimelbahrawi";

    /// <summary>العدّاد مشترك على مستوى المصنع؛ ومجموعة <c>Phase2</c> تعمل تسلسليًّا فلا تتداخل القياسات.</summary>
    public SqlCommandCounter SqlCounter { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("ConnectionStrings:Default",
            System.Environment.GetEnvironmentVariable("PHASE2_TEST_DB_CONNECTION") ?? DefaultConnection);

        builder.UseSetting("Phase2:Employee360Enabled", "true");
        builder.UseSetting("Phase2:AttendanceEnabled", "true");
        builder.UseSetting("Phase2:HrOperationsEnabled", "true");
        builder.UseSetting("Phase2:EmployeeChecklistEnabled", "true");

        var connection = System.Environment.GetEnvironmentVariable("PHASE2_TEST_DB_CONNECTION")
                         ?? DefaultConnection;

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(SqlCounter);

            // إعادة تسجيل السياق ضروريّة لا تجميليّة: تسجيل المعترِض في الحاوية وحدها لم يُلتقَط
            // (قِيس: العدّاد بقي صفرًا)، و`AddDbContext` الثانية لا تتجاوز الخيارات المسجَّلة
            // إلّا بعد إزالتها. اعتماد الصيغة الأولى كان سيُنتج اختبار N+1 ينجح فارغًا.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options => options
                .UseNpgsql(connection)
                .AddInterceptors(new CountingCommandInterceptor(SqlCounter)));
        });
    }
}

[CollectionDefinition("Phase2")]
public class Phase2Collection : ICollectionFixture<Phase2WebApplicationFactory> { }
