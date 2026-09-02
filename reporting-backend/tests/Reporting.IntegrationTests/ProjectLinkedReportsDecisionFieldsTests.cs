using System.Net.Http.Json;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// VIS-02ب — صفّ «التقارير المرتبطة» يحمل ما يكفي لاتّخاذ قرار من داخل مساحة المشروع.
///
/// <para><b>العطل الذي تحرسه</b>: <c>LinkedReportRow</c> كان تسعة حقول لا تُميّز تقرير
/// السيو من تقرير التصميم من تقرير الفيديو (لا اسم قالب)، ولا تقول كم بندَ عملٍ يخصّ
/// <b>هذا المشروع</b> تحديدًا، ولا ما آخر قرار اعتماد. مدير الحساب الذي يفتح مساحة العمل
/// كان يرى صفوفًا متطابقة الشكل فيضطرّ لفتح كلّ تقرير واحدًا واحدًا.</para>
///
/// <para><b>الادّعاء الحاسم هنا</b>: <c>WorkItemCount</c> <b>مقصور على المشروع</b> لا مجموع
/// التسليم. السيناريو يضع ثلاثة بنود في «أ» وبندين في «ب» داخل تسليم واحد: عدّاد كسول
/// يعيد 5 لكليهما، والعدّاد الصحيح يعيد 3 و2. لولا هذا التنافر لمرّ العيب صامتًا.</para>
/// </summary>
[Collection("Integration")]
public class ProjectLinkedReportsDecisionFieldsTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ProjectLinkedReportsDecisionFieldsTests(CustomWebApplicationFactory factory) => _factory = factory;

    private const string SectionConfigV2 =
        "{\"schemaVersion\":2,\"projectRequired\":true,\"minProjects\":1,\"maxProjects\":0," +
        "\"fields\":[{\"key\":\"work_status\",\"label\":\"حالة العمل\",\"type\":\"SingleSelect\",\"required\":true}]," +
        "\"workItems\":{\"key\":\"work_items\",\"label\":\"بنود العمل\",\"itemLabel\":\"بند عمل\"," +
        "\"addLabel\":\"+ إضافة بند عمل\",\"minItems\":1,\"maxItems\":0,\"uniqueBy\":[]," +
        "\"fields\":[{\"key\":\"work_type\",\"label\":\"نوع العمل\",\"type\":\"SingleSelect\",\"required\":true}]}}";

    // مخطّط v1 لا يعرف بنود العمل: بطاقة المشروع نفسها هي البند الواحد.
    private const string SectionConfigV1 =
        "{\"projectRequired\":true,\"minProjects\":1,\"maxProjects\":5," +
        "\"fields\":[{\"key\":\"work_type\",\"label\":\"نوع العمل\",\"type\":\"SingleSelect\",\"required\":true}]}";

    private sealed record Fixture(Guid ProjectA, Guid ProjectB, Guid SubmissionId, string TemplateTitle, HttpClient Admin);

    private static string EntryV2(Guid projectId, int itemCount)
    {
        var items = string.Join(",", Enumerable.Range(1, itemCount)
            .Select(i => $"{{\"answers\":{{\"work_type\":\"بند-{i}\"}}}}"));
        return $"{{\"projectId\":\"{projectId}\",\"answers\":{{\"work_status\":\"مكتمل\"}},\"workItems\":[{items}]}}";
    }

    private static string EntryV1(Guid projectId)
        => $"{{\"projectId\":\"{projectId}\",\"answers\":{{\"work_type\":\"بند وحيد\"}}}}";

    private static string List(Guid projectId) => $"/api/projects/{projectId}/reports";

    /// <summary>ثلاثة بنود في «أ» وبندان في «ب» داخل تسليم واحد وقالب معلوم العنوان.</summary>
    private async Task<Fixture> BuildAsync(string sectionConfig, Func<Guid, Guid, string> entriesJson)
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var title = $"قالب قرار {Guid.NewGuid():N}";

        var created = (await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest(title, null, null, PeriodType.Weekly, TemplateClassification.Supplementary)))
            .ReadAsync<ReportTemplateDetailDto>())!;
        var versionId = created.Versions.Single().Id;

        var section = (await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("تفاصيل المشاريع", "projects", FieldType.ProjectRepeatableSection, true, null, sectionConfig)))
            .ReadAsync<TemplateFieldDto>())!;

        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);

        var client = (await (await admin.PostAsJsonAsync("/api/clients",
            new CreateClientRequest($"عميل قرار {Guid.NewGuid():N}", null))).ReadAsync<ClientDto>())!;
        var a = (await (await admin.PostAsJsonAsync("/api/projects",
            new CreateProjectRequest(client.Id, $"مشروع أ {Guid.NewGuid():N}", ServiceType.Social))).ReadAsync<ProjectDto>())!;
        var b = (await (await admin.PostAsJsonAsync("/api/projects",
            new CreateProjectRequest(client.Id, $"مشروع ب {Guid.NewGuid():N}", ServiceType.Social))).ReadAsync<ProjectDto>())!;

        var draft = (await (await admin.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(created.Id, PeriodType.Weekly,
                ReportingCalendarPolicy.CycleKeyFor(ReportingCalendarPolicy.RiyadhToday()))))
            .ReadAsync<SubmissionDto>())!;

        var save = await admin.PutAsJsonAsync($"/api/submissions/{draft.Id}/values", new SaveFieldValuesRequest(new[]
        {
            new FieldValueInput(section.Id, null, null, null, null, entriesJson(a.Id, b.Id)),
        }));
        Assert.True(save.IsSuccessStatusCode);

        return new Fixture(a.Id, b.Id, draft.Id, title, admin);
    }

    private static async Task<LinkedReportRow> RowAsync(Fixture f, Guid projectId)
    {
        var rows = await (await f.Admin.GetAsync(List(projectId))).ReadAsync<List<LinkedReportRow>>();
        return Assert.Single(rows!.Where(r => r.SubmissionId == f.SubmissionId));
    }

    // ===== (1) عدّ بنود العمل مقصور على المشروع لا مجموع التسليم =====
    [Fact]
    public async Task WorkItemCount_IsScopedToProject_NotSubmissionTotal()
    {
        var f = await BuildAsync(SectionConfigV2, (a, b) => "[" + EntryV2(a, 3) + "," + EntryV2(b, 2) + "]");

        Assert.Equal(3, (await RowAsync(f, f.ProjectA)).WorkItemCount);
        Assert.Equal(2, (await RowAsync(f, f.ProjectB)).WorkItemCount);
    }

    // ===== (2) مخطّط v1 بلا بنود: بطاقة المشروع نفسها تُحسَب بندًا واحدًا =====
    //
    // التقارير القديمة لا يجوز أن تظهر بعدّاد صفر — الصفر يقرأ «لا عمل» وهو خطأ وصفيّ،
    // بينما الواقع أنّ نموذج ذلك الإصدار لم يكن يفصّل البنود أصلًا.
    [Fact]
    public async Task LegacyV1Entries_CountAsOneItemPerProjectCard()
    {
        var f = await BuildAsync(SectionConfigV1, (a, b) => "[" + EntryV1(a) + "," + EntryV1(b) + "]");

        Assert.Equal(1, (await RowAsync(f, f.ProjectA)).WorkItemCount);
        Assert.Equal(1, (await RowAsync(f, f.ProjectB)).WorkItemCount);
    }

    // ===== (3) اسم القالب حاضر في الصفّ =====
    [Fact]
    public async Task TemplateName_IsPresent_SoRowsAreDistinguishable()
    {
        var f = await BuildAsync(SectionConfigV2, (a, b) => "[" + EntryV2(a, 1) + "," + EntryV2(b, 1) + "]");

        Assert.Equal(f.TemplateTitle, (await RowAsync(f, f.ProjectA)).TemplateName);
    }

    // ===== (4) آخر تحديث موجود، وحقول القرار خالية ما لم يُتَّخذ قرار =====
    //
    // النصف السالب مقصود: صفّ يعرض قرارًا لم يقع أسوأ من صفّ لا يعرض شيئًا.
    [Fact]
    public async Task LastUpdated_IsSet_AndDecisionFieldsStayEmptyBeforeAnyDecision()
    {
        var f = await BuildAsync(SectionConfigV2, (a, b) => "[" + EntryV2(a, 1) + "," + EntryV2(b, 1) + "]");
        var row = await RowAsync(f, f.ProjectA);

        Assert.NotNull(row.LastUpdatedAtUtc);
        Assert.Null(row.LastDecision);
        Assert.Null(row.LastDecisionAtUtc);
        Assert.Null(row.LastReturnReason);
    }

    // ===== (5) المواضع التسعة الأصليّة لم تتغيّر (توافق خلفيّ للمستهلكين القائمين) =====
    [Fact]
    public async Task OriginalNineMembers_RemainPopulated()
    {
        var f = await BuildAsync(SectionConfigV2, (a, b) => "[" + EntryV2(a, 1) + "," + EntryV2(b, 1) + "]");
        var row = await RowAsync(f, f.ProjectA);

        Assert.Equal(f.SubmissionId, row.SubmissionId);
        Assert.NotEqual(Guid.Empty, row.SubmitterId);
        Assert.False(string.IsNullOrWhiteSpace(row.SubmitterName));
        Assert.Equal(PeriodType.Weekly, row.PeriodType);
        Assert.False(string.IsNullOrWhiteSpace(row.PeriodKey));
        Assert.Equal(SubmissionStatus.Draft, row.Status);
    }
}
