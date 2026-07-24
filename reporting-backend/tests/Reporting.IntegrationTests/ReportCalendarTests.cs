using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Calendar;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Entities.Submissions;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// تقويم التقارير التشغيلي (Phase 5 §5/§6/§9/§14): كشف التقارير الأسبوعية الناقصة،
/// تأخّر الاعتماد للمستوى الأعلى فقط، وتجميع تقارير المبيعات اليومية أسبوعيًّا.
/// كلّ الحالات قائمة على الدور والنطاق (ScopeResolver) لا على اسم مستخدم بعينه.
/// </summary>
[Collection("Integration")]
public class ReportCalendarTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ReportCalendarTests(CustomWebApplicationFactory factory) => _factory = factory;

    // أسبوع تشغيلي ماضٍ بالكامل بالنسبة لتاريخ التشغيل (الخميس 04/06 → الأربعاء 10/06 2026).
    private const string PastWeek = "2026-W23";

    // ===== مساعدات =====

    private async Task<Guid> EnsureJobRoleAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var jr = await db.JobRoles.FirstOrDefaultAsync(j => j.Code == code);
        if (jr is null)
        {
            jr = new JobRole { NameAr = $"مسمّى {code}", Code = code };
            db.JobRoles.Add(jr);
            await db.SaveChangesAsync();
        }
        return jr.Id;
    }

    private async Task SetJobRoleAsync(Guid userId, Guid jobRoleId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var u = await db.Users.FirstAsync(x => x.Id == userId);
        u.JobRoleId = jobRoleId;
        await db.SaveChangesAsync();
    }

    /// <summary>ينشئ قالبًا أساسيًّا منشورًا مربوطًا بمسمّى وظيفي، ويعيد (معرّف القالب، معرّف الحقل المطلوب).</summary>
    private static async Task<(Guid TemplateId, Guid FieldId)> PublishPrimaryBoundAsync(HttpClient admin, Guid jobRoleId)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب تقويم {Guid.NewGuid():N}", null, jobRoleId, PeriodType.Weekly)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        var field = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("قيمة", "value", FieldType.Number, true, null, null)))
            .ReadAsync<TemplateFieldDto>();
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return (created.Id, field!.Id);
    }

    /// <summary>قالب عام (غير مربوط) منشور لاستخدامه في اختبارات تأخّر الاعتماد.</summary>
    private static async Task<(Guid TemplateId, Guid FieldId)> PublishGenericAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
            new CreateTemplateRequest($"قالب عام {Guid.NewGuid():N}", null, null, PeriodType.Weekly)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        var field = await (await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("قيمة", "value", FieldType.Number, true, null, null)))
            .ReadAsync<TemplateFieldDto>();
        await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null);
        return (created.Id, field!.Id);
    }

    private static async Task SubmitAsync(HttpClient client, Guid templateId, Guid fieldId, PeriodType type, string periodKey)
    {
        var draft = await (await client.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, type, periodKey))).ReadAsync<SubmissionDto>();
        await client.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(fieldId, null, 1m, null, null, null) }));
        await client.PostAsync($"/api/submissions/{draft.Id}/submit", null);
    }

    // ===== §5 / §14.10 — كشف التقارير الأسبوعية الناقصة =====

    [Fact]
    public async Task MissingReports_DetectsWeeklyNonSubmitters_InScope()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var weeklyRole = await EnsureJobRoleAsync($"WK_{Guid.NewGuid():N}");
        var (templateId, fieldId) = await PublishPrimaryBoundAsync(admin, weeklyRole);

        var (mgrClient, mgrId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (submitter, submitterId) = await TestAuth.CreateUserAsync(_factory, "Employee", mgrId);
        var (_, missingId) = await TestAuth.CreateUserAsync(_factory, "Employee", mgrId);
        await SetJobRoleAsync(submitterId, weeklyRole);
        await SetJobRoleAsync(missingId, weeklyRole);

        // واحد يُسلّم، والآخر لا.
        await SubmitAsync(submitter, templateId, fieldId, PeriodType.Weekly, PastWeek);

        var report = await (await mgrClient.GetAsync($"/api/report-calendar/missing-reports?weekKey={PastWeek}"))
            .ReadAsync<MissingReportsReport>();

        Assert.NotNull(report);
        Assert.Equal("department", report!.ScopeType);
        Assert.True(report.CanViewRows);
        var submitterRow = report.Rows.Single(r => r.UserId == submitterId);
        var missingRow = report.Rows.Single(r => r.UserId == missingId);
        Assert.NotEqual("missing", submitterRow.Status);   // سلَّم (متأخّر لأن التشغيل بعد المهلة)
        Assert.Equal("missing", missingRow.Status);
        Assert.True(report.MissingCount >= 1);
        Assert.Contains(report.TeamShortfalls, t => t.Missing >= 1 || t.Late >= 1);
    }

    [Fact]
    public async Task MissingReports_MalformedWeekKey_400()
    {
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var res = await manager.GetAsync("/api/report-calendar/missing-reports?weekKey=2026-25");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task MissingReports_Anonymous_401()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/report-calendar/missing-reports");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ===== §6 / §14.11 / §14.12 / §14.13 — تأخّر الاعتماد =====

    [Fact]
    public async Task ApprovalDelays_VisibleToHigherLevel_NotToCurrentApprover()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishGenericAsync(admin);

        var (mgrClient, mgrId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (tlClient, tlId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader", mgrId);
        var (empClient, empId) = await TestAuth.CreateUserAsync(_factory, "Employee", tlId);

        // الموظّف يُرسل تقريرًا أسبوعيًّا لأسبوع ماضٍ ⇒ المعتمِد الحالي = قائد الفريق، والمهلة انقضت.
        await SubmitAsync(empClient, templateId, fieldId, PeriodType.Weekly, PastWeek);

        // المستوى الأعلى (المدير) يرى التأخّر.
        var mgrView = await (await mgrClient.GetAsync("/api/report-calendar/approval-delays"))
            .ReadAsync<ApprovalDelaysReport>();
        Assert.NotNull(mgrView);
        var row = mgrView!.Rows.SingleOrDefault(r => r.SubmitterId == empId);
        Assert.NotNull(row);
        Assert.Equal(tlId, row!.ApproverId);
        Assert.True(row.DaysOverdue > 0);

        // المعتمِد الحالي (قائد الفريق) لا يرى تقريره المعلَّق كتأخّر (يُعرض لمن فوقه).
        var tlView = await (await tlClient.GetAsync("/api/report-calendar/approval-delays"))
            .ReadAsync<ApprovalDelaysReport>();
        Assert.DoesNotContain(tlView!.Rows, r => r.SubmitterId == empId);
    }

    [Fact]
    public async Task ApprovalDelays_NormalEmployee_SeesNothing()
    {
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");
        var report = await (await employee.GetAsync("/api/report-calendar/approval-delays"))
            .ReadAsync<ApprovalDelaysReport>();
        Assert.NotNull(report);
        Assert.Equal(0, report!.DelayCount);
        Assert.Empty(report.Rows);
    }

    [Fact]
    public async Task ApprovalDelays_OutOfScopeDelay_NotVisible()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, fieldId) = await PublishGenericAsync(admin);

        // فرع مستقلّ تمامًا: موظّف تحت قائد فريق تحت مدير مختلف.
        var (_, otherMgrId) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var (_, otherTlId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader", otherMgrId);
        var (foreignEmp, foreignEmpId) = await TestAuth.CreateUserAsync(_factory, "Employee", otherTlId);
        await SubmitAsync(foreignEmp, templateId, fieldId, PeriodType.Weekly, PastWeek);

        // مدير في فرع آخر لا يرى تأخّر هذا الموظّف (خارج نطاقه).
        var (mgrClient, _) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var report = await (await mgrClient.GetAsync("/api/report-calendar/approval-delays"))
            .ReadAsync<ApprovalDelaysReport>();
        Assert.DoesNotContain(report!.Rows, r => r.SubmitterId == foreignEmpId);
    }

    // ===== §9 / §14.9 / §14.22 / §14.23 — تجميع تقارير المبيعات اليومية أسبوعيًّا =====

    // W28 = 2026-07-04 (السبت) → 2026-07-10 (الجمعة). أيّام العمل بعد أرضية الإطلاق (2026-07-04):
    // الأحد 05 → الخميس 09 = 5 أيّام. السبت 04 والجمعة 10 مستبعدان (عطلة أسبوعية).
    private const string W28Key = "2026-W28";

    /// <summary>
    /// يُدرج تقريرًا يوميًّا فعليًّا مباشرةً في القاعدة (يتجاوز حارس CreateAsync الذي يمنع الجمعة/السبت)
    /// لإثبات §5: التقرير الفعليّ على يوم غير منطبق يبقى محفوظًا لكنه لا يدخل بسط الالتزام.
    /// </summary>
    private async Task InsertDailyDirectAsync(Guid templateId, Guid submitterId, string dayKey)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var versionId = await db.ReportTemplateVersions
            .Where(v => v.ReportTemplateId == templateId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => v.Id)
            .FirstAsync();
        db.ReportSubmissions.Add(new ReportSubmission
        {
            ReportTemplateVersionId = versionId,
            SubmitterId = submitterId,
            PeriodType = PeriodType.Daily,
            PeriodKey = dayKey,
            Status = SubmissionStatus.Submitted,
            SubmittedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task SalesDailyCompliance_W28_ExpectedFiveBusinessDays_ExcludesFridaySaturday()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var salesRole = await EnsureJobRoleAsync("SALES_B2C");
        var (templateId, fieldId) = await PublishPrimaryBoundAsync(admin, salesRole);

        var (tlClient, tlId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader");
        var (completeRep, completeId) = await TestAuth.CreateUserAsync(_factory, "Employee", tlId);
        var (partialRep, partialId) = await TestAuth.CreateUserAsync(_factory, "Employee", tlId);
        await SetJobRoleAsync(completeId, salesRole);
        await SetJobRoleAsync(partialId, salesRole);

        // أيّام العمل الخمسة (الأحد→الخميس) تُرسَل عبر الـ API (تمرّ حارس CreateAsync).
        var businessDays = new[] { "2026-07-05", "2026-07-06", "2026-07-07", "2026-07-08", "2026-07-09" };
        foreach (var d in businessDays)
            await SubmitAsync(completeRep, templateId, fieldId, PeriodType.Daily, d);

        // §5: تقريران فعليّان على السبت 04 والجمعة 10 يُدرَجان مباشرةً (يمنعهما الـ API) —
        // يجب ألّا يزيدا البسط (submitted يبقى 5 لا 7) ولا التوقّع.
        await InsertDailyDirectAsync(templateId, completeId, "2026-07-04"); // السبت (أرضية الإطلاق)
        await InsertDailyDirectAsync(templateId, completeId, "2026-07-10"); // الجمعة

        // المندوب الجزئي: 3 أيّام عمل فقط ⇒ أسبوع ناقص يحتاج مراجعة (المتوقّع 5، الناقص 2).
        foreach (var d in businessDays[..3])
            await SubmitAsync(partialRep, templateId, fieldId, PeriodType.Daily, d);

        var report = await (await tlClient.GetAsync($"/api/report-calendar/sales-daily-compliance?weekKey={W28Key}"))
            .ReadAsync<SalesDailyComplianceReport>();

        Assert.NotNull(report);
        Assert.Equal("team", report!.ScopeType);
        var completeRow = report.Rows.Single(r => r.UserId == completeId);
        var partialRow = report.Rows.Single(r => r.UserId == partialId);

        // §6 #4: المتوقّع = 5 (أيّام العمل) لا 7 (عدّ خام للدورة).
        Assert.Equal(5, completeRow.ExpectedDays);
        // §6 #5: السبت 04 والجمعة 10 لا يدخلان البسط — المُسلَّم المحتسَب = 5 فقط.
        Assert.Equal(5, completeRow.SubmittedDays);
        Assert.True(completeRow.IsComplete);
        Assert.False(completeRow.NeedsReview);

        Assert.Equal(5, partialRow.ExpectedDays);
        Assert.Equal(3, partialRow.SubmittedDays);
        Assert.Equal(2, partialRow.MissingDays);
        Assert.False(partialRow.IsComplete);
        Assert.True(partialRow.NeedsReview);   // §14.23 أسبوع ناقص ⇒ يحتاج مراجعة
    }

    [Fact]
    public async Task SalesDailyCompliance_MalformedWeekKey_400()
    {
        var (manager, _) = await TestAuth.CreateUserAsync(_factory, "Manager");
        var res = await manager.GetAsync("/api/report-calendar/sales-daily-compliance?weekKey=bad");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
