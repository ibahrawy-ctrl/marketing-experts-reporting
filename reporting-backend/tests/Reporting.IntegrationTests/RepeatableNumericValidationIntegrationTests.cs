using System.Globalization;
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
/// PROJECT-REPEATABLE-NUMERIC-VALIDATION-R1 — اختبارات تكامل تحقّق الحقول الرقميّة داخل قسم المشاريع
/// المتكرّر (ProjectRepeatableSection) عبر مسار الإرسال الفعليّ (POST /submissions/{id}/submit).
///
/// المبدأ المُثبَت: الفرض الرقميّ (min/max/integerOnly/step) يُفعَّل فقط للحقل الرقميّ الذي يحمل قيدًا
/// واحدًا على الأقل؛ القوالب/الحقول بلا قيود تبقى بلا فرض (توافق خلفيّ تامّ — تقبل السالب والعشريّ).
/// الخادم هو طبقة الفرض المرجعيّة: القيمة المخالفة تُرفَض 400 برمز خطأ مستقرّ حتى لو أُرسلت من API مباشرة.
///
/// العزل: قاعدة reporting_pfe_num_iso المنفصلة (PfeNumericIsolatedFactory). مفاتيح الفترة مُولَّدة ديناميكيًّا
/// للأسبوع الحاليّ/الماضي (ISO-8601) — لا مفاتيح مستقبليّة ثابتة.
/// </summary>
[Collection("PfeNumericIsolated")]
public class RepeatableNumericValidationIntegrationTests
{
    private readonly PfeNumericIsolatedFactory _factory;

    public RepeatableNumericValidationIntegrationTests(PfeNumericIsolatedFactory factory) => _factory = factory;

    // ===== أدوات البناء (نمط ProjectRepeatableGridTests) =====
    private static string SectionConfig(bool projectRequired, int min, int max, params object[] fields)
        => JsonSerializer.Serialize(new { projectRequired, minProjects = min, maxProjects = max, fields });

    // حقل رقميّ مُقيَّد داخل القسم المتكرّر — أيّ قيد يُترَك null لا يُدرَج (توافق خلفيّ).
    private static object NumericField(string key, string label, string type, bool required,
        decimal? min = null, decimal? max = null, bool integerOnly = false, decimal? step = null)
        => new { key, label, type, required, min, max, integerOnly, step };

    // حقل رقميّ بلا أيّ قيد (توافق خلفيّ — يقبل السالب/العشريّ كالقوالب القديمة).
    private static object PlainNumericField(string key, string label, bool required)
        => new { key, label, type = "Number", required };

    private static string SectionValue(params (Guid? ProjectId, Dictionary<string, string> Answers)[] entries)
        => JsonSerializer.Serialize(entries.Select(e => new { projectId = e.ProjectId, answers = e.Answers }).ToArray());

    private static async Task<(Guid TemplateId, Guid FieldId)> PublishSectionAsync(HttpClient admin, string configJson, bool required = true)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب PFE-NUM {Guid.NewGuid():N}", null, null, PeriodType.Weekly,
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

    // مفتاح فترة أسبوعيّ مُولَّد ديناميكيًّا (ISO-8601 YYYY-Www) بإزاحة أسابيع للخلف — لا مفتاح مستقبليّ.
    private static string WeekKey(int weeksBack)
    {
        var d = DateTime.UtcNow.Date.AddDays(-7 * weeksBack);
        var week = System.Globalization.ISOWeek.GetWeekOfYear(d);
        var year = System.Globalization.ISOWeek.GetYear(d);
        return $"{year:D4}-W{week:D2}";
    }

    // ينشئ مسوّدة بقيمة واحدة لمشروع واحد في القسم المتكرّر ويحاول الإرسال، ويُعيد الاستجابة.
    private async Task<HttpResponseMessage> SubmitSingleAsync(
        HttpClient admin, Guid templateId, Guid fieldId, Guid projectId, int weeksBack,
        Dictionary<string, string> answers)
    {
        var draftId = await CreateDraftAsync(admin, templateId, WeekKey(weeksBack));
        await SaveValuesAsync(admin, draftId,
            new FieldValueInput(fieldId, null, null, null, null, SectionValue((projectId, answers))));
        return await admin.PostAsync($"/api/submissions/{draftId}/submit", null);
    }

    private static async Task<string> BodyAsync(HttpResponseMessage r) => await r.Content.ReadAsStringAsync();

    // القالب المُتحكَّم فيه: عدد صحيح min=0، عدد صحيح max=100، عشريّ step=0.1، رقميّ اختياريّ، رقميّ مطلوب.
    private static string ControlledConfig() => SectionConfig(true, 1, 5,
        NumericField("qty", "عدد القطع", "Number", required: true, min: 0m, integerOnly: true),
        NumericField("capped", "قيمة محدودة", "Number", required: false, min: 0m, max: 100m, integerOnly: true),
        NumericField("ratio", "نسبة عشريّة", "Decimal", required: false, step: 0.1m),
        PlainNumericField("legacy", "قيمة قديمة بلا قيود", required: false));

    // ---- I1: قيمة صحيحة موجبة صالحة ⇒ يُقبَل الإرسال (200) ----
    [Fact]
    public async Task ValidPositiveInteger_Submits200()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (tid, fid) = await PublishSectionAsync(admin, ControlledConfig());
        var p = await CreateProjectAsync(admin, "مشروع صالح");
        var res = await SubmitSingleAsync(admin, tid, fid, p.Id, 0,
            new() { ["qty"] = "5", ["capped"] = "50", ["ratio"] = "0.2" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ---- I2: صفر مع min=0 صالح ⇒ 200 (لا فرض ضمنيّ لموجب صرف) ----
    [Fact]
    public async Task ZeroWithMinZero_Submits200()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (tid, fid) = await PublishSectionAsync(admin, ControlledConfig());
        var p = await CreateProjectAsync(admin, "مشروع صفر");
        var res = await SubmitSingleAsync(admin, tid, fid, p.Id, 1, new() { ["qty"] = "0" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ---- I3: قيمة سالبة تحت min=0 ⇒ 400 + رمز below_min ----
    [Fact]
    public async Task NegativeBelowMin_Rejected400_BelowMinCode()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (tid, fid) = await PublishSectionAsync(admin, ControlledConfig());
        var p = await CreateProjectAsync(admin, "مشروع سالب");
        var res = await SubmitSingleAsync(admin, tid, fid, p.Id, 2, new() { ["qty"] = "-1" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains(RepeatableNumericValidation.BelowMin, await BodyAsync(res));
    }

    // ---- I4: قيمة عشريّة لحقل integerOnly ⇒ 400 + رمز integer_required ----
    [Fact]
    public async Task DecimalForIntegerOnly_Rejected400_IntegerRequiredCode()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (tid, fid) = await PublishSectionAsync(admin, ControlledConfig());
        var p = await CreateProjectAsync(admin, "مشروع عشريّ");
        var res = await SubmitSingleAsync(admin, tid, fid, p.Id, 3, new() { ["qty"] = "12.5" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains(RepeatableNumericValidation.IntegerRequired, await BodyAsync(res));
    }

    // ---- I5: قيمة أكبر من max=100 ⇒ 400 + رمز above_max ----
    [Fact]
    public async Task AboveMax_Rejected400_AboveMaxCode()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (tid, fid) = await PublishSectionAsync(admin, ControlledConfig());
        var p = await CreateProjectAsync(admin, "مشروع فوق الحدّ");
        var res = await SubmitSingleAsync(admin, tid, fid, p.Id, 4,
            new() { ["qty"] = "1", ["capped"] = "101" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains(RepeatableNumericValidation.AboveMax, await BodyAsync(res));
    }

    // ---- I6: قيمة مطابقة للخطوة (0.2 على step=0.1) ⇒ 200 ----
    [Fact]
    public async Task OnStepDecimal_Submits200()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (tid, fid) = await PublishSectionAsync(admin, ControlledConfig());
        var p = await CreateProjectAsync(admin, "مشروع خطوة صحيحة");
        var res = await SubmitSingleAsync(admin, tid, fid, p.Id, 5,
            new() { ["qty"] = "1", ["ratio"] = "0.2" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ---- I7: قيمة غير مطابقة للخطوة (0.15 على step=0.1) ⇒ 400 + رمز step_invalid ----
    [Fact]
    public async Task OffStepDecimal_Rejected400_StepInvalidCode()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (tid, fid) = await PublishSectionAsync(admin, ControlledConfig());
        var p = await CreateProjectAsync(admin, "مشروع خطوة خاطئة");
        var res = await SubmitSingleAsync(admin, tid, fid, p.Id, 6,
            new() { ["qty"] = "1", ["ratio"] = "0.15" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains(RepeatableNumericValidation.StepInvalid, await BodyAsync(res));
    }

    // ---- I8: حقل قديم بلا قيود يقبل السالب والعشريّ (توافق خلفيّ — لا فرض) ⇒ 200 ----
    [Fact]
    public async Task UnconstrainedLegacyField_AcceptsNegativeAndDecimal_Submits200()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (tid, fid) = await PublishSectionAsync(admin, ControlledConfig());
        var p = await CreateProjectAsync(admin, "مشروع قديم");
        var res = await SubmitSingleAsync(admin, tid, fid, p.Id, 7,
            new() { ["qty"] = "1", ["legacy"] = "-1" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ---- I9: قالب بلا أيّ قيود إطلاقًا يقبل السالب التاريخيّ (approved_first_time = -1) ⇒ 200 ----
    [Fact]
    public async Task FullyUnconstrainedTemplate_AcceptsHistoricalNegative_Submits200()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var config = SectionConfig(true, 1, 5,
            PlainNumericField("approved_first_time", "المعتمد أول مرّة", required: false));
        var (tid, fid) = await PublishSectionAsync(admin, config);
        var p = await CreateProjectAsync(admin, "مشروع تاريخيّ");
        var res = await SubmitSingleAsync(admin, tid, fid, p.Id, 8,
            new() { ["approved_first_time"] = "-1" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ---- I10: قيمة رقميّة غير قابلة للتحليل لحقل مُقيَّد ⇒ 400 + رمز number_invalid ----
    [Fact]
    public async Task UnparseableNumber_Rejected400_NumberInvalidCode()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (tid, fid) = await PublishSectionAsync(admin, ControlledConfig());
        var p = await CreateProjectAsync(admin, "مشروع نصّ");
        var res = await SubmitSingleAsync(admin, tid, fid, p.Id, 9,
            new() { ["qty"] = "abc" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains(RepeatableNumericValidation.NumberInvalid, await BodyAsync(res));
    }

    // ---- I11: تعريف قيود غير صالح (Min>Max) ⇒ 400 + رمز config_invalid ----
    [Fact]
    public async Task InvalidConstraintDefinition_Rejected400_ConfigInvalidCode()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var config = SectionConfig(true, 1, 5,
            NumericField("bad", "قيد خاطئ", "Number", required: false, min: 100m, max: 0m));
        var (tid, fid) = await PublishSectionAsync(admin, config);
        var p = await CreateProjectAsync(admin, "مشروع قيد خاطئ");
        var res = await SubmitSingleAsync(admin, tid, fid, p.Id, 10,
            new() { ["bad"] = "5" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains(RepeatableNumericValidation.ConfigInvalid, await BodyAsync(res));
    }

    // ---- I12: حقل رقميّ مُقيَّد اختياريّ متروك فارغًا ⇒ لا فرض (الاختياريّة تبقى) ⇒ 200 ----
    [Fact]
    public async Task ConstrainedOptionalFieldLeftEmpty_Submits200()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (tid, fid) = await PublishSectionAsync(admin, ControlledConfig());
        var p = await CreateProjectAsync(admin, "مشروع اختياريّ فارغ");
        // qty مطلوب فقط؛ capped/ratio مُقيَّدان اختياريّان يُتركان فارغين.
        var res = await SubmitSingleAsync(admin, tid, fid, p.Id, 11, new() { ["qty"] = "3" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ---- I13: خانات عربيّة لقيمة صالحة تُقبَل (١٢٣ ⇒ 123 ضمن المدى) ⇒ 200 ----
    [Fact]
    public async Task ArabicDigitsWithinRange_Submits200()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var config = SectionConfig(true, 1, 5,
            NumericField("qty", "عدد", "Number", required: true, min: 0m, integerOnly: true));
        var (tid, fid) = await PublishSectionAsync(admin, config);
        var p = await CreateProjectAsync(admin, "مشروع خانات عربيّة");
        var res = await SubmitSingleAsync(admin, tid, fid, p.Id, 12, new() { ["qty"] = "١٢٣" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
