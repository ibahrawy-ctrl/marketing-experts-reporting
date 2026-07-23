using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Calendar;
using Reporting.Application.Common;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Entities.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// REPORTING-CYCLE-SUBMISSION-STATUS-CONSISTENCY-R1 — PHASE 3 (توافق DTO/API). اختبارات تكامل على
/// طبقة HTTP لنقطة <c>GET /api/reporting-calendar/my-cycles</c> على قاعدة معزولة (reporting_calendar_iso).
/// تُثبِت: (أ) إضافة الحقل الموحّد <c>unified</c> إلى كل صفّ دورة دون كسر الحقول القديمة (توافق خلفيّ)؛
/// (ب) اتّساق الحقل الموحّد مع المحرّك الخلفيّ (دورة ماضية بلا تسليم ⇒ OverdueNotSubmitted، الحالية ⇒ DueNow)؛
/// (ج) الواجهة لا تحسب الحالة — المصدر خادميّ حصرًا؛ (د) RBAC: غير المصادَق ⇒ 401.
/// </summary>
[Collection("CalendarIsolated")]
public class UnifiedStatusApiTests
{
    private readonly CalendarIsolatedFactory _factory;

    public UnifiedStatusApiTests(CalendarIsolatedFactory factory) => _factory = factory;

    /// <summary>يجعل المستخدم متوقَّعًا منه تقرير أسبوعيّ عبر مسمّى له قالب أساسيّ أسبوعيّ منشور (بذر مباشر).</summary>
    private async Task<Guid> SeedWeeklyReportingRoleAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var jobRole = new JobRole { NameAr = $"مسمّى {Guid.NewGuid():N}", Code = null };
        db.JobRoles.Add(jobRole);

        var template = new ReportTemplate
        {
            Title = $"قالب {Guid.NewGuid():N}",
            JobRoleId = jobRole.Id,
            Classification = TemplateClassification.Primary,
            DefaultPeriodType = PeriodType.Weekly,
            IsActive = true,
            Status = TemplateStatus.Published,
            OwnerId = userId
        };
        db.ReportTemplates.Add(template);

        // أرضيّة الانطباق = MAX(إنشاء المستخدم، أوّل نشر للقالب). لاختبار اشتقاق الحالة (DueNow/OverdueNotSubmitted)
        // يجب أن يكون المستخدم مؤهَّلًا قبل الدورات المفحوصة، وإلّا صنّفتها بوّابة الانطباق NotRequired (لا التزام رجعيّ
        // منتصف الدورة — قاعدة REPORT-EXPECTED-SUBMISSION-STATUS-R1). لذا نُرجِع تاريخ النشر وإنشاء المستخدم 120 يومًا.
        var eligibleFrom = DateTime.UtcNow.AddDays(-120);

        var version = new ReportTemplateVersion
        {
            ReportTemplateId = template.Id,
            VersionNumber = 1,
            IsPublished = true,
            PublishedAtUtc = eligibleFrom
        };
        db.ReportTemplateVersions.Add(version);

        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.JobRoleId = jobRole.Id;
        user.CreatedAtUtc = eligibleFrom;
        await db.SaveChangesAsync();
        return version.Id;
    }

    // ===== 1) موظّف مُسنَد ⇒ كل دورة تحمل الحقل الموحّد، والحقول القديمة باقية (توافق خلفيّ) =====
    [Fact]
    public async Task MyCycles_AssignedEmployee_PopulatesUnified_KeepsLegacyFields()
    {
        var (employee, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedWeeklyReportingRoleAsync(uid);

        var res = await employee.GetAsync("/api/reporting-calendar/my-cycles");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        // (أ) فحص خام للـJSON: أسماء الحقول القديمة باقية + الحقل الموحّد الجديد مضاف.
        var json = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var firstCycle = doc.RootElement.GetProperty("cycles").EnumerateArray().First();
        Assert.True(firstCycle.TryGetProperty("status", out _));        // حقل قديم
        Assert.True(firstCycle.TryGetProperty("isOverdue", out _));     // حقل قديم
        Assert.True(firstCycle.TryGetProperty("cycleKey", out _));      // حقل قديم
        Assert.True(firstCycle.TryGetProperty("unified", out var unifiedEl)); // حقل جديد (إضافيّ)
        Assert.Equal(JsonValueKind.Object, unifiedEl.ValueKind);
        Assert.True(unifiedEl.TryGetProperty("unifiedStatus", out _));

        // (ب) فحص مُنمَّط: كل الدورات تحمل الحقل الموحّد مُعبَّأً.
        var data = await res.ReadAsync<MyCyclesDto>();
        Assert.NotNull(data);
        Assert.NotEmpty(data!.Cycles);
        Assert.All(data.Cycles, c => Assert.NotNull(c.Unified));
    }

    // ===== 2) اتّساق مع المحرّك: دورة ماضية بلا تسليم ⇒ OverdueNotSubmitted =====
    [Fact]
    public async Task MyCycles_PastCycleNoSubmission_UnifiedIsOverdueNotSubmitted()
    {
        var (employee, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedWeeklyReportingRoleAsync(uid);

        var data = await (await employee.GetAsync("/api/reporting-calendar/my-cycles?past=8&future=1"))
            .ReadAsync<MyCyclesDto>();

        // دورة ماضية واضحة (تجاوزت موعدها) بلا تسليم ⇒ متأخّرة غير مُسلَّمة.
        // تُختار أقدم دورة ماضية منطبقة (بدايتها في/بعد أرضيّة الإطلاق الأسبوعيّ 2026-07-04)؛ فالدورات
        // قبل الأرضيّة غير منطبقة (لا متأخّرة) ولا يصحّ التأكيد عليها بحالة OverdueNotSubmitted.
        var past = data!.Cycles.First(c => c.IsPast && c.CycleStart >= ApplicabilityFloorPolicy.WeeklyReportingLaunchFloor);
        Assert.NotNull(past.Unified);
        Assert.Equal(UnifiedCycleStatus.OverdueNotSubmitted, past.Unified!.UnifiedStatus);
        Assert.True(past.Unified.IsLate);
        Assert.Equal("alert", past.Unified.Severity);
        // لا تسليم لهذه الدورة.
        Assert.False(past.Unified.HasSubmission);
    }

    // ===== 3) اتّساق مع المحرّك: الدورة الحالية بلا تسليم ⇒ DueNow (لا متأخّر) =====
    [Fact]
    public async Task MyCycles_CurrentCycleNoSubmission_UnifiedIsDueNow()
    {
        var (employee, uid) = await TestAuth.CreateUserAsync(_factory, "Employee");
        await SeedWeeklyReportingRoleAsync(uid);

        var data = await (await employee.GetAsync("/api/reporting-calendar/my-cycles"))
            .ReadAsync<MyCyclesDto>();

        var current = data!.Cycles.Single(c => c.IsCurrent);
        Assert.NotNull(current.Unified);
        Assert.Equal(UnifiedCycleStatus.DueNow, current.Unified!.UnifiedStatus);
        Assert.False(current.Unified.IsLate);
    }

    // ===== 4) موظّف بلا مسمّى تقارير ⇒ الحقل الموحّد باقٍ (NotAssigned) والنقطة تعمل (توافق خلفيّ) =====
    [Fact]
    public async Task MyCycles_NoReportingRole_UnifiedNotAssigned_EndpointStillWorks()
    {
        var (employee, _) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var res = await employee.GetAsync("/api/reporting-calendar/my-cycles");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var data = await res.ReadAsync<MyCyclesDto>();
        Assert.NotEmpty(data!.Cycles);
        Assert.All(data.Cycles, c =>
        {
            Assert.NotNull(c.Unified);
            Assert.Equal(UnifiedCycleStatus.NotAssigned, c.Unified!.UnifiedStatus);
            Assert.False(c.Unified.IsAssigned);
        });
    }

    // ===== 5) RBAC: غير مصادَق ⇒ 401 =====
    [Fact]
    public async Task MyCycles_Unauthenticated_Returns401()
    {
        var anon = _factory.CreateClient();
        var res = await anon.GetAsync("/api/reporting-calendar/my-cycles");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
