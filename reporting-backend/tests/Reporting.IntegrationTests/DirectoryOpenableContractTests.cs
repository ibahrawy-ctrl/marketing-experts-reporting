using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Directory;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// P123-R2 — العقد الذي يجعل «دليل الموظّفين» صادقًا: <b>ما يظهر في الدليل يُفتَح، وما لا يظهر يُرَدّ</b>.
///
/// الدليل (<c>/api/directory/users</c>) وملفّ الموظّف (<c>/api/dashboard/employee-profile/{id}</c>) يقرآن
/// النطاق من المصدر نفسه (<see cref="IScopeResolver"/>)، لكنّ ذلك تفصيل تنفيذيّ قابل للانحراف بتغيير أحد
/// الطرفين وحده. والانحراف هنا ليس عيبًا تجميليًّا: كلّ صفّ في الدليل هو **وعد بفتح** — فإن ردّ الخادم 403
/// عليه صار السطح يعرض بابًا يصفع صاحبه، وهو بالضبط ما يمنعه DEC-05.
///
/// لذلك يُقاس الثابت على النتيجة لا على الشيفرة: نطلب الدليل فعلًا بحساب كلّ دور، ثمّ نفتح كلّ صفّ فيه فعلًا.
/// وأُثبِت الاتّجاه المعاكس كذلك (مستخدم خارج الدليل ⇒ 403)، وإلّا لكفى الدليلَ أن يعود فارغًا لينجح.
/// </summary>
[Collection("Integration")]
public class DirectoryOpenableContractTests
{
    private readonly CustomWebApplicationFactory _factory;

    public DirectoryOpenableContractTests(CustomWebApplicationFactory factory) => _factory = factory;

    private sealed class Org
    {
        public required (HttpClient C, Guid Id) Gm;
        public required (HttpClient C, Guid Id) Mgr;
        public required (HttpClient C, Guid Id) Tl;
        public required (HttpClient C, Guid Id) Emp;
        public required (HttpClient C, Guid Id) OtherMgr;
        public required (HttpClient C, Guid Id) OtherEmp;
    }

    /// شجرة معزولة بالكامل: كلّ الحسابات تُنشأ الآن، فلا يتسرّب إليها ساكنو القاعدة المشتركة.
    private async Task<Org> BuildOrgAsync()
    {
        var gm = await TestAuth.CreateUserAsync(_factory, Roles.GeneralManager);
        var mgr = await TestAuth.CreateUserAsync(_factory, Roles.Manager, gm.UserId);
        var tl = await TestAuth.CreateUserAsync(_factory, Roles.TeamLeader, mgr.UserId);
        var emp = await TestAuth.CreateUserAsync(_factory, Roles.Employee, tl.UserId);
        var otherMgr = await TestAuth.CreateUserAsync(_factory, Roles.Manager, gm.UserId);
        var otherEmp = await TestAuth.CreateUserAsync(_factory, Roles.Employee, otherMgr.UserId);

        return new Org
        {
            Gm = (gm.Client, gm.UserId),
            Mgr = (mgr.Client, mgr.UserId),
            Tl = (tl.Client, tl.UserId),
            Emp = (emp.Client, emp.UserId),
            OtherMgr = (otherMgr.Client, otherMgr.UserId),
            OtherEmp = (otherEmp.Client, otherEmp.UserId),
        };
    }

    private static async Task<IReadOnlyList<DirectoryUserDto>> DirectoryAsync(HttpClient client)
    {
        var res = await client.GetAsync("/api/directory/users");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<List<DirectoryUserDto>>())!;
    }

    private static async Task<HttpStatusCode> OpenAsync(HttpClient client, Guid subjectId) =>
        (await client.GetAsync($"/api/dashboard/employee-profile/{subjectId}")).StatusCode;

    // ===== 1) كلّ صفّ معروض يُفتَح فعلًا — على نطاقات محدودة تُعَدّ صفًّا صفًّا =====

    [Fact]
    public async Task Every_Row_The_Directory_Shows_Can_Actually_Be_Opened()
    {
        var org = await BuildOrgAsync();

        // الأدوار ذات النطاق المحدود فقط: النطاق المؤسّسيّ يُقاس في اختبار مستقلّ بعيّنة،
        // إذ فتح كلّ مستخدم نشط في قاعدة مشتركة متراكمة قياسٌ للبطء لا للصحّة.
        foreach (var (label, actor) in new[]
                 {
                     ("الموظّف", org.Emp),
                     ("قائد الفريق", org.Tl),
                     ("المدير", org.Mgr),
                 })
        {
            var rows = await DirectoryAsync(actor.C);
            Assert.NotEmpty(rows);
            foreach (var row in rows)
                Assert.Equal(HttpStatusCode.OK, await OpenAsync(actor.C, row.Id));

            // الوعد ليس عن قائمة فارغة: صاحب الحساب نفسه حاضر دائمًا.
            Assert.Contains(rows, r => r.Id == actor.Id);
            Assert.True(rows.Count > 0, label);
        }
    }

    // ===== 2) الاتّجاه المعاكس: الغياب عن الدليل منعٌ فعليّ لا إخفاء تجميليّ =====

    [Fact]
    public async Task A_User_Missing_From_The_Directory_Is_Actually_Refused()
    {
        var org = await BuildOrgAsync();

        foreach (var actor in new[] { org.Emp, org.Tl, org.Mgr })
        {
            var rows = await DirectoryAsync(actor.C);
            Assert.DoesNotContain(rows, r => r.Id == org.OtherEmp.Id);
            Assert.Equal(HttpStatusCode.Forbidden, await OpenAsync(actor.C, org.OtherEmp.Id));
        }
    }

    // ===== 3) جدول النطاق (DEC-03) مقيسًا على الاستجابة نفسها =====

    [Fact]
    public async Task Employee_Sees_Exactly_Himself()
    {
        var org = await BuildOrgAsync();
        var rows = await DirectoryAsync(org.Emp.C);
        Assert.Equal(new[] { org.Emp.Id }, rows.Select(r => r.Id).ToArray());
    }

    [Fact]
    public async Task TeamLeader_Sees_Himself_And_His_Direct_Reports_Only()
    {
        var org = await BuildOrgAsync();
        var ids = (await DirectoryAsync(org.Tl.C)).Select(r => r.Id).ToHashSet();

        Assert.Equal(new HashSet<Guid> { org.Tl.Id, org.Emp.Id }, ids);
        // «صعودًا» ممنوع كذلك: المدير ليس جزءًا من نطاق قائد الفريق.
        Assert.DoesNotContain(org.Mgr.Id, ids);
    }

    [Fact]
    public async Task Manager_Sees_His_Whole_Subtree_And_Nothing_Beside_It()
    {
        var org = await BuildOrgAsync();
        var ids = (await DirectoryAsync(org.Mgr.C)).Select(r => r.Id).ToHashSet();

        Assert.Equal(new HashSet<Guid> { org.Mgr.Id, org.Tl.Id, org.Emp.Id }, ids);
        Assert.DoesNotContain(org.OtherMgr.Id, ids);
    }

    [Fact]
    public async Task GeneralManager_Sees_The_Company_And_Opens_What_He_Sees()
    {
        var org = await BuildOrgAsync();
        var rows = await DirectoryAsync(org.Gm.C);
        var ids = rows.Select(r => r.Id).ToHashSet();

        // النطاق المؤسّسيّ: كلّ أفراد الشجرة حاضرون، بمن فيهم مَن هم خارج فرع المدير الواحد.
        foreach (var member in new[] { org.Gm.Id, org.Mgr.Id, org.Tl.Id, org.Emp.Id, org.OtherMgr.Id, org.OtherEmp.Id })
        {
            Assert.Contains(member, ids);
            Assert.Equal(HttpStatusCode.OK, await OpenAsync(org.Gm.C, member));
        }
    }

    // ===== 4) الحسابات الموقوفة لا تُعرَض افتراضًا =====

    [Fact]
    public async Task Deactivated_Accounts_Are_Not_Offered_By_Default()
    {
        var org = await BuildOrgAsync();

        var before = (await DirectoryAsync(org.Mgr.C)).Select(r => r.Id).ToHashSet();
        Assert.Contains(org.Emp.Id, before);

        await SetActiveAsync(org.Emp.Id, false);

        var after = (await DirectoryAsync(org.Mgr.C)).Select(r => r.Id).ToHashSet();
        Assert.DoesNotContain(org.Emp.Id, after);
    }

    private async Task SetActiveAsync(Guid userId, bool isActive)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.IsActive = isActive;
        await db.SaveChangesAsync();
    }

    // ===== 5) الدليل ليس سطحًا عامًّا =====

    [Fact]
    public async Task Directory_Requires_An_Authenticated_Session()
    {
        var anon = _factory.CreateClient();
        var res = await anon.GetAsync("/api/directory/users");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
