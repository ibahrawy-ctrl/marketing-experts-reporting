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
/// بنود العمل المتعدّدة داخل المشروع الواحد + اكتشاف تقارير المشروع من الارتباط المتداخل
/// (PROJECT360-MULTI-WORK-ITEMS-AND-REPORT-DISCOVERY-CLOSURE-R2).
///
/// <para><b>العطل الذي تحرسه</b>: قائمة تقارير المشروع كانت تُبنى على <c>ReportSubmissions.ProjectId</c>
/// وحده — عمود شبه مهجور (مملوء في تسليمَين من 311 على الإنتاج) — بينما الارتباط الحقيقيّ يعيش داخل
/// <c>ValueJson</c> لقسم المشاريع المتكرّر. النتيجة: التقرير يُفتَح بشريحته إن عرفتَ رابطه، لكنّه
/// لا يظهر في قائمة تقارير مشروعه أصلًا.</para>
///
/// <para><b>والعطل الثاني</b>: المشروع لا يُقبل مرّتين في التقرير الواحد (حارس تفرّد صحيح ومقصود)،
/// فكان تسجيل «كاروسيل + بوست ثابت + ريل» داخل نفس المشروع مستحيلًا. العلاج: بنود عمل متداخلة
/// داخل بطاقة المشروع، لا تكرار للمشروع.</para>
/// </summary>
[Collection("Integration")]
public class ProjectMultiWorkItemDiscoveryTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ProjectMultiWorkItemDiscoveryTests(CustomWebApplicationFactory factory) => _factory = factory;

    // بصمات فريدة: ظهور بصمة مشروع داخل استجابة المشروع الآخر = تسريب مثبت.
    private const string ItemA1 = "كاروسيل-A1-Z7Q";
    private const string ItemA2 = "بوست-ثابت-A2-Z7Q";
    private const string ItemA3 = "ريل-A3-Z7Q";
    private const string ItemB1 = "مقال-B1-Z7Q";
    private const string ItemB2 = "مهمّة-سيو-B2-Z7Q";

    // مخطّط v2: حقول مستوى المشروع + مجموعة بنود عمل معرَّفة بالقالب بالكامل.
    private const string SectionConfigV2 =
        "{\"schemaVersion\":2,\"projectRequired\":true,\"minProjects\":1,\"maxProjects\":0," +
        "\"fields\":[{\"key\":\"work_status\",\"label\":\"حالة العمل\",\"type\":\"SingleSelect\",\"required\":true}]," +
        "\"workItems\":{\"key\":\"work_items\",\"label\":\"بنود العمل\",\"itemLabel\":\"بند عمل\"," +
        "\"addLabel\":\"+ إضافة بند عمل\",\"minItems\":1,\"maxItems\":0,\"uniqueBy\":[]," +
        "\"fields\":[{\"key\":\"work_type\",\"label\":\"نوع العمل\",\"type\":\"SingleSelect\",\"required\":true}," +
        "{\"key\":\"qty\",\"label\":\"العدد\",\"type\":\"Number\",\"required\":false,\"min\":1,\"integerOnly\":true}]}}";

    // مخطّط v1 حرفيًّا كما هو في الإنتاج — لإثبات أنّ التقارير القديمة لم تتغيّر.
    private const string SectionConfigV1 =
        "{\"projectRequired\":true,\"minProjects\":1,\"maxProjects\":5," +
        "\"fields\":[{\"key\":\"work_type\",\"label\":\"نوع العمل\",\"type\":\"SingleSelect\",\"required\":true}]}";

    private sealed record Fixture(
        Guid ProjectA, Guid ProjectB, Guid SubmissionId, Guid SectionFieldId, HttpClient Admin);

    /// <summary>
    /// السيناريو المطلوب في التذكرة حرفيًّا: مشروع أ بثلاثة بنود عمل، مشروع ب ببندين،
    /// داخل تسليم واحد وبطاقة مشروع واحدة لكلٍّ منهما (بلا أيّ تكرار للمشروع).
    /// </summary>
    private async Task<Fixture> BuildAbScenarioAsync(string sectionConfig = SectionConfigV2, bool useWorkItems = true)
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);

        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب بنود {Guid.NewGuid():N}", null, null, PeriodType.Weekly,
                TemplateClassification.Supplementary)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;

        var section = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("تفاصيل المشاريع", "projects", FieldType.ProjectRepeatableSection, true, null, sectionConfig)))
            .ReadAsync<TemplateFieldDto>();

        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);

        var client = (await (await admin.PostAsJsonAsync("/api/clients",
            new CreateClientRequest($"عميل بنود {Guid.NewGuid():N}", null))).ReadAsync<ClientDto>())!;
        var a = (await (await admin.PostAsJsonAsync("/api/projects",
            new CreateProjectRequest(client.Id, $"مشروع أ {Guid.NewGuid():N}", ServiceType.Social))).ReadAsync<ProjectDto>())!;
        var b = (await (await admin.PostAsJsonAsync("/api/projects",
            new CreateProjectRequest(client.Id, $"مشروع ب {Guid.NewGuid():N}", ServiceType.Social))).ReadAsync<ProjectDto>())!;

        var draft = (await (await admin.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(created.Id, PeriodType.Weekly,
                ReportingCalendarPolicy.CycleKeyFor(ReportingCalendarPolicy.RiyadhToday()))))
            .ReadAsync<SubmissionDto>())!;

        var json = useWorkItems
            ? "[" + EntryV2(a.Id, ItemA1, ItemA2, ItemA3) + "," + EntryV2(b.Id, ItemB1, ItemB2) + "]"
            : "[" + EntryV1(a.Id, ItemA1) + "," + EntryV1(b.Id, ItemB1) + "]";

        var save = await admin.PutAsJsonAsync($"/api/submissions/{draft.Id}/values", new SaveFieldValuesRequest(new[]
        {
            new FieldValueInput(section!.Id, null, null, null, null, json),
        }));
        Assert.True(save.IsSuccessStatusCode);

        return new Fixture(a.Id, b.Id, draft.Id, section.Id, admin);
    }

    private static string EntryV2(Guid projectId, params string[] workTypes)
    {
        var items = string.Join(",", workTypes.Select(t => $"{{\"answers\":{{\"work_type\":\"{t}\",\"qty\":2}}}}"));
        return $"{{\"projectId\":\"{projectId}\",\"answers\":{{\"work_status\":\"مكتمل\"}},\"workItems\":[{items}]}}";
    }

    private static string EntryV1(Guid projectId, string workType)
        => $"{{\"projectId\":\"{projectId}\",\"answers\":{{\"work_type\":\"{workType}\"}}}}";

    private static string Slice(Guid projectId, Guid submissionId) => $"/api/projects/{projectId}/reports/{submissionId}";
    private static string List(Guid projectId) => $"/api/projects/{projectId}/reports";

    // ===== 1: الاكتشاف من الارتباط المتداخل — التقرير يظهر في قائمتَي المشروعين =====
    [Fact]
    public async Task NestedLinkage_ReportAppearsInBothProjectLists()
    {
        var f = await BuildAbScenarioAsync();

        var listA = await (await f.Admin.GetAsync(List(f.ProjectA))).ReadAsync<List<LinkedReportRow>>();
        var listB = await (await f.Admin.GetAsync(List(f.ProjectB))).ReadAsync<List<LinkedReportRow>>();

        Assert.Contains(listA!, r => r.SubmissionId == f.SubmissionId);
        Assert.Contains(listB!, r => r.SubmissionId == f.SubmissionId);
    }

    // ===== 2: صفّ واحد لكلّ تسليم مهما تعدّدت مواضع الارتباط (لا تكرار) =====
    [Fact]
    public async Task NestedLinkage_NoDuplicateRowsForSameSubmission()
    {
        var f = await BuildAbScenarioAsync();

        var listA = await (await f.Admin.GetAsync(List(f.ProjectA))).ReadAsync<List<LinkedReportRow>>();

        Assert.Single(listA!, r => r.SubmissionId == f.SubmissionId);
    }

    // ===== 3: عدّاد الملخّص يوافق القائمة تحته =====
    [Fact]
    public async Task Summary_CountsNestedLinkedReports()
    {
        var f = await BuildAbScenarioAsync();

        var summary = await (await f.Admin.GetAsync($"/api/projects/{f.ProjectA}/summary")).ReadAsync<ProjectSummaryDto>();

        Assert.True(summary!.TotalReports >= 1);
    }

    // ===== 4: بنود العمل تُعاد كاملة للمشروع المطلوب وحده =====
    [Fact]
    public async Task Slice_ReturnsAllWorkItemsOfRequestedProjectOnly()
    {
        var f = await BuildAbScenarioAsync();

        var res = await f.Admin.GetAsync(Slice(f.ProjectA, f.SubmissionId));
        var body = await res.Content.ReadAsStringAsync();
        var slice = await res.ReadAsync<ProjectReportSliceDto>();

        var entry = Assert.Single(Assert.Single(slice!.Fields).Entries);
        Assert.Equal(3, entry.WorkItems.Count);
        Assert.Contains(entry.WorkItems, i => i["work_type"] == ItemA1);
        Assert.Contains(entry.WorkItems, i => i["work_type"] == ItemA2);
        Assert.Contains(entry.WorkItems, i => i["work_type"] == ItemA3);
        // الفحص على النصّ الخام: الإخفاء في الواجهة يمرّ من فحص الكائن، لا من فحص الشبكة.
        Assert.DoesNotContain(ItemB1, body);
        Assert.DoesNotContain(ItemB2, body);
    }

    // ===== 5: الشريحة المقابلة لا تحمل شيئًا من الأولى =====
    [Fact]
    public async Task Slice_OppositeProjectLeaksNothing()
    {
        var f = await BuildAbScenarioAsync();

        var res = await f.Admin.GetAsync(Slice(f.ProjectB, f.SubmissionId));
        var body = await res.Content.ReadAsStringAsync();
        var slice = await res.ReadAsync<ProjectReportSliceDto>();

        var entry = Assert.Single(Assert.Single(slice!.Fields).Entries);
        Assert.Equal(2, entry.WorkItems.Count);
        Assert.DoesNotContain(ItemA1, body);
        Assert.DoesNotContain(ItemA2, body);
        Assert.DoesNotContain(ItemA3, body);
    }

    // ===== 6: نوع عمل مكرّر داخل المشروع مسموح ما دام لا قيد تفرّد في القالب =====
    [Fact]
    public async Task SameWorkTypeTwice_IsAcceptedWhenTemplateDeclaresNoUniqueness()
    {
        var f = await BuildAbScenarioAsync();

        var json = "[" + EntryV2(f.ProjectA, ItemA1, ItemA1) + "]";
        await f.Admin.PutAsJsonAsync($"/api/submissions/{f.SubmissionId}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(f.SectionFieldId, null, null, null, null, json) }));

        var res = await f.Admin.PostAsync($"/api/submissions/{f.SubmissionId}/submit", null);

        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
    }

    // ===== 7: تكرار بطاقة المشروع يبقى مرفوضًا (الحارس الصحيح لم يُمسّ) =====
    [Fact]
    public async Task DuplicateProjectCard_StillRejected()
    {
        var f = await BuildAbScenarioAsync();

        var json = "[" + EntryV2(f.ProjectA, ItemA1) + "," + EntryV2(f.ProjectA, ItemA2) + "]";
        await f.Admin.PutAsJsonAsync($"/api/submissions/{f.SubmissionId}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(f.SectionFieldId, null, null, null, null, json) }));

        var res = await f.Admin.PostAsync($"/api/submissions/{f.SubmissionId}/submit", null);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("لا يمكن تكرار نفس المشروع", await res.Content.ReadAsStringAsync());
    }

    // ===== 8: حقل مطلوب داخل بند العمل يُفرَض خادميًّا =====
    [Fact]
    public async Task MissingRequiredWorkItemField_Rejected()
    {
        var f = await BuildAbScenarioAsync();

        var json = $"[{{\"projectId\":\"{f.ProjectA}\",\"answers\":{{\"work_status\":\"مكتمل\"}}," +
                   "\"workItems\":[{\"answers\":{\"qty\":3}}]}]";
        await f.Admin.PutAsJsonAsync($"/api/submissions/{f.SubmissionId}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(f.SectionFieldId, null, null, null, null, json) }));

        var res = await f.Admin.PostAsync($"/api/submissions/{f.SubmissionId}/submit", null);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("«نوع العمل» مطلوب", await res.Content.ReadAsStringAsync());
    }

    // ===== 9: الحدّ الأدنى لبنود العمل يُفرَض =====
    [Fact]
    public async Task EmptyWorkItems_RejectedWhenMinItemsIsOne()
    {
        var f = await BuildAbScenarioAsync();

        var json = $"[{{\"projectId\":\"{f.ProjectA}\",\"answers\":{{\"work_status\":\"مكتمل\"}},\"workItems\":[]}}]";
        await f.Admin.PutAsJsonAsync($"/api/submissions/{f.SubmissionId}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(f.SectionFieldId, null, null, null, null, json) }));

        var res = await f.Admin.PostAsync($"/api/submissions/{f.SubmissionId}/submit", null);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ===== 10: القيود الرقميّة داخل بند العمل مفروضة بنفس مصدر الحقيقة =====
    [Fact]
    public async Task WorkItemNumericConstraint_Enforced()
    {
        var f = await BuildAbScenarioAsync();

        var json = $"[{{\"projectId\":\"{f.ProjectA}\",\"answers\":{{\"work_status\":\"مكتمل\"}}," +
                   $"\"workItems\":[{{\"answers\":{{\"work_type\":\"{ItemA1}\",\"qty\":\"0\"}}}}]}}]";
        await f.Admin.PutAsJsonAsync($"/api/submissions/{f.SubmissionId}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(f.SectionFieldId, null, null, null, null, json) }));

        var res = await f.Admin.PostAsync($"/api/submissions/{f.SubmissionId}/submit", null);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains(RepeatableNumericValidation.BelowMin, await res.Content.ReadAsStringAsync());
    }

    // ===== 11: بيانات v1 داخل قالب v1 تبقى كما هي بلا أيّ بند ضمنيّ =====
    [Fact]
    public async Task LegacyV1Data_RemainsFlatWithNoImplicitWorkItem()
    {
        var f = await BuildAbScenarioAsync(SectionConfigV1, useWorkItems: false);

        var slice = await (await f.Admin.GetAsync(Slice(f.ProjectA, f.SubmissionId))).ReadAsync<ProjectReportSliceDto>();

        var entry = Assert.Single(Assert.Single(slice!.Fields).Entries);
        Assert.Equal(ItemA1, entry.Answers["work_type"]);
        Assert.Empty(entry.WorkItems);
    }

    // ===== 12: بيانات v1 داخل قالب أعلن بنود عمل ⇒ بند ضمنيّ واحد للعرض بلا كتابة =====
    [Fact]
    public async Task LegacyV1Data_UnderV2Template_ShownAsSingleImplicitWorkItem()
    {
        var f = await BuildAbScenarioAsync(SectionConfigV2, useWorkItems: false);

        var slice = await (await f.Admin.GetAsync(Slice(f.ProjectA, f.SubmissionId))).ReadAsync<ProjectReportSliceDto>();
        var entry = Assert.Single(Assert.Single(slice!.Fields).Entries);
        Assert.Single(entry.WorkItems);
        Assert.Equal(ItemA1, entry.WorkItems[0]["work_type"]);

        // البيانات المخزَّنة لم تُمسّ: القراءة لا تكتب.
        var values = await (await f.Admin.GetAsync($"/api/submissions/{f.SubmissionId}")).ReadAsync<SubmissionDto>();
        var stored = values!.FieldValues.Single(v => v.TemplateFieldId == f.SectionFieldId).ValueJson;
        Assert.DoesNotContain("workItems", stored);
    }

    // ===== 13: خارج النطاق لا يظهر في القائمة ولا يفتح =====
    [Fact]
    public async Task OutOfScopeUser_SeesNeitherListNorSlice()
    {
        var f = await BuildAbScenarioAsync();
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var list = await employee.GetAsync(List(f.ProjectA));
        var slice = await employee.GetAsync(Slice(f.ProjectA, f.SubmissionId));

        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, slice.StatusCode);
    }
}
