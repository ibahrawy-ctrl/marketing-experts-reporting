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
/// ERDS Phase 3 — تعميم التقارير الرقمية على كل الإدارات (7 قوالب جديدة متوازية، مُهيكَلة، additive).
/// يتحقّق من: بذر كل قالب بالأعمدة والحقول النصية المتوقّعة، دورة حياة الإرسال مع تخزين قيم الجدول
/// (string[][] في ValueJson) واعتمادها عبر المسار الحالي، وبقاء القوالب القديمة + قالب B2C حسب الدورة قابلة للقراءة.
/// نطاق تحقّق الخلايا لـ B2C (Phase 2A) محصور بمطابقة أعمدته تمامًا؛ لذا هذه القوالب السبعة خارج نطاقه ولا تتأثّر به.
/// </summary>
[Collection("Integration")]
public class ErdsPhase3RolloutTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ErdsPhase3RolloutTests(CustomWebApplicationFactory factory) => _factory = factory;

    private record GridConfig(string[] Columns);

    /// <summary>القوالب السبعة الجديدة: (العنوان، اسم الجدول الرئيسي، الأعمدة، عناوين الحقول النصية الأربعة).</summary>
    public static IEnumerable<object[]> Phase3Templates()
    {
        yield return new object[]
        {
            B2bByServiceReportSchema.TemplateTitle, B2bByServiceReportSchema.MainTableLabel,
            B2bByServiceReportSchema.Columns,
            new[] { B2bByServiceReportSchema.TopAchievements, B2bByServiceReportSchema.TopChallenges,
                    B2bByServiceReportSchema.SupportNeeded, B2bByServiceReportSchema.Notes },
        };
        yield return new object[]
        {
            ProjectsByClientReportSchema.TemplateTitle, ProjectsByClientReportSchema.MainTableLabel,
            ProjectsByClientReportSchema.Columns,
            new[] { ProjectsByClientReportSchema.TopAchievements, ProjectsByClientReportSchema.TopObstacles,
                    ProjectsByClientReportSchema.DecisionsNeeded, ProjectsByClientReportSchema.Notes },
        };
        yield return new object[]
        {
            ContentProductionReportSchema.TemplateTitle, ContentProductionReportSchema.MainTableLabel,
            ContentProductionReportSchema.Columns,
            new[] { ContentProductionReportSchema.BestContent, ContentProductionReportSchema.TopChallenges,
                    ContentProductionReportSchema.SupportNeeded, ContentProductionReportSchema.Notes },
        };
        yield return new object[]
        {
            DesignProductionReportSchema.TemplateTitle, DesignProductionReportSchema.MainTableLabel,
            DesignProductionReportSchema.Columns,
            new[] { DesignProductionReportSchema.BestDesigns, DesignProductionReportSchema.TopChallenges,
                    DesignProductionReportSchema.SupportNeeded, DesignProductionReportSchema.Notes },
        };
        yield return new object[]
        {
            VideoProductionReportSchema.TemplateTitle, VideoProductionReportSchema.MainTableLabel,
            VideoProductionReportSchema.Columns,
            new[] { VideoProductionReportSchema.BestVideos, VideoProductionReportSchema.TopChallenges,
                    VideoProductionReportSchema.SupportNeeded, VideoProductionReportSchema.Notes },
        };
        yield return new object[]
        {
            SocialPublishingReportSchema.TemplateTitle, SocialPublishingReportSchema.MainTableLabel,
            SocialPublishingReportSchema.Columns,
            new[] { SocialPublishingReportSchema.PublishingConsistency, SocialPublishingReportSchema.TopPublishingIssues,
                    SocialPublishingReportSchema.SupportNeeded, SocialPublishingReportSchema.Notes },
        };
        yield return new object[]
        {
            MediaBuyerByClientReportSchema.TemplateTitle, MediaBuyerByClientReportSchema.MainTableLabel,
            MediaBuyerByClientReportSchema.Columns,
            new[] { MediaBuyerByClientReportSchema.BestCampaign, MediaBuyerByClientReportSchema.WeakestCampaign,
                    MediaBuyerByClientReportSchema.ImprovementOrDeclineReasons, MediaBuyerByClientReportSchema.SupportNeeded },
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

    // صفّ نصّي: أول خلية «عنصر N» ثم بقية الخلايا أرقام بسيطة.
    private static string[] BuildRow(int width, string seed)
    {
        var row = new string[width];
        row[0] = $"عنصر {seed}";
        for (var i = 1; i < width; i++) row[i] = $"{i}";
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

    // ===== (4) القوالب السبعة جميعها مسرودة (نمت قائمة القوالب) =====
    [Fact]
    public async Task All_Seven_Templates_Are_Listed()
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
