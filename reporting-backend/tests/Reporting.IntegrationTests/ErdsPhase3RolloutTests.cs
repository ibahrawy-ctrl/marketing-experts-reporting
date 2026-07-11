using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Reporting.Application.Common;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// ERDS Phase 3 — تعميم التقارير الرقمية المُهيكَلة (additive). بعد تحويل التنفيذ إلى Project-First (RC-4 Task 4)
/// أُرشِفت عائلة الإنتاج المسطّحة الستة (محتوى/تصميم/فيديو/نشر/ميديا باير/مشاريع) بوصفها Legacy، وبقي قالب المبيعات
/// المُهيكَل الحيّ الوحيد من هذه الدفعة = B2B حسب الخدمة. تتحقّق هذه الاختبارات من بذره منشورًا بجدوله وحقوله النصية،
/// دورة حياة إرساله مع تخزين قيم الجدول (string[][] في ValueJson) واعتماده عبر المسار الحالي، وبقاء القوالب القديمة
/// (المالي) + قالب B2C حسب الدورة قابلة للقراءة. القوالب المُرشَفة لها تغطية جديدة عبر محرّك Project-First.
/// </summary>
[Collection("Integration")]
public class ErdsPhase3RolloutTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ErdsPhase3RolloutTests(CustomWebApplicationFactory factory) => _factory = factory;

    private record GridConfig(string[] Columns);

    /// <summary>
    /// القالب المُهيكَل الحيّ الوحيد المتبقّي من دفعة Phase 3: (العنوان، اسم الجدول الرئيسي، الأعمدة، عناوين الحقول النصية الأربعة).
    /// عائلة الإنتاج المسطّحة الستة أُرشِفت بعد تحويل التنفيذ إلى Project-First (RC-4 Task 4).
    /// </summary>
    public static IEnumerable<object[]> Phase3Templates()
    {
        yield return new object[]
        {
            B2bByServiceReportSchema.TemplateTitle, B2bByServiceReportSchema.MainTableLabel,
            B2bByServiceReportSchema.Columns,
            new[] { B2bByServiceReportSchema.TopAchievements, B2bByServiceReportSchema.TopChallenges,
                    B2bByServiceReportSchema.SupportNeeded, B2bByServiceReportSchema.Notes },
        };
    }

    private static async Task<ReportTemplateDetailDto> GetTemplateByTitleAsync(HttpClient admin, string title)
    {
        var list = await (await admin.GetAsync("/api/report-templates"))
            .ReadAsync<List<ReportTemplateDto>>();
        var summary = Assert.Single(list!.Where(t => t.Title == title));
        return (await (await admin.GetAsync($"/api/report-templates/{summary.Id}"))
            .ReadAsync<ReportTemplateDetailDto>())!;
    }

    private static TemplateVersionDto PublishedVersion(ReportTemplateDetailDto t)
        => t.Versions.Single(v => v.IsPublished);

    // ===== (1) كل قالب مبذور، منشور، عام، بجدول مطلوب بالأعمدة الصحيحة + 4 حقول نصية =====
    [Theory]
    [MemberData(nameof(Phase3Templates))]
    public async Task SeededTemplate_Exists_WithRequiredGrid_AndFourTextFields(
        string title, string mainLabel, string[] columns, string[] textLabels)
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var detail = await GetTemplateByTitleAsync(admin, title);

        Assert.Equal(TemplateStatus.Published, detail.Status);
        Assert.Null(detail.JobRoleId); // قالب عام

        var fields = PublishedVersion(detail).Fields;

        var grid = Assert.Single(fields.Where(f => f.FieldType == FieldType.TableGrid));
        Assert.True(grid.IsRequired);
        Assert.Equal(mainLabel, grid.Label);
        var cols = JsonSerializer.Deserialize<GridConfig>(grid.ConfigJson!,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!.Columns;
        Assert.Equal(columns, cols);

        var actualTextLabels = fields.Where(f => f.FieldType == FieldType.LongText).Select(f => f.Label).ToHashSet();
        foreach (var label in textLabels)
            Assert.Contains(label, actualTextLabels);
    }

    // ===== (2) دورة حياة كاملة لكل قالب: مسودّة→تعبئة الجدول→إرسال→اعتماد→إغلاق مع تخزين القيم =====
    [Theory]
    [MemberData(nameof(Phase3Templates))]
    public async Task Submit_StoresTabularValues_And_ApprovesViaCurrentPath(
        string title, string mainLabel, string[] columns, string[] textLabels)
    {
        _ = mainLabel;
        _ = textLabels;
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var detail = await GetTemplateByTitleAsync(admin, title);
        var grid = Assert.Single(PublishedVersion(detail).Fields.Where(f => f.FieldType == FieldType.TableGrid));

        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", ceoId);

        var draft = await (await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(detail.Id, PeriodType.Weekly, "2026-W33")))
            .ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Draft, draft!.Status);

        // الإرسال قبل تعبئة الجدول المطلوب ⇒ 400 (التحقّق الحالي المبني على الحضور).
        var early = await employee.PostAsync($"/api/submissions/{draft.Id}/submit", null);
        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);

        // صفّان بعدد الأعمدة نفسه — قيم رقمية/نصية بسيطة (كل الخلايا نصّية على مستوى التخزين).
        var rows = new[] { BuildRow(columns.Length, "1"), BuildRow(columns.Length, "2") };
        var gridJson = JsonSerializer.Serialize(rows);

        var save = await employee.PutAsJsonAsync($"/api/submissions/{draft.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(grid.Id, null, null, null, null, gridJson) }));
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var submitted = await (await employee.PostAsync($"/api/submissions/{draft.Id}/submit", null))
            .ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Submitted, submitted!.Status);

        var storedJson = Assert.Single(submitted.FieldValues.Where(v => v.TemplateFieldId == grid.Id)).ValueJson;
        Assert.Equal(rows, JsonSerializer.Deserialize<string[][]>(storedJson!));

        var approved = await (await ceo.PostAsJsonAsync($"/api/submissions/{draft.Id}/approve",
            new ApprovalActionRequest("معتمد"))).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Closed, approved!.Status);

        var afterClose = Assert.Single(approved.FieldValues.Where(v => v.TemplateFieldId == grid.Id)).ValueJson;
        Assert.Equal(rows, JsonSerializer.Deserialize<string[][]>(afterClose!));
    }

    // صفّ نصّي: أول خلية «عنصر N» ثم بقية الخلايا قيمة رقمية ثابتة "1".
    // القيمة "1" الموحّدة تُرضي حارس قمع مبيعات B2B (Meetings≤Leads، Proposals≤Meetings، Won≤Proposals، Lost≤Leads،
    // وWorkHours>0 عند وجود نشاط) بينما بقيّة القوالب لا تُطبّق أيّ تحقّق رقمي؛ والاختبار يتحقّق round-trip فقط.
    private static string[] BuildRow(int width, string seed)
    {
        var row = new string[width];
        row[0] = $"عنصر {seed}";
        for (var i = 1; i < width; i++) row[i] = "1";
        return row;
    }

    // ===== (3) القوالب القديمة + قالب B2C حسب الدورة غير متأثّرة (توافق خلفي) =====
    [Fact]
    public async Task OldTemplates_And_B2cByCourse_RemainReadable()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var list = await (await admin.GetAsync("/api/report-templates"))
            .ReadAsync<List<ReportTemplateDto>>();

        // القالب القديم المالي ما زال منشورًا وقابلًا للقراءة.
        var old = Assert.Single(list!.Where(t => t.Title == "التقرير المالي"));
        var oldDetail = await (await admin.GetAsync($"/api/report-templates/{old.Id}"))
            .ReadAsync<ReportTemplateDetailDto>();
        Assert.Equal(TemplateStatus.Published, oldDetail!.Status);
        Assert.NotEmpty(PublishedVersion(oldDetail).Fields);

        // قالب B2C حسب الدورة (Phase 1/2A) ما زال بأعمدته العشرة كما هي.
        var b2c = Assert.Single(list!.Where(t => t.Title == B2cByCourseReportSchema.TemplateTitle));
        var b2cDetail = await (await admin.GetAsync($"/api/report-templates/{b2c.Id}"))
            .ReadAsync<ReportTemplateDetailDto>();
        var b2cGrid = Assert.Single(PublishedVersion(b2cDetail!).Fields.Where(f => f.FieldType == FieldType.TableGrid));
        var b2cCols = JsonSerializer.Deserialize<GridConfig>(b2cGrid.ConfigJson!,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!.Columns;
        Assert.Equal(B2cByCourseReportSchema.Columns, b2cCols);
    }

    // ===== (4) قالب المبيعات المُهيكَل الحيّ مسرود ضمن القوالب =====
    [Fact]
    public async Task LiveRolloutTemplate_Is_Listed()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var list = await (await admin.GetAsync("/api/report-templates"))
            .ReadAsync<List<ReportTemplateDto>>();

        foreach (var row in Phase3Templates())
        {
            var title = (string)row[0];
            Assert.Contains(list!, t => t.Title == title);
        }
    }
}
