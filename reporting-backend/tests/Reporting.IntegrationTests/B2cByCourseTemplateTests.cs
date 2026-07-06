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
/// ERDS Phase 1 — قالب «تقرير مبيعات B2C حسب الدورة» (تجريبي، مُهيكَل، additive).
/// يتحقّق من: بذر القالب بالأعمدة والحقول المتوقّعة، دورة حياة الإرسال مع تخزين قيم الجدول
/// (string[][] في ValueJson) واعتمادها عبر المسار الحالي، وبقاء القوالب القديمة قابلة للقراءة.
/// </summary>
[Collection("Integration")]
public class B2cByCourseTemplateTests
{
    private readonly CustomWebApplicationFactory _factory;

    public B2cByCourseTemplateTests(CustomWebApplicationFactory factory) => _factory = factory;

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
    public async Task SeededTemplate_Exists_WithRequiredGrid_TenColumns_AndFourTextFields()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var detail = await GetSeededB2cTemplateAsync(admin);

        Assert.Equal(TemplateStatus.Published, detail.Status);
        Assert.Null(detail.JobRoleId); // قالب عام (يُسنَد للجميع بلا مسمّى أخصّ)

        var fields = PublishedVersion(detail).Fields;

        // الجدول الرئيسي: TableGrid مطلوب بالأعمدة العشرة بالترتيب.
        var grid = Assert.Single(fields.Where(f => f.FieldType == FieldType.TableGrid));
        Assert.True(grid.IsRequired);
        Assert.Equal(B2cByCourseReportSchema.MainTableLabel, grid.Label);
        var cols = JsonSerializer.Deserialize<GridConfig>(grid.ConfigJson!,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!.Columns;
        Assert.Equal(B2cByCourseReportSchema.Columns, cols);

        // الحقول النصية الداعمة الأربعة موجودة.
        var textLabels = fields.Where(f => f.FieldType == FieldType.LongText).Select(f => f.Label).ToHashSet();
        Assert.Contains(B2cByCourseReportSchema.TopAchievements, textLabels);
        Assert.Contains(B2cByCourseReportSchema.TopChallenges, textLabels);
        Assert.Contains(B2cByCourseReportSchema.SupportNeeded, textLabels);
        Assert.Contains(B2cByCourseReportSchema.ExceptionalNotes, textLabels);
    }

    [Fact]
    public async Task Submit_B2cTable_StoresTabularValues_And_ApprovesViaCurrentPath()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var detail = await GetSeededB2cTemplateAsync(admin);
        var grid = Assert.Single(PublishedVersion(detail).Fields.Where(f => f.FieldType == FieldType.TableGrid));

        // طبقة عليا بلا مدير ⇒ اعتماد بخطوة واحدة (نفس مسار الاعتماد الحالي، بلا تغيير).
        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", ceoId);

        var draft = await (await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(detail.Id, PeriodType.Weekly, "2026-W23")))
            .ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Draft, draft!.Status);

        // الإرسال قبل تعبئة الجدول المطلوب ⇒ 400 (لا يكسر التحقّق الحالي المبني على الحضور).
        var early = await employee.PostAsync($"/api/submissions/{draft.Id}/submit", null);
        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);

        // صفّان لدورتين — كل الخلايا نصّية (تخزين string[][] في ValueJson).
        var rows = new[]
        {
            new[] { "دورة التسويق الرقمي", "12", "40", "30", "18", "9", "6", "18000", "3", "السعر" },
            new[] { "دورة إدارة المشاريع", "8", "25", "20", "12", "5", "4", "12000", "2", "التوقيت" },
        };
        var gridJson = JsonSerializer.Serialize(rows);

        var save = await employee.PutAsJsonAsync($"/api/submissions/{draft.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(grid.Id, null, null, null, null, gridJson) }));
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var submitted = await (await employee.PostAsync($"/api/submissions/{draft.Id}/submit", null))
            .ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Submitted, submitted!.Status);
        Assert.Equal(ceoId, submitted.CurrentApproverId);

        // القيم الجدولية مُخزَّنة ومُعادة كما هي.
        var storedJson = Assert.Single(submitted.FieldValues.Where(v => v.TemplateFieldId == grid.Id)).ValueJson;
        var storedRows = JsonSerializer.Deserialize<string[][]>(storedJson!);
        Assert.Equal(rows, storedRows);

        // الاعتماد عبر المسار الحالي ⇒ مُغلق.
        var approved = await (await ceo.PostAsJsonAsync($"/api/submissions/{draft.Id}/approve",
            new ApprovalActionRequest("معتمد"))).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Closed, approved!.Status);

        // القيم الجدولية باقية للقراءة بعد الإغلاق.
        var afterClose = Assert.Single(approved.FieldValues.Where(v => v.TemplateFieldId == grid.Id)).ValueJson;
        Assert.Equal(rows, JsonSerializer.Deserialize<string[][]>(afterClose!));
    }

    [Fact]
    public async Task OldTemplates_RemainReadable_And_TemplateCount_Grew()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var list = await (await admin.GetAsync("/api/report-templates"))
            .ReadAsync<List<ReportTemplateDto>>();

        // القالب الجديد مضاف، والقوالب القديمة ما زالت مسرودة وقابلة للقراءة (توافق خلفي، بلا كسر).
        Assert.Contains(list!, t => t.Title == B2cByCourseReportSchema.TemplateTitle);
        var old = Assert.Single(list!.Where(t => t.Title == "التقرير المالي"));
        var oldDetail = await (await admin.GetAsync($"/api/report-templates/{old.Id}"))
            .ReadAsync<ReportTemplateDetailDto>();
        Assert.Equal(TemplateStatus.Published, oldDetail!.Status);
        Assert.NotEmpty(PublishedVersion(oldDetail).Fields);
    }

    private record GridConfig(string[] Columns);
}
