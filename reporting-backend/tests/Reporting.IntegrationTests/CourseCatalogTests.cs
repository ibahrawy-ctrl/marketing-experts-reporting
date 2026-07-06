using System.Net;
using System.Net.Http.Json;
using Reporting.Application.Courses;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// كتالوج الدورات (المصدر الرسمي لأسماء دورات مبيعات B2C). الكتابة عبر سياسة حوكمة القوالب (Admin/CEO/GM)،
/// والقراءة النشطة متاحة لأي مستخدم مصادَق. تُغطّى: CRUD، تفعيل/تعطيل، رفض الاسم المكرّر (case-insensitive)،
/// إخفاء المعطّلة عن نقطة القراءة العامة، وحراسة RBAC. أسماء فريدة لكل تشغيل لتفادي تلوّث القاعدة المشتركة.
/// </summary>
[Collection("Integration")]
public class CourseCatalogTests
{
    private readonly CustomWebApplicationFactory _factory;

    public CourseCatalogTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    [Fact]
    public async Task Admin_CanCreate_Get_Update_Course()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var name = UniqueName("دورة اختبار");

        var created = await (await admin.PostAsJsonAsync("/api/admin/courses",
            new CreateCourseRequest(name, "Test Course", 500))).ReadAsync<CourseDto>();
        Assert.NotNull(created);
        Assert.Equal(name, created!.NameAr);
        Assert.True(created.IsActive);
        Assert.Equal(500, created.SortOrder);

        var got = await (await admin.GetAsync($"/api/admin/courses/{created.Id}")).ReadAsync<CourseDto>();
        Assert.Equal(created.Id, got!.Id);

        var newName = UniqueName("دورة معدّلة");
        var updated = await (await admin.PutAsJsonAsync($"/api/admin/courses/{created.Id}",
            new UpdateCourseRequest(newName, null, 510))).ReadAsync<CourseDto>();
        Assert.Equal(newName, updated!.NameAr);
        Assert.Null(updated.NameEn);
        Assert.Equal(510, updated.SortOrder);
    }

    [Fact]
    public async Task Admin_Deactivate_HidesFromPublicList_ThenReactivate_ShowsAgain()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var name = UniqueName("دورة تعطيل");
        var created = await (await admin.PostAsJsonAsync("/api/admin/courses",
            new CreateCourseRequest(name, null, 600))).ReadAsync<CourseDto>();

        // قبل التعطيل: تظهر في القائمة العامة النشطة.
        var activeBefore = await (await admin.GetAsync("/api/courses")).ReadAsync<List<CourseDto>>();
        Assert.Contains(activeBefore!, c => c.Id == created!.Id);

        var deactivated = await (await admin.PatchAsync($"/api/admin/courses/{created!.Id}/deactivate", null))
            .ReadAsync<CourseDto>();
        Assert.False(deactivated!.IsActive);

        // بعد التعطيل: تختفي من القائمة العامة النشطة، لكنها تبقى في قائمة الإدارة (الكل).
        var activeAfter = await (await admin.GetAsync("/api/courses")).ReadAsync<List<CourseDto>>();
        Assert.DoesNotContain(activeAfter!, c => c.Id == created.Id);
        var adminAll = await (await admin.GetAsync("/api/admin/courses")).ReadAsync<List<CourseDto>>();
        Assert.Contains(adminAll!, c => c.Id == created.Id);

        var reactivated = await (await admin.PatchAsync($"/api/admin/courses/{created.Id}/activate", null))
            .ReadAsync<CourseDto>();
        Assert.True(reactivated!.IsActive);
        var activeAgain = await (await admin.GetAsync("/api/courses")).ReadAsync<List<CourseDto>>();
        Assert.Contains(activeAgain!, c => c.Id == created.Id);
    }

    [Fact]
    public async Task Create_DuplicateName_CaseInsensitive_Conflict()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var name = UniqueName("دورة مكرّرة");
        var first = await admin.PostAsJsonAsync("/api/admin/courses", new CreateCourseRequest(name, null, 700));
        first.EnsureSuccessStatusCode();

        // نفس الاسم بحالة أحرف مختلفة (لاتيني) — نُثبت بمثال لاتيني مكرّر.
        var latin = UniqueName("Course");
        var l1 = await admin.PostAsJsonAsync("/api/admin/courses", new CreateCourseRequest(latin, null, 701));
        l1.EnsureSuccessStatusCode();
        var l2 = await admin.PostAsJsonAsync("/api/admin/courses",
            new CreateCourseRequest(latin.ToUpperInvariant(), null, 702));
        Assert.Equal(HttpStatusCode.Conflict, l2.StatusCode);

        // نفس الاسم العربي حرفيًّا — تعارض.
        var dup = await admin.PostAsJsonAsync("/api/admin/courses", new CreateCourseRequest(name, null, 703));
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
    }

    [Fact]
    public async Task Get_UnknownCourse_NotFound()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await admin.GetAsync($"/api/admin/courses/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task PublicList_ReturnsSeededCourses_ForAnyAuthenticatedUser()
    {
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var list = await (await employee.GetAsync("/api/courses")).ReadAsync<List<CourseDto>>();
        Assert.NotNull(list);
        // الكتالوج المبذور يحوي «الدبلوم الشامل» و«Google Ads» على الأقل.
        Assert.Contains(list!, c => c.NameAr == "الدبلوم الشامل");
        Assert.Contains(list!, c => c.NameAr == "Google Ads");
        Assert.All(list!, c => Assert.True(c.IsActive));
    }

    [Fact]
    public async Task Employee_CannotWriteCatalog_Forbidden()
    {
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var create = await employee.PostAsJsonAsync("/api/admin/courses",
            new CreateCourseRequest(UniqueName("دورة ممنوعة"), null, 800));
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        var list = await employee.GetAsync("/api/admin/courses");
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotAccessCatalog_Unauthorized()
    {
        var client = _factory.CreateClient();
        var pub = await client.GetAsync("/api/courses");
        Assert.Equal(HttpStatusCode.Unauthorized, pub.StatusCode);
        var adminList = await client.GetAsync("/api/admin/courses");
        Assert.Equal(HttpStatusCode.Unauthorized, adminList.StatusCode);
    }
}
