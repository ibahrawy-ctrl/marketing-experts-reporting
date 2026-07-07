using System.Net;
using System.Net.Http.Json;
using Reporting.Application.Services;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// RC-3 Task 2 — كتالوج خدمات B2B (المصدر الرسمي لأسماء الخدمات في قالب «مبيعات B2B حسب الخدمة»).
/// الكتابة عبر سياسة حوكمة القوالب (Admin/CEO/GM)، والقراءة النشطة متاحة لأي مستخدم مصادَق.
/// تُغطّى: إنشاء/تعديل/تفعيل/تعطيل/حذف آمن (أرشفة)، إخفاء المعطّلة عن القراءة العامة، وحراسة RBAC.
/// أسماء فريدة لكل تشغيل لتفادي تلوّث القاعدة المشتركة الدائمة.
/// </summary>
[Collection("Integration")]
public class ServiceCatalogTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ServiceCatalogTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    // ===== (1) إنشاء + قراءة + تعديل خدمة =====
    [Fact]
    public async Task Admin_CanCreate_Get_Update_Service()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var name = UniqueName("خدمة اختبار");

        var created = await (await admin.PostAsJsonAsync("/api/admin/services",
            new CreateServiceRequest(name, "Test Service", 500))).ReadAsync<ServiceDto>();
        Assert.NotNull(created);
        Assert.Equal(name, created!.NameAr);
        Assert.Equal("Test Service", created.NameEn);
        Assert.True(created.IsActive);
        Assert.Equal(500, created.SortOrder);

        var got = await (await admin.GetAsync($"/api/admin/services/{created.Id}")).ReadAsync<ServiceDto>();
        Assert.Equal(created.Id, got!.Id);

        var newName = UniqueName("خدمة معدّلة");
        var updated = await (await admin.PutAsJsonAsync($"/api/admin/services/{created.Id}",
            new UpdateServiceRequest(newName, null, 510))).ReadAsync<ServiceDto>();
        Assert.Equal(newName, updated!.NameAr);
        Assert.Null(updated.NameEn);
        Assert.Equal(510, updated.SortOrder);
    }

    // ===== (2) تعطيل يُخفي من القائمة العامة ثم تفعيل يعيد الظهور =====
    [Fact]
    public async Task Admin_Deactivate_HidesFromPublicList_ThenActivate_ShowsAgain()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var name = UniqueName("خدمة تعطيل");
        var created = await (await admin.PostAsJsonAsync("/api/admin/services",
            new CreateServiceRequest(name, null, 600))).ReadAsync<ServiceDto>();

        // قبل التعطيل: تظهر في القائمة العامة النشطة (منتقي «الخدمة»).
        var activeBefore = await (await admin.GetAsync("/api/services")).ReadAsync<List<ServiceDto>>();
        Assert.Contains(activeBefore!, s => s.Id == created!.Id);

        var deactivated = await (await admin.PatchAsync($"/api/admin/services/{created!.Id}/deactivate", null))
            .ReadAsync<ServiceDto>();
        Assert.False(deactivated!.IsActive);

        // بعد التعطيل: تختفي من القائمة العامة النشطة، وتبقى في قائمة الإدارة (الكل).
        var activeAfter = await (await admin.GetAsync("/api/services")).ReadAsync<List<ServiceDto>>();
        Assert.DoesNotContain(activeAfter!, s => s.Id == created.Id);
        var adminAll = await (await admin.GetAsync("/api/admin/services")).ReadAsync<List<ServiceDto>>();
        Assert.Contains(adminAll!, s => s.Id == created.Id);

        var activated = await (await admin.PatchAsync($"/api/admin/services/{created.Id}/activate", null))
            .ReadAsync<ServiceDto>();
        Assert.True(activated!.IsActive);
        var activeAgain = await (await admin.GetAsync("/api/services")).ReadAsync<List<ServiceDto>>();
        Assert.Contains(activeAgain!, s => s.Id == created.Id);
    }

    // ===== (3) حذف آمن لخدمة غير مُستخدَمة = حذف نهائي =====
    [Fact]
    public async Task Delete_UnusedService_HardDeletes()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await (await admin.PostAsJsonAsync("/api/admin/services",
            new CreateServiceRequest(UniqueName("خدمة للحذف"), null, 650))).ReadAsync<ServiceDto>();

        var result = await (await admin.DeleteAsync($"/api/admin/services/{created!.Id}"))
            .ReadAsync<ServiceDeleteResult>();
        Assert.True(result!.HardDeleted);

        // بعد الحذف النهائي: غير موجودة في قائمة الإدارة (الكل).
        var adminAll = await (await admin.GetAsync("/api/admin/services")).ReadAsync<List<ServiceDto>>();
        Assert.DoesNotContain(adminAll!, s => s.Id == created.Id);
    }

    // ===== (4) رفض الاسم المكرّر (case-insensitive) =====
    [Fact]
    public async Task Create_DuplicateName_CaseInsensitive_Conflict()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var latin = UniqueName("Service");
        var first = await admin.PostAsJsonAsync("/api/admin/services", new CreateServiceRequest(latin, null, 700));
        first.EnsureSuccessStatusCode();

        var dupUpper = await admin.PostAsJsonAsync("/api/admin/services",
            new CreateServiceRequest(latin.ToUpperInvariant(), null, 701));
        Assert.Equal(HttpStatusCode.Conflict, dupUpper.StatusCode);

        var name = UniqueName("خدمة مكرّرة");
        (await admin.PostAsJsonAsync("/api/admin/services", new CreateServiceRequest(name, null, 702)))
            .EnsureSuccessStatusCode();
        var dup = await admin.PostAsJsonAsync("/api/admin/services", new CreateServiceRequest(name, null, 703));
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
    }

    // ===== (5) خدمة غير موجودة ⇒ 404 =====
    [Fact]
    public async Task Get_UnknownService_NotFound()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await admin.GetAsync($"/api/admin/services/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ===== (6) القراءة العامة تُعيد الكتالوج المبذور لأي مستخدم مصادَق، والمعطّلة غير ظاهرة =====
    [Fact]
    public async Task PublicList_ReturnsSeededServices_AndOnlyActive()
    {
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var list = await (await employee.GetAsync("/api/services")).ReadAsync<List<ServiceDto>>();
        Assert.NotNull(list);
        // الكتالوج المبذور يحوي «تصميم موقع إلكتروني» و«حملات إعلانية» على الأقل.
        Assert.Contains(list!, s => s.NameAr == "تصميم موقع إلكتروني");
        Assert.Contains(list!, s => s.NameAr == "حملات إعلانية");
        // نقطة القراءة العامة لا تُظهِر إلا النشطة (منتقي «الخدمة» لا يعرض المعطّلة).
        Assert.All(list!, s => Assert.True(s.IsActive));
    }

    // ===== (7) الموظّف لا يملك حق كتابة الكتالوج ⇒ 403 =====
    [Fact]
    public async Task Employee_CannotWriteCatalog_Forbidden()
    {
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var create = await employee.PostAsJsonAsync("/api/admin/services",
            new CreateServiceRequest(UniqueName("خدمة ممنوعة"), null, 800));
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        var list = await employee.GetAsync("/api/admin/services");
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    // ===== (8) المجهول (غير مصادَق) ⇒ 401 على القراءة والإدارة =====
    [Fact]
    public async Task Anonymous_CannotAccessCatalog_Unauthorized()
    {
        var client = _factory.CreateClient();
        var pub = await client.GetAsync("/api/services");
        Assert.Equal(HttpStatusCode.Unauthorized, pub.StatusCode);
        var adminList = await client.GetAsync("/api/admin/services");
        Assert.Equal(HttpStatusCode.Unauthorized, adminList.StatusCode);
    }
}
