using System.Net;
using System.Net.Http.Json;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// شريحة المشروع من تقرير واحد (PROJECT360-PROJECT-SCOPED-REPORT-NAVIGATION-FIX-R1).
///
/// <para><b>ما تحرسه هذه المجموعة</b>: تقرير الموظّف الأسبوعيّ الواحد قد يحمل عمل عدّة
/// مشروعات في قسم متكرّر واحد. فتحه كاملًا من داخل مشروع كان يعرض عمل المشروعات الأخرى.
/// الحارس الفعليّ **خادميّ**: ما لا يخصّ المشروع لا يغادر الخادم أصلًا — ولذلك تفحص
/// الاختبارات **نصّ الاستجابة الخام** لا الكائن المُفكَّك، فالإخفاء في الواجهة يمرّ من
/// فحص الكائن وحده بينما البصمة تبقى في الشبكة.</para>
/// </summary>
[Collection("Integration")]
public class ProjectScopedReportSliceTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ProjectScopedReportSliceTests(CustomWebApplicationFactory factory) => _factory = factory;

    // بصمتان فريدتان لا تظهران في أيّ بيانات أخرى ⇒ وجود إحداهما في استجابة المشروع الآخر تسريب.
    private const string MarkerA = "776001";
    private const string MarkerB = "889002";
    private const string GeneralNote = "ملخّص-عامّ-لا-ينتمي-لمشروع-Z7Q";

    private const string SectionConfig =
        "{\"projectRequired\":true,\"minProjects\":1,\"maxProjects\":5," +
        "\"fields\":[{\"key\":\"spend\",\"label\":\"الميزانية\",\"type\":\"Currency\",\"required\":true}]}";

    private sealed record Fixture(
        Guid ProjectA, Guid ProjectB, Guid SubmissionId, Guid SectionFieldId, HttpClient Admin);

    /// <summary>
    /// تسليم واحد يحمل مشروعين في القسم المتكرّر + حقلًا نصّيًّا عامًّا خارج القسم.
    /// هذا بالضبط شكل العطل المُبلَّغ عنه في الإنتاج.
    /// </summary>
    private async Task<Fixture> BuildTwoProjectSubmissionAsync()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);

        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب شريحة {Guid.NewGuid():N}", null, null, PeriodType.Weekly,
                TemplateClassification.Supplementary)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;

        var section = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("أداء المشاريع", "projects", FieldType.ProjectRepeatableSection, true, null, SectionConfig)))
            .ReadAsync<TemplateFieldDto>();
        var general = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("ملخّص عامّ", "summary", FieldType.LongText, false, null, null)))
            .ReadAsync<TemplateFieldDto>();

        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);

        var client = (await (await admin.PostAsJsonAsync("/api/clients",
            new CreateClientRequest($"عميل شريحة {Guid.NewGuid():N}", null))).ReadAsync<ClientDto>())!;
        var a = (await (await admin.PostAsJsonAsync("/api/projects",
            new CreateProjectRequest(client.Id, $"مشروع أ {Guid.NewGuid():N}", ServiceType.MediaBuying))).ReadAsync<ProjectDto>())!;
        var b = (await (await admin.PostAsJsonAsync("/api/projects",
            new CreateProjectRequest(client.Id, $"مشروع ب {Guid.NewGuid():N}", ServiceType.MediaBuying))).ReadAsync<ProjectDto>())!;

        var draft = (await (await admin.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(created.Id, PeriodType.Weekly,
                ReportingCalendarPolicy.CycleKeyFor(ReportingCalendarPolicy.RiyadhToday()))))
            .ReadAsync<SubmissionDto>())!;

        await admin.PutAsJsonAsync($"/api/submissions/{draft.Id}/values", new SaveFieldValuesRequest(new[]
        {
            new FieldValueInput(section!.Id, null, null, null, null,
                $"[{{\"projectId\":\"{a.Id}\",\"answers\":{{\"spend\":\"{MarkerA}\"}}}}," +
                $"{{\"projectId\":\"{b.Id}\",\"answers\":{{\"spend\":\"{MarkerB}\"}}}}]"),
            new FieldValueInput(general!.Id, GeneralNote, null, null, null, null),
        }));

        return new Fixture(a.Id, b.Id, draft.Id, section.Id, admin);
    }

    private static string Url(Guid projectId, Guid submissionId)
        => $"/api/projects/{projectId}/reports/{submissionId}";

    // ===== 1: داخل النطاق يرى شريحة مشروعه فقط =====
    [Fact]
    public async Task InScope_ReturnsOnlyThisProjectSlice()
    {
        var f = await BuildTwoProjectSubmissionAsync();

        var slice = await (await f.Admin.GetAsync(Url(f.ProjectA, f.SubmissionId)))
            .ReadAsync<ProjectReportSliceDto>();

        Assert.NotNull(slice);
        Assert.Equal(f.ProjectA, slice!.ProjectId);
        Assert.Equal(f.SubmissionId, slice.SubmissionId);
        var entries = Assert.Single(slice.Fields).Entries;
        Assert.Equal(MarkerA, Assert.Single(entries)["spend"]);
    }

    // ===== 2: خارج النطاق → 404 (لا 403) منعًا للتعداد =====
    [Fact]
    public async Task OutOfScopeUser_Returns404()
    {
        var f = await BuildTwoProjectSubmissionAsync();
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var res = await employee.GetAsync(Url(f.ProjectA, f.SubmissionId));

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ===== 3: تقرير غير مرتبط بالمشروع → 404 =====
    [Fact]
    public async Task UnlinkedReport_Returns404()
    {
        var f = await BuildTwoProjectSubmissionAsync();
        var other = await BuildTwoProjectSubmissionAsync(); // مشروع من عالم آخر تمامًا

        var res = await f.Admin.GetAsync(Url(other.ProjectA, f.SubmissionId));

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ===== 4: تقرير بمشروعين — كلٌّ يرى شريحته فقط =====
    [Fact]
    public async Task ReportLinkedToTwoProjects_EachSeesOwnSliceOnly()
    {
        var f = await BuildTwoProjectSubmissionAsync();

        var a = await (await f.Admin.GetAsync(Url(f.ProjectA, f.SubmissionId))).ReadAsync<ProjectReportSliceDto>();
        var b = await (await f.Admin.GetAsync(Url(f.ProjectB, f.SubmissionId))).ReadAsync<ProjectReportSliceDto>();

        Assert.Equal(MarkerA, Assert.Single(Assert.Single(a!.Fields).Entries)["spend"]);
        Assert.Equal(MarkerB, Assert.Single(Assert.Single(b!.Fields).Entries)["spend"]);
    }

    // ===== 5: العبث بالمعرّفات لا يكشف شيئًا =====
    [Fact]
    public async Task TamperedIds_RevealNothing()
    {
        var f = await BuildTwoProjectSubmissionAsync();

        var badProject = await f.Admin.GetAsync(Url(Guid.NewGuid(), f.SubmissionId));
        var badReport = await f.Admin.GetAsync(Url(f.ProjectA, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, badProject.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, badReport.StatusCode);
        // رسالة الرفض ورمزه واحدان في الحالتين ⇒ لا يُفرَّق «غير موجود» عن «موجود وليس لك».
        // (المقارنة على الحقلين لا على الجسم كلّه: `instance` يحمل المسار المطلوب وهو يختلف بطبيعته.)
        Assert.Equal(await ProblemFingerprintAsync(badProject), await ProblemFingerprintAsync(badReport));
    }

    /// <summary>
    /// بصمة الرفض كما يراها المستخدم: الرسالة ورمز الخطأ فقط. حقل <c>instance</c> يحمل المسار
    /// المطلوب فيختلف بطبيعته بين النداءين، ومقارنة الجسم كلّه كانت ستفشل لسبب لا علاقة له بالتسريب.
    /// </summary>
    private static async Task<string> ProblemFingerprintAsync(HttpResponseMessage res)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        string? Get(string name) => doc.RootElement.TryGetProperty(name, out var v) ? v.GetString() : null;
        return $"{Get("title")}|{Get("detail")}|{Get("type")}";
    }

    // ===== 6: لا أثر لبيانات مشروع آخر في نصّ الاستجابة الخام =====
    [Fact]
    public async Task RawResponse_ContainsNoOtherProjectData()
    {
        var f = await BuildTwoProjectSubmissionAsync();

        var rawA = await (await f.Admin.GetAsync(Url(f.ProjectA, f.SubmissionId))).Content.ReadAsStringAsync();
        var rawB = await (await f.Admin.GetAsync(Url(f.ProjectB, f.SubmissionId))).Content.ReadAsStringAsync();

        Assert.Contains(MarkerA, rawA);
        Assert.DoesNotContain(MarkerB, rawA);
        Assert.DoesNotContain(f.ProjectB.ToString(), rawA);
        Assert.Contains(MarkerB, rawB);
        Assert.DoesNotContain(MarkerA, rawB);
        Assert.DoesNotContain(f.ProjectA.ToString(), rawB);
    }

    // ===== 7: الحقول غير المرتبطة بمشروع لا تخرج في الشريحة =====
    [Fact]
    public async Task NonProjectScopedFields_AreNotReturned()
    {
        var f = await BuildTwoProjectSubmissionAsync();

        var raw = await (await f.Admin.GetAsync(Url(f.ProjectA, f.SubmissionId))).Content.ReadAsStringAsync();

        // الملخّص العامّ لا رابط موثوقًا له بمشروع ⇒ لا يُنسَب لأيّ مشروع ولا يُعرَض في شريحته.
        Assert.DoesNotContain(GeneralNote, raw);
    }

    // ===== 8: صلاحيّات الإدارة القائمة لم تنكسر =====
    [Fact]
    public async Task ExistingProjectReportsListing_StillWorks()
    {
        var f = await BuildTwoProjectSubmissionAsync();

        var res = await f.Admin.GetAsync($"/api/projects/{f.ProjectA}/reports");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ===== 9: التقرير الكامل العامّ بقي على سياساته بلا توسيع =====
    [Fact]
    public async Task GeneralFullSubmission_PolicyUnchanged_ForOutOfScopeUser()
    {
        var f = await BuildTwoProjectSubmissionAsync();
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var res = await employee.GetAsync($"/api/submissions/{f.SubmissionId}");

        // المسار العامّ يظلّ يرفض من ليس طرفًا فيه — الشريحة لم تفتح له بابًا جديدًا.
        Assert.True(res.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
            $"المتوقَّع رفض المسار العامّ، والوارد {(int)res.StatusCode}.");
    }
}
