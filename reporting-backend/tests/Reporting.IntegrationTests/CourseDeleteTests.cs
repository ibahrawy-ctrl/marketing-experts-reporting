using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Reporting.Application.Common;
using Reporting.Application.Courses;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// الحذف الآمن لكتالوج الدورات (الجزء 1 من Phase 7):
/// - دورة غير مستخدَمة في أي تقرير ⇒ حذف نهائيّ (HardDeleted=true) وتختفي من قائمة الإدارة.
/// - دورة مستخدَمة في تقرير مبيعات B2C ⇒ أرشفة (تعطيل، HardDeleted=false) دون حذف؛
///   تبقى في قائمة الإدارة، تختفي من القائمة العامة النشطة، والتقرير القديم يبقى قابلًا للقراءة كما هو.
/// - الموظّف/المجهول لا يستطيع الحذف (RBAC).
/// أسماء فريدة لكل تشغيل لتفادي تلوّث القاعدة المشتركة الدائمة.
/// </summary>
[Collection("Integration")]
public class CourseDeleteTests
{
    private readonly CustomWebApplicationFactory _factory;

    public CourseDeleteTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private static async Task<ReportTemplateDetailDto> GetSeededB2cTemplateAsync(HttpClient admin)
    {
        var list = await (await admin.GetAsync("/api/report-templates"))
            .ReadAsync<List<ReportTemplateDto>>();
        var summary = Assert.Single(list!.Where(t => t.Title == B2cByCourseReportSchema.TemplateTitle));
        var detail = await (await admin.GetAsync($"/api/report-templates/{summary.Id}"))
            .ReadAsync<ReportTemplateDetailDto>();
        return detail!;
    }

    private static TemplateVersionDto PublishedVersion(ReportTemplateDetailDto t)
        => t.Versions.Single(v => v.IsPublished);

    [Fact]
    public async Task Delete_UnusedCourse_HardDeletes_AndDisappearsFromAdminList()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var name = UniqueName("دورة غير مستخدمة");
        var created = await (await admin.PostAsJsonAsync("/api/admin/courses",
            new CreateCourseRequest(name, null, 900))).ReadAsync<CourseDto>();

        var res = await admin.DeleteAsync($"/api/admin/courses/{created!.Id}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var result = await res.ReadAsync<CourseDeleteResult>();
        Assert.True(result!.HardDeleted);
        Assert.Null(result.Course);

        // اختفت من قائمة الإدارة (حُذفت نهائيًّا) ومن القائمة العامة.
        var adminAll = await (await admin.GetAsync("/api/admin/courses")).ReadAsync<List<CourseDto>>();
        Assert.DoesNotContain(adminAll!, c => c.Id == created.Id);
        var getGone = await admin.GetAsync($"/api/admin/courses/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getGone.StatusCode);
    }

    [Fact]
    public async Task Delete_UsedCourse_Archives_KeepsOldReportReadable_AndHidesFromActiveList()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var detail = await GetSeededB2cTemplateAsync(admin);
        var grid = Assert.Single(PublishedVersion(detail).Fields.Where(f => f.FieldType == FieldType.TableGrid));

        // دورة فريدة سنستخدمها كنصّ في تقرير B2C.
        var courseName = UniqueName("دورة مستخدمة");
        var course = await (await admin.PostAsJsonAsync("/api/admin/courses",
            new CreateCourseRequest(courseName, null, 910))).ReadAsync<CourseDto>();

        // طبقة عليا بلا مدير ⇒ اعتماد بخطوة واحدة (نفس مسار الاعتماد الحالي).
        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", ceoId);

        var draft = await (await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(detail.Id, PeriodType.Weekly, "2026-W24")))
            .ReadAsync<SubmissionDto>();

        var rows = new[]
        {
            new[] { courseName, "12", "40", "30", "18", "9", "6", "18000", "3", "السعر" },
        };
        var gridJson = JsonSerializer.Serialize(rows);
        var save = await employee.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(grid.Id, null, null, null, null, gridJson) }));
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var submitted = await employee.PostAsync($"/api/submissions/{draft.Id}/submit", null);
        submitted.EnsureSuccessStatusCode();
        var approved = await (await ceo.PostAsJsonAsync($"/api/submissions/{draft.Id}/approve",
            new ApprovalActionRequest("معتمد"))).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Closed, approved!.Status);

        // الحذف الآمن ⇒ أرشفة (لا حذف نهائي) لأنها مستخدَمة في تقرير معتمَد.
        var res = await admin.DeleteAsync($"/api/admin/courses/{course!.Id}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var result = await res.ReadAsync<CourseDeleteResult>();
        Assert.False(result!.HardDeleted);
        Assert.NotNull(result.Course);
        Assert.False(result.Course!.IsActive);

        // تبقى في قائمة الإدارة (مؤرشفة) وتختفي من القائمة العامة النشطة.
        var adminAll = await (await admin.GetAsync("/api/admin/courses")).ReadAsync<List<CourseDto>>();
        Assert.Contains(adminAll!, c => c.Id == course.Id && !c.IsActive);
        var activeList = await (await admin.GetAsync("/api/courses")).ReadAsync<List<CourseDto>>();
        Assert.DoesNotContain(activeList!, c => c.Id == course.Id);

        // التقرير القديم يبقى قابلًا للقراءة ويحتفظ باسم الدورة نصًّا كما هو.
        var reread = await (await admin.GetAsync($"/api/submissions/{draft.Id}")).ReadAsync<SubmissionDto>();
        var storedJson = Assert.Single(reread!.FieldValues.Where(v => v.TemplateFieldId == grid.Id)).ValueJson;
        var storedRows = JsonSerializer.Deserialize<string[][]>(storedJson!);
        Assert.Equal(courseName, storedRows![0][0]);
    }

    [Fact]
    public async Task Delete_UnknownCourse_NotFound()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var res = await admin.DeleteAsync($"/api/admin/courses/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Employee_CannotDeleteCourse_Forbidden()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await (await admin.PostAsJsonAsync("/api/admin/courses",
            new CreateCourseRequest(UniqueName("دورة محمية"), null, 920))).ReadAsync<CourseDto>();

        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var del = await employee.DeleteAsync($"/api/admin/courses/{created!.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, del.StatusCode);

        // ما زالت موجودة (لم تُحذَف).
        var still = await admin.GetAsync($"/api/admin/courses/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, still.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotDeleteCourse_Unauthorized()
    {
        var client = _factory.CreateClient();
        var del = await client.DeleteAsync($"/api/admin/courses/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, del.StatusCode);
    }
}
