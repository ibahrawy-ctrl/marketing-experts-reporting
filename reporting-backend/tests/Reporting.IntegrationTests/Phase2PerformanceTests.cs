using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Security;
using Reporting.Domain.Entities.Development;
using Reporting.Domain.Entities.Governance;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Identity;
using Reporting.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace Reporting.IntegrationTests;

/// <summary>
/// P2 — قياس الأداء وغياب N+1 على أسطح المرحلة الثانية.
///
/// <para><b>لماذا العدّ لا الزمن وحده؟</b> N+1 على عيّنة صغيرة يبدو سريعًا: عشرة استعلامات
/// على عشرة صفوف تنتهي في أجزاء من الثانية، فيمرّ العيب ثمّ ينفجر على بيانات حقيقيّة.
/// الادّعاء الصادق إذن **بنيويّ**: عدد أوامر SQL لا يتغيّر حين يكبر حجم البيانات.
/// الزمن يُقاس أيضًا، لكنّه شاهد ثانويّ لا الحارس.</para>
///
/// <para><b>حدود هذا القياس — تُذكَر ولا تُعمَّم:</b> القاعدة محلّيّة على الجهاز نفسه، بلا
/// كمون شبكة، وببيانات مولَّدة لا واقعيّة التوزيع. الأرقام هنا سقف علويّ للمقارنة البنيويّة
/// لا وعد أداء إنتاجيّ.</para>
/// </summary>
[Collection("Phase2")]
public class Phase2PerformanceTests(Phase2WebApplicationFactory factory, ITestOutputHelper output)
{
    private const int SampleSize = 20;

    /// <summary>عيّنة تقارب الخمسمئة موظّف كما طُلِب؛ يُصرَّح بالرقم الفعليّ في المخرجات.</summary>
    private const int LargeScopeSize = 500;

    private async Task<T> DbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>
    /// إنشاء موظّفين تابعين مباشرةً عبر السياق بلا كلمات مرور: هؤلاء **موضوعات** لا فاعلون،
    /// ولا يسجّلون دخولًا إطلاقًا. تمريرهم عبر <c>UserManager</c> كان سيدفع كلفة اشتقاق
    /// مفتاح لكلّ واحد منهم بلا أن يضيف ذلك شيئًا إلى ما نقيسه.
    /// </summary>
    private async Task SeedDirectReportsAsync(Guid managerId, int count)
    {
        await DbAsync(async db =>
        {
            var batch = Enumerable.Range(0, count).Select(_ =>
            {
                var id = Guid.NewGuid();
                var email = $"p2-perf-{id:N}@test.local";
                return new ApplicationUser
                {
                    Id = id,
                    UserName = email,
                    NormalizedUserName = email.ToUpperInvariant(),
                    Email = email,
                    NormalizedEmail = email.ToUpperInvariant(),
                    EmailConfirmed = true,
                    FullName = $"موظّف قياس {id:N}"[..24],
                    IsActive = true,
                    ManagerId = managerId,
                    SecurityStamp = Guid.NewGuid().ToString("N"),
                    ConcurrencyStamp = Guid.NewGuid().ToString("N"),
                };
            });

            db.Users.AddRange(batch);
            await db.SaveChangesAsync();
            return 0;
        });
    }

    /// <summary>صفوف تابعة للموضوع نفسه — بها نثبت أنّ عدد الأوامر لا يتبع عدد الصفوف.</summary>
    private async Task SeedSubjectRowsAsync(Guid subjectId, Guid authorId, int count)
    {
        await DbAsync(async db =>
        {
            for (var i = 0; i < count; i++)
            {
                db.ImprovementPlans.Add(new ImprovementPlan
                {
                    SubjectUserId = subjectId,
                    OwnerId = subjectId,
                    Title = $"خطّة قياس {i}",
                    Status = ImprovementPlanStatus.Open,
                    CreatedAtUtc = DateTime.UtcNow,
                });
                db.ManagementNotes.Add(new ManagementNote
                {
                    EntityType = ManagementNoteEntityType.User,
                    EntityId = subjectId,
                    AuthorId = authorId,
                    Body = $"ملاحظة قياس {i}",
                    RequiresAction = true,
                    Status = ManagementNoteStatus.Open,
                    Sensitivity = (int)FieldSensitivity.Internal,
                    CreatedAtUtc = DateTime.UtcNow,
                });
            }

            await db.SaveChangesAsync();
            return 0;
        });
    }

    /// <summary>عدد أوامر SQL لنداء واحد. الإحماء خارج القياس: أوّل نداء يبني نموذج EF والخطط.</summary>
    private async Task<int> CommandsForAsync(HttpClient client, string url)
    {
        factory.SqlCounter.Reset();
        var res = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return factory.SqlCounter.Count;
    }

    private static async Task<(double P95, double Median)> LatencyAsync(HttpClient client, string url)
    {
        var samples = new List<double>(SampleSize);
        for (var i = 0; i < SampleSize; i++)
        {
            var sw = Stopwatch.StartNew();
            var res = await client.GetAsync(url);
            sw.Stop();
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            samples.Add(sw.Elapsed.TotalMilliseconds);
        }

        samples.Sort();
        var p95Index = (int)Math.Ceiling(0.95 * samples.Count) - 1;
        return (samples[p95Index], samples[samples.Count / 2]);
    }

    // ═══════════════ ① غياب N+1 بالعدّ لا بالانطباع ═══════════════

    /// <summary>
    /// الحارس البنيويّ لـEmployee 360: مضاعفة صفوف الموضوع عشرين ضعفًا **لا تزيد أمرًا واحدًا**.
    /// لو كان أيّ قسم يستعلم صفًّا صفًّا لظهر الفرق هنا فورًا مهما كان الزمن مقبولًا.
    /// </summary>
    [Fact]
    public async Task Employee360_Command_Count_Does_Not_Follow_The_Subjects_Row_Count()
    {
        var (manager, managerId) = await Phase2TestAuth.CreateUserAsync(factory, "Manager");
        var (_, subjectId) = await Phase2TestAuth.CreateUserAsync(factory, "Employee", managerId);
        var url = $"/api/employees/{subjectId}/profile-360";

        await manager.GetAsync(url); // إحماء: بناء النموذج والخطط خارج القياس.
        var before = await CommandsForAsync(manager, url);

        await SeedSubjectRowsAsync(subjectId, managerId, 20);
        var after = await CommandsForAsync(manager, url);

        output.WriteLine($"Employee360 commands: before={before} after={after}");
        Assert.Equal(before, after);
    }

    /// <summary>
    /// الحارس البنيويّ للوحة العمليّات: النطاق يكبر من موظّفَين إلى اثنين وخمسين،
    /// وعدد الأوامر يبقى كما هو — وهو تعريف «التجميع دفعةً واحدة» لا «لكلّ موظّف استعلام».
    /// </summary>
    [Fact]
    public async Task HrOperations_Dashboard_Command_Count_Does_Not_Follow_Team_Size()
    {
        var (hr, hrId) = await Phase2TestAuth.CreateUserAsync(
            factory, "Hr", null, null, null, AppPermissions.HrOperationsView);
        await SeedDirectReportsAsync(hrId, 2);
        const string url = "/api/hr-operations/dashboard";

        await hr.GetAsync(url); // إحماء.
        var small = await CommandsForAsync(hr, url);

        await SeedDirectReportsAsync(hrId, 50);
        var large = await CommandsForAsync(hr, url);

        output.WriteLine($"HrOperations commands: 2 users={small} · 52 users={large}");
        Assert.Equal(small, large);
    }

    /// <summary>
    /// قائمة الالتزام محسوبة في كلّ نداء — فلولا التجميع لكانت أظهر موضع لـN+1 في المرحلة.
    /// </summary>
    [Fact]
    public async Task Checklist_Command_Count_Does_Not_Follow_The_Subjects_Row_Count()
    {
        var (manager, managerId) = await Phase2TestAuth.CreateUserAsync(factory, "Manager");
        var (_, subjectId) = await Phase2TestAuth.CreateUserAsync(factory, "Employee", managerId);
        var url = $"/api/employees/{subjectId}/checklist";

        await manager.GetAsync(url); // إحماء.
        var before = await CommandsForAsync(manager, url);

        await SeedSubjectRowsAsync(subjectId, managerId, 20);
        var after = await CommandsForAsync(manager, url);

        output.WriteLine($"Checklist commands: before={before} after={after}");
        Assert.Equal(before, after);
    }

    // ═══════════════ ② الزمن — شاهد ثانويّ بحدوده المُصرَّح بها ═══════════════

    /// <summary>هدف المرحلة: <c>Employee 360 P95 ≤ 800ms</c> على نطاق مأهول.</summary>
    [Fact]
    public async Task Employee360_P95_Stays_Under_The_Phase_Target()
    {
        var (manager, managerId) = await Phase2TestAuth.CreateUserAsync(factory, "Manager");
        var (_, subjectId) = await Phase2TestAuth.CreateUserAsync(factory, "Employee", managerId);
        await SeedSubjectRowsAsync(subjectId, managerId, 40);
        await SeedDirectReportsAsync(managerId, 60);

        var url = $"/api/employees/{subjectId}/profile-360";
        await manager.GetAsync(url); // إحماء.

        var (p95, median) = await LatencyAsync(manager, url);
        output.WriteLine($"Employee360 latency: p95={p95:F1}ms median={median:F1}ms (n={SampleSize})");
        Assert.True(p95 <= 800, $"Employee 360 P95 = {p95:F1}ms > 800ms");
    }

    /// <summary>
    /// هدف المرحلة: <c>HR Operations P95 ≤ 1.5s</c> على عيّنة تقارب الخمسمئة موظّف.
    /// حجم النطاق الفعليّ يُطبَع في المخرجات كي لا يُقرأ الرقم خارج سياقه.
    /// </summary>
    [Fact]
    public async Task HrOperations_P95_Stays_Under_The_Phase_Target_On_A_Large_Scope()
    {
        var (hr, hrId) = await Phase2TestAuth.CreateUserAsync(
            factory, "Hr", null, null, null, AppPermissions.HrOperationsView);
        await SeedDirectReportsAsync(hrId, LargeScopeSize);

        var scopeSize = await DbAsync(db => db.Users.CountAsync(u => u.ManagerId == hrId));
        const string url = "/api/hr-operations/dashboard";
        await hr.GetAsync(url); // إحماء.

        var (p95, median) = await LatencyAsync(hr, url);
        output.WriteLine($"HrOperations latency on {scopeSize} direct reports: p95={p95:F1}ms median={median:F1}ms (n={SampleSize})");
        Assert.Equal(LargeScopeSize, scopeSize);
        Assert.True(p95 <= 1500, $"HR Operations P95 = {p95:F1}ms > 1500ms على {scopeSize} موظّفًا");
    }
}
