using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Directory;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// DEF-P123-001 (تكرار أسماء الإدارات/الفرق يُقبل صامتًا) و DEF-P123-002 (تكرار رمز الإدارة ⇒ 500).
///
/// <para>
/// الحماية مزدوجة عمدًا: تحقّق تطبيقيّ يعطي رسالة عربيّة مفهومة، وفهرس فريد في قاعدة البيانات
/// هو الضمانة النهائيّة ضدّ التسابق. هذه الحزمة تثبت الطبقتين معًا: المسار المفهوم في الحالة
/// المتسلسلة، والقيد الفعليّ في الحالة المتزامنة.
/// </para>
/// </summary>
[Collection("Integration")]
public class DirectoryNameUniquenessTests
{
    private readonly CustomWebApplicationFactory _factory;

    public DirectoryNameUniquenessTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private async Task<DepartmentDto> CreateDepartmentAsync(HttpClient admin, string nameAr, string? code = null)
    {
        var res = await admin.PostAsJsonAsync("/api/directory/departments",
            new CreateDepartmentRequest(nameAr, null, code, null));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.ReadAsync<DepartmentDto>();
        Assert.NotNull(dto);
        return dto!;
    }

    private static async Task AssertConflictAsync(HttpResponseMessage res, string expectedErrorCode)
    {
        // لا 500 ولا جسم فارغ: العقد هو 409 + RFC 7807 يحمل الرمز الدلاليّ والرسالة العربيّة.
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains(expectedErrorCode, body);
        // لا يُسرَّب اسم القيد الداخليّ ولا نصّ SQL.
        Assert.DoesNotContain("IX_", body);
        Assert.DoesNotContain("23505", body);
        Assert.DoesNotContain("duplicate key", body);
    }

    [Fact]
    public async Task CreateDepartment_WithDuplicateNameAr_Returns409_NotSilentSuccess()
    {
        var (admin, _) = await TestAuth.CreateUserAsync(_factory, "Admin");
        var name = Unique("إدارة-تفرّد");
        await CreateDepartmentAsync(admin, name);

        var second = await admin.PostAsJsonAsync("/api/directory/departments",
            new CreateDepartmentRequest(name, null, null, null));

        await AssertConflictAsync(second, "department.name.conflict");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Departments.CountAsync(d => d.NameAr == name));
    }

    [Fact]
    public async Task CreateDepartment_WithDuplicateCode_Returns409_NotServerError()
    {
        var (admin, _) = await TestAuth.CreateUserAsync(_factory, "Admin");
        var code = Unique("CD").Substring(0, 20);
        await CreateDepartmentAsync(admin, Unique("إدارة-رمز-أولى"), code);

        var second = await admin.PostAsJsonAsync("/api/directory/departments",
            new CreateDepartmentRequest(Unique("إدارة-رمز-ثانية"), null, code, null));

        // DEF-P123-002: كان 500 بجسم فارغ لأنّ 23505 يصعد غير مُترجَم.
        Assert.NotEqual(HttpStatusCode.InternalServerError, second.StatusCode);
        await AssertConflictAsync(second, "department.code.conflict");
    }

    [Fact]
    public async Task UpdateDepartment_ToAnExistingName_Returns409_ButKeepingOwnNameSucceeds()
    {
        var (admin, _) = await TestAuth.CreateUserAsync(_factory, "Admin");
        var takenName = Unique("إدارة-محجوزة");
        await CreateDepartmentAsync(admin, takenName);
        var mine = await CreateDepartmentAsync(admin, Unique("إدارة-قابلة-للتعديل"));

        var clash = await admin.PutAsJsonAsync($"/api/directory/departments/{mine.Id}",
            new UpdateDepartmentRequest(takenName, null, null, null, true));
        await AssertConflictAsync(clash, "department.name.conflict");

        // الصفّ نفسه مستثنى من فحص التفرّد ⇒ حفظ الاسم الحاليّ بلا تغيير لا يتضارب مع ذاته.
        var selfSave = await admin.PutAsJsonAsync($"/api/directory/departments/{mine.Id}",
            new UpdateDepartmentRequest(mine.NameAr, "Renamed", null, null, true));
        Assert.Equal(HttpStatusCode.OK, selfSave.StatusCode);
    }

    [Fact]
    public async Task CreateTeam_WithDuplicateNameInSameDepartment_Returns409()
    {
        var (admin, _) = await TestAuth.CreateUserAsync(_factory, "Admin");
        var dept = await CreateDepartmentAsync(admin, Unique("إدارة-فرق"));
        var teamName = Unique("فريق-تفرّد");

        var first = await admin.PostAsJsonAsync("/api/directory/teams",
            new CreateTeamRequest(teamName, null, dept.Id, null));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await admin.PostAsJsonAsync("/api/directory/teams",
            new CreateTeamRequest(teamName, null, dept.Id, null));
        await AssertConflictAsync(second, "team.name.conflict");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Teams.CountAsync(t => t.DepartmentId == dept.Id && t.NameAr == teamName));
    }

    [Fact]
    public async Task CreateTeam_WithSameNameInAnotherDepartment_IsAllowed_PerContract()
    {
        var (admin, _) = await TestAuth.CreateUserAsync(_factory, "Admin");
        var deptA = await CreateDepartmentAsync(admin, Unique("إدارة-أ"));
        var deptB = await CreateDepartmentAsync(admin, Unique("إدارة-ب"));
        var teamName = Unique("فريق-مشترك-الاسم");

        var inA = await admin.PostAsJsonAsync("/api/directory/teams",
            new CreateTeamRequest(teamName, null, deptA.Id, null));
        var inB = await admin.PostAsJsonAsync("/api/directory/teams",
            new CreateTeamRequest(teamName, null, deptB.Id, null));

        // نطاق التفرّد هو (الإدارة، الاسم) لا الاسم وحده ⇒ لا يُمنع التكرار عبر الإدارات.
        Assert.Equal(HttpStatusCode.OK, inA.StatusCode);
        Assert.Equal(HttpStatusCode.OK, inB.StatusCode);
    }

    [Fact]
    public async Task UpdateTeam_RenamingOntoASiblingName_Returns409()
    {
        var (admin, _) = await TestAuth.CreateUserAsync(_factory, "Admin");
        var dept = await CreateDepartmentAsync(admin, Unique("إدارة-إعادة-تسمية"));
        var takenName = Unique("فريق-محجوز");

        await admin.PostAsJsonAsync("/api/directory/teams",
            new CreateTeamRequest(takenName, null, dept.Id, null));
        var mine = await (await admin.PostAsJsonAsync("/api/directory/teams",
            new CreateTeamRequest(Unique("فريق-قابل-للتعديل"), null, dept.Id, null))).ReadAsync<TeamDto>();

        var clash = await admin.PutAsJsonAsync($"/api/directory/teams/{mine!.Id}",
            new UpdateTeamRequest(takenName, null, dept.Id, null, true));
        await AssertConflictAsync(clash, "team.name.conflict");
    }

    [Fact]
    public async Task ConcurrentDepartmentCreates_WithSameName_ProduceExactlyOneRow()
    {
        var (admin, _) = await TestAuth.CreateUserAsync(_factory, "Admin");
        var name = Unique("إدارة-تسابق");

        // التحقّق التطبيقيّ وحده لا يمنع التسابق (فحص ثمّ كتابة). الفهرس الفريد هو ما يحسمه،
        // والاستثناء 23505 يُترجَم إلى نفس الرمز الدلاليّ فلا يظهر 500 للمستخدم إطلاقًا.
        var requests = Enumerable.Range(0, 6).Select(_ =>
            admin.PostAsJsonAsync("/api/directory/departments",
                new CreateDepartmentRequest(name, null, null, null))).ToArray();
        var responses = await Task.WhenAll(requests);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.OK),
            r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Departments.CountAsync(d => d.NameAr == name));
    }

    [Fact]
    public async Task ConcurrentTeamCreates_WithSameNameInSameDepartment_ProduceExactlyOneRow()
    {
        var (admin, _) = await TestAuth.CreateUserAsync(_factory, "Admin");
        var dept = await CreateDepartmentAsync(admin, Unique("إدارة-تسابق-فرق"));
        var teamName = Unique("فريق-تسابق");

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            admin.PostAsJsonAsync("/api/directory/teams",
                new CreateTeamRequest(teamName, null, dept.Id, null))).ToArray());

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.OK),
            r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Teams.CountAsync(t => t.DepartmentId == dept.Id && t.NameAr == teamName));
    }

    [Fact]
    public async Task LeadingOrTrailingWhitespace_DoesNotBypassUniqueness()
    {
        var (admin, _) = await TestAuth.CreateUserAsync(_factory, "Admin");
        var name = Unique("إدارة-فراغات");
        await CreateDepartmentAsync(admin, name);

        var padded = await admin.PostAsJsonAsync("/api/directory/departments",
            new CreateDepartmentRequest($"   {name}  ", null, null, null));

        // التطبيع = Trim فقط (مطابق حرفيًّا لما تخزّنه الخدمة) ⇒ الفراغات لا تصنع اسمًا جديدًا.
        await AssertConflictAsync(padded, "department.name.conflict");
    }
}
