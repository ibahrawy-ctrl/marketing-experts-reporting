using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Application.Reports;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// OFFICIAL-LAUNCH-FIX-PACK-R1B — «تبويبات المشروع/العميل — آمن للـ KPI — مدخلات جدولية».
/// يثبت أن الحقول الفرعية من نوع Grid داخل ProjectRepeatableSection تُخزَّن وتُقرأ ذهابًا وإيابًا
/// عبر ValueJson دون Migration ودون تغيير مسار ValueNumber أو تجميع KPI (الأرقام تبقى حقولًا عليا).
/// جدول البيانات المرتبطة بمشروع = صفوف×أعمدة في خلية واحدة (لا حقول مكررة).
/// </summary>
[Collection("Integration")]
public class ProjectRepeatableGridTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ProjectRepeatableGridTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static readonly JsonSerializerOptions J = new() { PropertyNameCaseInsensitive = true };

    // ===== أدوات بناء ConfigJson للقسم المتكرر مع حقول فرعية (منها Grid) =====
    private static string SectionConfig(bool projectRequired, int min, int max, params object[] fields)
        => JsonSerializer.Serialize(new
        {
            projectRequired,
            minProjects = min,
            maxProjects = max,
            fields,
        });

    private static object GridField(string key, string label, bool required, params string[] columns)
        => new { key, label, type = "Grid", required, columns };

    private static object TextField(string key, string label, bool required)
        => new { key, label, type = "ShortText", required, columns = (string[]?)null };

    // القيمة داخل خلية Grid = سلسلة JSON مُسلسلة لـ string[][].
    private static string GridCell(params string[][] rows) => JsonSerializer.Serialize(rows);

    // بناء ValueJson للقسم = قائمة {projectId, answers}.
    private static string SectionValue(params (Guid? ProjectId, Dictionary<string, string> Answers)[] entries)
        => JsonSerializer.Serialize(entries.Select(e => new { projectId = e.ProjectId, answers = e.Answers }).ToArray());

    private static async Task<(Guid TemplateId, Guid FieldId)> PublishSectionAsync(HttpClient admin, string configJson, bool required = true)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب R1B {Guid.NewGuid():N}", null, null, PeriodType.Weekly,
                TemplateClassification.Supplementary)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        var field = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("تفاصيل المشاريع", "projects", FieldType.ProjectRepeatableSection, required, null, configJson)))
            .ReadAsync<TemplateFieldDto>();
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return (created.Id, field!.Id);
    }

    private static async Task<Guid> CreateDraftAsync(HttpClient c, Guid templateId, string periodKey)
        => (await (await c.PostAsJsonAsync("/api/submissions",
                new CreateSubmissionRequest(templateId, PeriodType.Weekly, periodKey))).ReadAsync<SubmissionDto>())!.Id;

    private static Task SaveValuesAsync(HttpClient c, Guid submissionId, params FieldValueInput[] values)
        => c.PutAsJsonAsync($"/api/submissions/{submissionId}/values", new SaveFieldValuesRequest(values));

    private static async Task<ProjectDto> CreateProjectAsync(HttpClient admin, string name)
    {
        var client = (await (await admin.PostAsJsonAsync("/api/clients",
            new CreateClientRequest($"عميل {Guid.NewGuid():N}", null))).ReadAsync<ClientDto>())!;
        return (await (await admin.PostAsJsonAsync("/api/projects",
            new CreateProjectRequest(client.Id, name, ServiceType.Seo))).ReadAsync<ProjectDto>())!;
    }

    private static async Task<SubmissionDto> GetAsync(HttpClient c, Guid id)
        => (await (await c.GetAsync($"/api/submissions/{id}")).ReadAsync<SubmissionDto>())!;

    private static string[][] ReadGrid(SubmissionDto sub, Guid fieldId, Guid projectId, string key)
    {
        var val = sub.FieldValues.First(v => v.TemplateFieldId == fieldId).ValueJson!;
        var entries = JsonSerializer.Deserialize<List<JsonElement>>(val, J)!;
        var entry = entries.First(e => e.GetProperty("projectId").GetGuid() == projectId);
        var cell = entry.GetProperty("answers").GetProperty(key).GetString()!;
        return JsonSerializer.Deserialize<string[][]>(cell, J)!;
    }

    // ===== 1: ذهاب/إياب Grid داخل قسم مشروع — تُحفَظ وتُقرأ الصفوف كما هي =====
    [Fact]
    public async Task Grid_RoundTrip_PersistsAndReadsBack()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var config = SectionConfig(true, 1, 5, GridField("rows", "جدول", false, "أ", "ب", "ج"));
        var (templateId, fieldId) = await PublishSectionAsync(admin, config);
        var project = await CreateProjectAsync(admin, "مشروع 1");

        var grid = GridCell(new[] { "x1", "y1", "z1" }, new[] { "x2", "y2", "z2" });
        var draftId = await CreateDraftAsync(admin, templateId, TestCalendar.Cycle(1));
        await SaveValuesAsync(admin, draftId,
            new FieldValueInput(fieldId, null, null, null, null,
                SectionValue((project.Id, new() { ["rows"] = grid }))));
        await admin.PostAsync($"/api/submissions/{draftId}/submit", null);

        var back = ReadGrid(await GetAsync(admin, draftId), fieldId, project.Id, "rows");
        Assert.Equal(2, back.Length);
        Assert.Equal(new[] { "x1", "y1", "z1" }, back[0]);
        Assert.Equal(new[] { "x2", "y2", "z2" }, back[1]);
    }

    // ===== 2: كلمة SEO المفتاحية + Position + Impressions + Clicks + CTR في صف واحد =====
    [Fact]
    public async Task Seo_Keyword_AllMetrics_LiveInSameRow()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var config = SectionConfig(true, 1, 10, GridField("keywords", "كلمات المشروع", false,
            "الكلمة المفتاحية", "الصفحة المستهدفة", "Position", "Impressions", "Clicks", "CTR", "التغيّر", "ملاحظة"));
        var (templateId, fieldId) = await PublishSectionAsync(admin, config);
        var project = await CreateProjectAsync(admin, "مشروع SEO");

        var row = new[] { "خدمات تسويق", "/services", "3", "1200", "84", "7%", "▲", "ثابت" };
        var draftId = await CreateDraftAsync(admin, templateId, TestCalendar.Cycle(2));
        await SaveValuesAsync(admin, draftId,
            new FieldValueInput(fieldId, null, null, null, null,
                SectionValue((project.Id, new() { ["keywords"] = GridCell(row) }))));
        await admin.PostAsync($"/api/submissions/{draftId}/submit", null);

        var back = ReadGrid(await GetAsync(admin, draftId), fieldId, project.Id, "keywords");
        Assert.Single(back);
        // كل مقاييس الكلمة في نفس الصف (لا حقول keyword1/position1 منفصلة).
        Assert.Equal(row, back[0]);
        Assert.Equal("3", back[0][2]);
        Assert.Equal("1200", back[0][3]);
        Assert.Equal("7%", back[0][5]);
    }

    // ===== 3: حملات Media Buyer كجدول واحد =====
    [Fact]
    public async Task MediaBuyer_Campaigns_Grid_RoundTrip()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var config = SectionConfig(true, 1, 10, GridField("campaigns", "حملات المشروع", false,
            "اسم الحملة", "المنصة", "الهدف", "الإنفاق", "النتيجة", "الحالة", "الإجراء التالي"));
        var (templateId, fieldId) = await PublishSectionAsync(admin, config);
        var project = await CreateProjectAsync(admin, "مشروع حملات");

        var cell = GridCell(
            new[] { "حملة رمضان", "Meta", "تحويلات", "5000", "120 عميل", "نشطة", "زيادة الميزانية" },
            new[] { "حملة بحث", "Google", "زيارات", "3000", "900 نقرة", "متوقفة", "مراجعة" });
        var draftId = await CreateDraftAsync(admin, templateId, TestCalendar.Cycle(3));
        await SaveValuesAsync(admin, draftId,
            new FieldValueInput(fieldId, null, null, null, null,
                SectionValue((project.Id, new() { ["campaigns"] = cell }))));
        await admin.PostAsync($"/api/submissions/{draftId}/submit", null);

        var back = ReadGrid(await GetAsync(admin, draftId), fieldId, project.Id, "campaigns");
        Assert.Equal(2, back.Length);
        Assert.Equal("Meta", back[0][1]);
        Assert.Equal("متوقفة", back[1][5]);
    }

    // ===== 4: مقالات كجدول واحد =====
    [Fact]
    public async Task Articles_Grid_RoundTrip()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var config = SectionConfig(true, 1, 10, GridField("articles", "مقالات المشروع", false,
            "عنوان المقال", "الكلمة المفتاحية", "الحالة", "المراجع", "تاريخ التسليم", "ملاحظات"));
        var (templateId, fieldId) = await PublishSectionAsync(admin, config);
        var project = await CreateProjectAsync(admin, "مشروع مقالات");

        var cell = GridCell(new[] { "دليل SEO", "سيو", "منشور", "سارة", TestCalendar.Day(0), "جيد" });
        var draftId = await CreateDraftAsync(admin, templateId, TestCalendar.Cycle(4));
        await SaveValuesAsync(admin, draftId,
            new FieldValueInput(fieldId, null, null, null, null,
                SectionValue((project.Id, new() { ["articles"] = cell }))));
        await admin.PostAsync($"/api/submissions/{draftId}/submit", null);

        var back = ReadGrid(await GetAsync(admin, draftId), fieldId, project.Id, "articles");
        Assert.Single(back);
        Assert.Equal("منشور", back[0][2]);
        Assert.Equal("سارة", back[0][3]);
    }

    // ===== 5: تحويل كامل لمدير الحسابات (سماح) — قسم عميل بحقول نصية + Grid، بلا رقم علوي =====
    [Fact]
    public async Task AccountManager_FullClientSection_RoundTrip()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var config = SectionConfig(true, 1, 20,
            TextField("account_status", "حالة الحساب", true),
            GridField("actions", "الإجراءات المطلوبة", false, "الإجراء", "المسؤول"));
        var (templateId, fieldId) = await PublishSectionAsync(admin, config);
        var project = await CreateProjectAsync(admin, "عميل سماح");

        var actions = GridCell(new[] { "تجديد العقد", "سماح" }, new[] { "اجتماع مراجعة", "المدير" });
        var draftId = await CreateDraftAsync(admin, templateId, TestCalendar.Cycle(5));
        await SaveValuesAsync(admin, draftId,
            new FieldValueInput(fieldId, null, null, null, null,
                SectionValue((project.Id, new() { ["account_status"] = "🟢 ممتازة", ["actions"] = actions }))));
        var submitted = await (await admin.PostAsync($"/api/submissions/{draftId}/submit", null)).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Closed, submitted!.Status);

        var sub = await GetAsync(admin, draftId);
        var entries = JsonSerializer.Deserialize<List<JsonElement>>(
            sub.FieldValues.First(v => v.TemplateFieldId == fieldId).ValueJson!, J)!;
        Assert.Equal("🟢 ممتازة", entries[0].GetProperty("answers").GetProperty("account_status").GetString());
        var back = ReadGrid(sub, fieldId, project.Id, "actions");
        Assert.Equal(2, back.Length);
    }

    // ===== 6: هجين (شيري) — حقل علوي عام + قسم مشروع يتعايشان في نفس القالب =====
    [Fact]
    public async Task Hybrid_TopLevelField_And_ProjectSection_Coexist()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب هجين {Guid.NewGuid():N}", null, null, PeriodType.Weekly,
                TemplateClassification.Supplementary))).ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;

        // حقل أكاديمية علوي (يبقى خارج المشاريع).
        var academy = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("عدد المتدربين", "trainees", FieldType.Number, false, null, null)))
            .ReadAsync<TemplateFieldDto>();
        var section = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("تفاصيل المشاريع", "projects", FieldType.ProjectRepeatableSection, false, null,
                SectionConfig(true, 0, 10, GridField("rows", "جدول", false, "أ", "ب")))))
            .ReadAsync<TemplateFieldDto>();
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        var project = await CreateProjectAsync(admin, "مشروع هجين");

        var draftId = await CreateDraftAsync(admin, created.Id, TestCalendar.Cycle(6));
        await SaveValuesAsync(admin, draftId,
            new FieldValueInput(academy!.Id, null, 42m, null, null, null),
            new FieldValueInput(section!.Id, null, null, null, null,
                SectionValue((project.Id, new() { ["rows"] = GridCell(new[] { "a", "b" }) }))));
        await admin.PostAsync($"/api/submissions/{draftId}/submit", null);

        var sub = await GetAsync(admin, draftId);
        // الرقم الأكاديمي بقي علويًا في ValueNumber (خارج المشاريع).
        Assert.Equal(42m, sub.FieldValues.First(v => v.TemplateFieldId == academy.Id).ValueNumber);
        Assert.Single(ReadGrid(sub, section.Id, project.Id, "rows"));
    }

    // ===== 7: ValueNumber لحقل علوي لا يتأثّر بوجود قسم Grid =====
    [Fact]
    public async Task TopLevel_ValueNumber_Unaffected_ByGridSection()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب رقم {Guid.NewGuid():N}", null, null, PeriodType.Weekly,
                TemplateClassification.Supplementary))).ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        var num = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("الإنفاق", "spend", FieldType.Currency, true, null, null)))
            .ReadAsync<TemplateFieldDto>();
        var section = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("تفاصيل المشاريع", "projects", FieldType.ProjectRepeatableSection, false, null,
                SectionConfig(true, 0, 10, GridField("rows", "جدول", false, "أ")))))
            .ReadAsync<TemplateFieldDto>();
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        var project = await CreateProjectAsync(admin, "مشروع رقم");

        var draftId = await CreateDraftAsync(admin, created.Id, TestCalendar.Cycle(7));
        await SaveValuesAsync(admin, draftId,
            new FieldValueInput(num!.Id, null, 999.5m, null, null, null),
            new FieldValueInput(section!.Id, null, null, null, null,
                SectionValue((project.Id, new() { ["rows"] = GridCell(new[] { "z" }) }))));
        await admin.PostAsync($"/api/submissions/{draftId}/submit", null);

        var sub = await GetAsync(admin, draftId);
        var spend = sub.FieldValues.First(v => v.TemplateFieldId == num.Id);
        Assert.Equal(999.5m, spend.ValueNumber);
        Assert.Null(spend.ValueJson);
    }

    // ===== 8: جدول فارغ لحقل غير مطلوب → لا يمنع الإرسال =====
    [Fact]
    public async Task EmptyGrid_WhenNotRequired_Succeeds()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var config = SectionConfig(true, 1, 5, GridField("rows", "جدول", false, "أ", "ب"));
        var (templateId, fieldId) = await PublishSectionAsync(admin, config);
        var project = await CreateProjectAsync(admin, "مشروع فارغ");

        var draftId = await CreateDraftAsync(admin, templateId, TestCalendar.Cycle(8));
        await SaveValuesAsync(admin, draftId,
            new FieldValueInput(fieldId, null, null, null, null,
                SectionValue((project.Id, new() { ["rows"] = GridCell() }))));
        var res = await admin.PostAsync($"/api/submissions/{draftId}/submit", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ===== 9: عدة مشاريع كلٌّ بصفوف Grid خاصة به =====
    [Fact]
    public async Task MultipleProjects_EachOwnGridRows_RoundTrip()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var config = SectionConfig(true, 1, 5, GridField("rows", "جدول", false, "أ"));
        var (templateId, fieldId) = await PublishSectionAsync(admin, config);
        var p1 = await CreateProjectAsync(admin, "أول");
        var p2 = await CreateProjectAsync(admin, "ثاني");

        var draftId = await CreateDraftAsync(admin, templateId, TestCalendar.Cycle(9));
        await SaveValuesAsync(admin, draftId,
            new FieldValueInput(fieldId, null, null, null, null,
                SectionValue(
                    (p1.Id, new() { ["rows"] = GridCell(new[] { "p1r1" }) }),
                    (p2.Id, new() { ["rows"] = GridCell(new[] { "p2r1" }, new[] { "p2r2" }) }))));
        await admin.PostAsync($"/api/submissions/{draftId}/submit", null);

        var sub = await GetAsync(admin, draftId);
        Assert.Single(ReadGrid(sub, fieldId, p1.Id, "rows"));
        Assert.Equal(2, ReadGrid(sub, fieldId, p2.Id, "rows").Length);
        Assert.Equal("p2r2", ReadGrid(sub, fieldId, p2.Id, "rows")[1][0]);
    }

    // ===== 10: أعمدة Grid في ConfigJson تُحفَظ وتُقرأ على مستوى القالب =====
    [Fact]
    public async Task ConfigJson_GridColumns_PreservedOnRead()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var cols = new[] { "الكلمة المفتاحية", "Position", "CTR" };
        var config = SectionConfig(true, 1, 5, GridField("keywords", "كلمات", false, cols));
        var (templateId, _) = await PublishSectionAsync(admin, config);

        var detail = await (await admin.GetAsync($"/api/report-templates/{templateId}")).ReadAsync<ReportTemplateDetailDto>();
        var field = detail!.Versions.Single().Fields.First(f => f.FieldType == FieldType.ProjectRepeatableSection);
        using var doc = JsonDocument.Parse(field.ConfigJson!);
        var sub = doc.RootElement.GetProperty("fields")[0];
        Assert.Equal("Grid", sub.GetProperty("type").GetString());
        var readCols = sub.GetProperty("columns").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Equal(cols, readCols);
    }

    // ===== 11: الحد الأدنى للمشاريع يبقى مفروضًا مع إعداد Grid =====
    [Fact]
    public async Task MinProjects_Enforced_WithGridConfig()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var config = SectionConfig(true, 1, 5, GridField("rows", "جدول", false, "أ"));
        var (templateId, _) = await PublishSectionAsync(admin, config);

        var draftId = await CreateDraftAsync(admin, templateId, TestCalendar.Cycle(10));
        var res = await admin.PostAsync($"/api/submissions/{draftId}/submit", null);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ===== 12: الحد الأقصى للمشاريع يبقى مفروضًا مع إعداد Grid =====
    [Fact]
    public async Task MaxProjects_Enforced_WithGridConfig()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var config = SectionConfig(true, 1, 1, GridField("rows", "جدول", false, "أ"));
        var (templateId, fieldId) = await PublishSectionAsync(admin, config);
        var p1 = await CreateProjectAsync(admin, "أ");
        var p2 = await CreateProjectAsync(admin, "ب");

        var draftId = await CreateDraftAsync(admin, templateId, TestCalendar.Cycle(11));
        await SaveValuesAsync(admin, draftId,
            new FieldValueInput(fieldId, null, null, null, null,
                SectionValue(
                    (p1.Id, new() { ["rows"] = GridCell(new[] { "1" }) }),
                    (p2.Id, new() { ["rows"] = GridCell(new[] { "2" }) }))));
        var res = await admin.PostAsync($"/api/submissions/{draftId}/submit", null);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ===== 13: مشروع خارج النطاق يُرفَض حتى مع بيانات Grid (منع IDOR) =====
    [Fact]
    public async Task OutOfScopeProject_Blocked_EvenWithGridAnswers()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var config = SectionConfig(true, 1, 5, GridField("rows", "جدول", false, "أ"));
        var (templateId, fieldId) = await PublishSectionAsync(admin, config);
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var project = await CreateProjectAsync(admin, "مشروع الأدمن فقط");

        var draftId = await CreateDraftAsync(employee, templateId, TestCalendar.Cycle(12));
        await SaveValuesAsync(employee, draftId,
            new FieldValueInput(fieldId, null, null, null, null,
                SectionValue((project.Id, new() { ["rows"] = GridCell(new[] { "x" }) }))));
        var res = await employee.PostAsync($"/api/submissions/{draftId}/submit", null);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ===== 14: التقرير القديم يبقى سليمًا بعد نشر إصدار جديد يحوي قسم Grid (التوافق الخلفي) =====
    [Fact]
    public async Task OldSubmission_StillRenders_AfterNewVersionWithGrid()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب إصدارات {Guid.NewGuid():N}", null, null, PeriodType.Weekly,
                TemplateClassification.Supplementary))).ReadAsync<ReportTemplateDetailDto>();
        var v1 = created!.Versions.Single().Id;
        var oldField = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{v1}/fields",
            new UpsertFieldRequest("ملاحظة قديمة", "note", FieldType.ShortText, false, null, null)))
            .ReadAsync<TemplateFieldDto>();
        await admin.PostAsync($"/api/report-templates/versions/{v1}/publish", null);

        // تسليم على الإصدار الأول (بلا Grid).
        var draftId = await CreateDraftAsync(admin, created.Id, TestCalendar.Cycle(13));
        await SaveValuesAsync(admin, draftId,
            new FieldValueInput(oldField!.Id, "قيمة قديمة", null, null, null, null));
        var submitted = await (await admin.PostAsync($"/api/submissions/{draftId}/submit", null)).ReadAsync<SubmissionDto>();

        // إصدار جديد v2 يضيف قسم Grid ثم يُنشَر.
        var v2 = await (await admin.PostAsJsonAsync($"/api/report-templates/{created.Id}/versions", new { }))
            .ReadAsync<TemplateVersionDto>();
        await admin.PostAsJsonAsync($"/api/report-templates/versions/{v2!.Id}/fields",
            new UpsertFieldRequest("تفاصيل المشاريع", "projects", FieldType.ProjectRepeatableSection, false, null,
                SectionConfig(true, 0, 5, GridField("rows", "جدول", false, "أ"))));
        await admin.PostAsync($"/api/report-templates/versions/{v2.Id}/publish", null);

        // التقرير القديم ما زال يشير إلى v1 ويحمل قيمته القديمة سليمة (لا Grid).
        var back = await GetAsync(admin, draftId);
        Assert.Equal(submitted!.ReportTemplateVersionId, back.ReportTemplateVersionId);
        Assert.Equal("قيمة قديمة", back.FieldValues.First(v => v.TemplateFieldId == oldField.Id).ValueText);
        Assert.DoesNotContain(back.FieldValues, v => v.FieldType == FieldType.ProjectRepeatableSection);
    }

    // ===== 15: تجميع SEO Rollup ما زال يقرأ الحقول الرقمية العليا (لم يتغيّر) =====
    [Fact]
    public async Task SeoRollup_Aggregation_Unchanged_ReadsTopLevelNumbers()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);

        // القالب المبذور «🔍 تقرير فريق SEO» (لا قالب جديد).
        var list = await (await admin.GetAsync("/api/report-templates")).ReadAsync<List<ReportTemplateDto>>();
        var seo = list!.First(t => t.Title == SeoReportSchema.TeamTemplateTitle);
        var detail = await (await admin.GetAsync($"/api/report-templates/{seo.Id}")).ReadAsync<ReportTemplateDetailDto>();
        var version = detail!.Versions.First(v => v.IsPublished);

        Guid Fid(string label) => version.Fields.First(f => f.Label == label).Id;
        var values = new List<FieldValueInput>();
        // املأ كل الحقول العليا المطلوبة بقيمة مناسبة للنوع كي يمرّ التحقق.
        foreach (var f in version.Fields.Where(f => f.IsRequired))
        {
            values.Add(f.FieldType switch
            {
                FieldType.Number or FieldType.Decimal or FieldType.Currency or FieldType.Percentage
                    or FieldType.Rating or FieldType.Scale => new FieldValueInput(f.Id, null, 1m, null, null, null),
                FieldType.Boolean => new FieldValueInput(f.Id, null, null, null, true, null),
                FieldType.Date or FieldType.DateTime => new FieldValueInput(f.Id, null, null, DateTime.UtcNow, null, null),
                _ => new FieldValueInput(f.Id, "قيمة", null, null, null, null),
            });
        }
        // الحقول الرقمية محل الاختبار (قد تكون مطلوبة أو لا — نضبطها صراحةً).
        void SetNum(string label, decimal n)
        {
            var id = Fid(label);
            values.RemoveAll(v => v.TemplateFieldId == id);
            values.Add(new FieldValueInput(id, null, n, null, null, null));
        }
        SetNum(SeoReportSchema.ImprovedKeywords, 10m);
        SetNum(SeoReportSchema.DeclinedKeywords, 4m);

        // القالب المبذور «🔍 تقرير فريق SEO» يفرض قسم مشاريع (minProjects=1)؛ نوفّر عنصرًا صالحًا كي يمرّ
        // التسليم دون أن يتغيّر أيّ من أرقام التجميع العليا محلّ الاختبار.
        var seoProject = await CreateProjectAsync(admin, "مشروع تجميع SEO");
        var prsId = version.Fields.First(f => f.FieldType == FieldType.ProjectRepeatableSection).Id;
        values.RemoveAll(v => v.TemplateFieldId == prsId);
        values.Add(new FieldValueInput(prsId, null, null, null, null, SectionValue((seoProject.Id, new()))));

        var draftId = await CreateDraftAsync(admin, seo.Id, TestCalendar.Cycle(14));
        await SaveValuesAsync(admin, draftId, values.ToArray());
        await admin.PostAsync($"/api/submissions/{draftId}/submit", null);

        var rollup = await (await admin.GetAsync($"/api/reports/seo-rollup?periodType=Weekly&periodKey={TestCalendar.Cycle(14)}"))
            .ReadAsync<SeoRollupReport>();
        Assert.Equal(10m, rollup!.TotalImprovedKeywords);
        Assert.Equal(4m, rollup.TotalDeclinedKeywords);
        Assert.Equal(6m, rollup.NetKeywordMovement);
    }

    // ===== 16: حقل فرعي نصّي مطلوب مفقود يُرفَض حتى مع وجود Grid صحيح =====
    [Fact]
    public async Task RequiredTextSubField_Missing_WithGridPresent_Returns400()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var config = SectionConfig(true, 1, 5,
            TextField("status", "الحالة", true),
            GridField("rows", "جدول", false, "أ"));
        var (templateId, fieldId) = await PublishSectionAsync(admin, config);
        var project = await CreateProjectAsync(admin, "مشروع ناقص");

        var draftId = await CreateDraftAsync(admin, templateId, TestCalendar.Cycle(15));
        // الحقل النصّي المطلوب "status" مفقود رغم امتلاء الجدول.
        await SaveValuesAsync(admin, draftId,
            new FieldValueInput(fieldId, null, null, null, null,
                SectionValue((project.Id, new() { ["rows"] = GridCell(new[] { "x" }) }))));
        var res = await admin.PostAsync($"/api/submissions/{draftId}/submit", null);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
