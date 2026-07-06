using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// B2C-UAT-FIXPACK — حزمة إصلاحات ما بعد UAT لتقرير مندوب مبيعات B2C:
/// (1) تفعيل قالب «مبيعات B2C حسب الدورة» بدلًا من قالب المندوب الفردي القديم (أساسي أخصّ للمسمّى).
/// (2) إيقاف صعود تقرير الفرد بعد قائد الفريق ⇒ اعتماد قائد الفريق يُغلق التقرير نهائيًّا بلا تصعيد للمدير.
/// (3) المدير يرى التقرير للقراءة فقط عبر النطاق/التجميع لا في طابور الاعتماد، والخط الزمني لا يُظهر
///     خطوات المدير/المدير العام/الرئيس بعد اعتماد قائد الفريق.
/// مع الحفاظ على البديل: لا قائد فريق ⇒ المدير، ولا مدير ⇒ المدير العام/الرئيس (توافق خلفي).
/// ملاحظة بيئة: الاختبارات تُنشئ قوالب جديدة ولا تُعدّل القوالب المبذورة (قاعدة اختبار دائمة مشتركة).
/// التغطية التكميلية: أعمدة القالب وإرساله في B2cByCourseTemplateTests؛ Phase 4/Phase 6 في اختباراتهما.
/// </summary>
[Collection("Integration")]
public class B2cUatFixPackTests
{
    private readonly CustomWebApplicationFactory _factory;

    public B2cUatFixPackTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ===== أدوات مساعدة =====

    /// <summary>ينشئ قالبًا منشورًا بحقل واحد مطلوب ويعيد (معرّف القالب، معرّف الحقل).</summary>
    private static async Task<(Guid TemplateId, Guid FieldId)> PublishReportTemplateAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب فرد {Guid.NewGuid():N}", null, null, PeriodType.Weekly)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;

        var field = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("الإنفاق", "spend", FieldType.Currency, true, null, null)))
            .ReadAsync<TemplateFieldDto>();

        var publishRes = await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        Assert.Equal(HttpStatusCode.OK, publishRes.StatusCode);

        return (created.Id, field!.Id);
    }

    /// <summary>ينشئ قالبًا أساسيًّا منشورًا مرتبطًا بمسمّى (لمحاكاة القالب الجديد الأخصّ) ويعيد معرّفه.</summary>
    private static async Task<Guid> PublishRoleTemplateAsync(HttpClient admin, Guid jobRoleId)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب مسمّى {Guid.NewGuid():N}", null, jobRoleId,
                PeriodType.Weekly, TemplateClassification.Primary)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("قيمة", "value", FieldType.Number, true, null, null));
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return created.Id;
    }

    /// <summary>ينشئ مسودّة، يعبّئ الحقل المطلوب، ثم يرسلها، ويعيد التسليم بعد الإرسال.</summary>
    private static async Task<SubmissionDto> SubmitReportAsync(
        HttpClient submitter, Guid templateId, Guid fieldId, string periodKey)
    {
        var draft = await (await submitter.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, periodKey)))
            .ReadAsync<SubmissionDto>();
        await submitter.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 100m, null, null, null) }));
        return (await (await submitter.PostAsync($"/api/submissions/{draft.Id}/submit", null))
            .ReadAsync<SubmissionDto>())!;
    }

    private async Task<Guid> CreateJobRoleAsync(string tag)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = new JobRole { NameAr = $"دور {tag}", Code = $"{tag}_{Guid.NewGuid():N}".Substring(0, 18) };
        db.JobRoles.Add(role);
        await db.SaveChangesAsync();
        return role.Id;
    }

    private async Task SetJobRoleAsync(Guid userId, Guid jobRoleId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.JobRoleId = jobRoleId;
        await db.SaveChangesAsync();
    }

    /// <summary>يُنزِل قالبًا إلى وضع «قديم» (مؤرشف + غير نشط + بلا مسمّى) كما تفعل OrgSeeder للقالب القديم.</summary>
    private async Task DemoteToLegacyAsync(Guid templateId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tpl = await db.ReportTemplates.FirstAsync(t => t.Id == templateId);
        tpl.JobRoleId = null;
        tpl.IsActive = false;
        tpl.Status = TemplateStatus.Archived;
        await db.SaveChangesAsync();
    }

    private static async Task<List<ReportTemplateDto>> SelfListAsync(HttpClient client)
        => (await (await client.GetAsync("/api/report-templates?status=Published&isActive=true&assignedOnly=true"))
            .ReadAsync<List<ReportTemplateDto>>())!;

    // ===== الجزء 1: تفعيل القالب الجديد بدل القالب الفردي القديم =====

    // (1) مندوب B2C يرى القالب الجديد الأخصّ لمسمّاه ولا يرى القالب القديم المؤرشف.
    [Fact]
    public async Task B2cRep_SeesNewRoleTemplate_NotLegacyIndividualTemplate()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var salesRole = await CreateJobRoleAsync("SALES_B2C");

        // القالب الجديد: أساسي مرتبط بمسمّى المندوب (أخصّ) ⇒ يجب أن يظهر.
        var newTemplate = await PublishRoleTemplateAsync(admin, salesRole);

        // القالب القديم: كان منشورًا ثم أُنزِل إلى «قديم» (مؤرشف + غير نشط + بلا مسمّى) ⇒ يجب أن يختفي.
        var (legacyTemplate, _) = await PublishReportTemplateAsync(admin);
        await DemoteToLegacyAsync(legacyTemplate);

        var (rep, repId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SetJobRoleAsync(repId, salesRole);

        var list = await SelfListAsync(rep);
        Assert.Contains(list, t => t.Id == newTemplate);         // الجديد ظاهر
        Assert.DoesNotContain(list, t => t.Id == legacyTemplate); // القديم مخفيّ (Legacy)
    }

    // (2) القالب المبذور «مبيعات B2C حسب الدورة» موجود ومنشور (القالب الجديد المستهدف للتفعيل).
    [Fact]
    public async Task SeededB2cByCourseTemplate_Exists_AndPublished()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var all = await (await admin.GetAsync("/api/report-templates"))
            .ReadAsync<List<ReportTemplateDto>>();

        var newB2c = Assert.Single(all!.Where(t => t.Title == B2cByCourseReportSchema.TemplateTitle));
        var detail = await (await admin.GetAsync($"/api/report-templates/{newB2c.Id}"))
            .ReadAsync<ReportTemplateDetailDto>();
        Assert.Equal(TemplateStatus.Published, detail!.Status);
    }

    // ===== الجزء 2: إيقاف الصعود بعد قائد الفريق =====

    // (3) اعتماد قائد الفريق يُغلق تقرير الفرد نهائيًّا بلا تصعيد للمدير (رغم وجود مدير مباشر).
    [Fact]
    public async Task TeamLeaderApproval_ClosesReport_NoManagerEscalation()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishReportTemplateAsync(admin);

        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (teamLeader, tlId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        // للموظّف مدير مباشر (managerId) وقائد فريق (tlId) معًا.
        var (employee, empId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        await TestAuth.CreateTeamWithLeaderAsync(_factory, tlId, empId);

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W20");
        Assert.Equal(tlId, submitted.CurrentApproverId); // يبدأ عند قائد الفريق

        var closed = await (await teamLeader.PostAsJsonAsync($"/api/submissions/{submitted.Id}/approve",
            new ApprovalActionRequest("اعتماد قائد الفريق"))).ReadAsync<SubmissionDto>();

        // اعتماد قائد الفريق ⇒ إغلاق نهائي، لا تصعيد للمدير.
        Assert.Equal(SubmissionStatus.Closed, closed!.Status);
        Assert.Null(closed.CurrentApproverId);
        Assert.NotNull(closed.ClosedAtUtc);
        Assert.NotEqual(managerId, closed.CurrentApproverId);
    }

    // (4) بعد إغلاق قائد الفريق للتقرير، لا يظهر التقرير في طابور اعتماد المدير.
    [Fact]
    public async Task ClosedByTeamLeader_NotInManagerPendingApprovals()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishReportTemplateAsync(admin);

        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (teamLeader, tlId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (employee, empId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        await TestAuth.CreateTeamWithLeaderAsync(_factory, tlId, empId);

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W21");
        var closed = await (await teamLeader.PostAsJsonAsync($"/api/submissions/{submitted.Id}/approve",
            new ApprovalActionRequest(null))).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Closed, closed!.Status);

        var pending = await (await manager.GetAsync("/api/submissions/pending-approvals"))
            .ReadAsync<List<SubmissionListItemDto>>();
        Assert.DoesNotContain(pending!, s => s.Id == submitted.Id);
    }

    // (5) المدير يرى التقرير المُغلق للقراءة فقط (ضمن نطاقه)، ولا يستطيع اعتماده.
    [Fact]
    public async Task Manager_CanReadClosedReport_ButCannotApprove()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishReportTemplateAsync(admin);

        var (manager, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (teamLeader, tlId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (employee, empId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        await TestAuth.CreateTeamWithLeaderAsync(_factory, tlId, empId);

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W22");
        var closed = await (await teamLeader.PostAsJsonAsync($"/api/submissions/{submitted.Id}/approve",
            new ApprovalActionRequest(null))).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Closed, closed!.Status);

        // قراءة فقط: المدير يفتح التقرير ضمن نطاقه لكن بلا صلاحية تحرير.
        var read = await manager.GetAsync($"/api/submissions/{submitted.Id}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var dto = await read.ReadAsync<SubmissionDto>();
        Assert.False(dto!.CanEdit);

        // محاولة الاعتماد من المدير تفشل (ليس المعتمِد، والتقرير مُغلق).
        var approveAttempt = await manager.PostAsJsonAsync($"/api/submissions/{submitted.Id}/approve",
            new ApprovalActionRequest(null));
        Assert.NotEqual(HttpStatusCode.OK, approveAttempt.StatusCode);
    }

    // (6) الخط الزمني بعد اعتماد قائد الفريق لا يحوي خطوات المدير/المدير العام/الرئيس.
    [Fact]
    public async Task Timeline_AfterTeamLeaderApproval_HasNoManagerOrSeniorSteps()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishReportTemplateAsync(admin);

        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (teamLeader, tlId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (employee, empId) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);
        await TestAuth.CreateTeamWithLeaderAsync(_factory, tlId, empId);

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W23");
        var closed = await (await teamLeader.PostAsJsonAsync($"/api/submissions/{submitted.Id}/approve",
            new ApprovalActionRequest(null))).ReadAsync<SubmissionDto>();
        Assert.Equal(SubmissionStatus.Closed, closed!.Status);

        var approverIds = closed.ApprovalSteps.Select(s => s.ApproverId).ToList();
        // الخطوة الوحيدة للاعتماد هي قائد الفريق، وليس المدير أو أي طبقة عليا.
        Assert.Contains(tlId, approverIds);
        Assert.DoesNotContain(managerId, approverIds);
    }

    // ===== الجزء 2 (توافق خلفي): الحفاظ على مسار البديل =====

    // (7) لا قائد فريق للموظّف ⇒ يُوجَّه للمدير المباشر (البديل محفوظ، لا إغلاق مبكّر).
    [Fact]
    public async Task Fallback_NoTeamLeader_RoutesToDirectManager()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishReportTemplateAsync(admin);

        var (_, managerId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee", managerId);

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W24");

        Assert.Equal(SubmissionStatus.Submitted, submitted.Status);
        Assert.Equal(managerId, submitted.CurrentApproverId); // لم يُغلَق؛ ذهب للمدير
    }

    // (8) لا قائد فريق ولا مدير مباشر ⇒ تصعيد للطبقة العليا (مدير عام) — البديل النهائي محفوظ.
    [Fact]
    public async Task Fallback_NoTeamLeaderNoManager_EscalatesToSeniorTier()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishReportTemplateAsync(admin);

        await TestAuth.CreateUserAsync(_factory, "GeneralManager"); // بديل عام مضمون
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee"); // بلا فريق وبلا مدير

        var submitted = await SubmitReportAsync(employee, templateId, fieldId, "2026-W25");

        Assert.Equal(SubmissionStatus.Submitted, submitted.Status);
        Assert.NotNull(submitted.CurrentApproverId); // لم يُغلَق؛ صُعِّد للطبقة العليا
    }
}
