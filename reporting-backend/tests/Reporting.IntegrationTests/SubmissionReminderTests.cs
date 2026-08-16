using Reporting.Application.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Reporting.Application.Notifications;
using Reporting.Domain.Entities.Org;
using Reporting.Domain.Entities.Submissions;
using Reporting.Domain.Entities.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Identity;
using Reporting.Infrastructure.Persistence;
using Reporting.Infrastructure.Services;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// اختبارات تذكير التقرير الأسبوعي (V1): تذكير واحد فقط للمستخدم النشط الذي لم يُسلِّم،
/// بلا تكرار عبر الدورات، واستثناء من سلّم تقريره. كل التذكيرات إشعارات داخل التطبيق
/// (البريد يبقى محكومًا ببوابة Email__Enabled وقائمة السماح).
/// </summary>
[Collection("Integration")]
public class SubmissionReminderTests
{
    private readonly CustomWebApplicationFactory _factory;
    public SubmissionReminderTests(CustomWebApplicationFactory factory) => _factory = factory;

    // الأربعاء 24 يونيو 2026 = موعد تسليم الموظّف لأسبوعه التشغيليّ.
    private static readonly DateOnly DueWednesday = new(2026, 6, 24);
    // المفتاح يُشتقّ من نفس السياسة التي يستعملها SubmissionReminderService بدل تصليبه: ترقيم
    // الأسابيع سلوك خادميّ، وتصليبه يجعل التسليم المبذور لا يطابق المفتاح الذي يبحث عنه الخادم.
    private static string WeekKey => ReportCalendarPolicy.WeekKeyFor(DueWednesday);

    private static SubmissionReminderService Service(WebApplicationFactory<Program> f)
    {
        var scopeFactory = f.Services.GetRequiredService<IServiceScopeFactory>();
        var opts = f.Services.GetRequiredService<IOptions<ReminderOptions>>();
        return new SubmissionReminderService(scopeFactory, opts, NullLogger<SubmissionReminderService>.Instance);
    }

    /// <summary>يُنشئ مستخدمًا نشطًا بمسمّى وظيفي أسبوعي مرتبط بقالب أساسي منشور، اختياريًا بتسليم قائم.</summary>
    private static async Task<Guid> SeedWeeklyReporterAsync(WebApplicationFactory<Program> f, bool hasSubmitted)
    {
        var jobRoleId = Guid.NewGuid();
        var email = $"reporter-{Guid.NewGuid():N}@test.local";

        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        db.JobRoles.Add(new JobRole
        {
            Id = jobRoleId, NameAr = "كاتب محتوى",
            Code = "CONTENT_" + Guid.NewGuid().ToString("N")[..6], IsActive = true
        });
        var template = new ReportTemplate
        {
            Id = Guid.NewGuid(), Title = "تقرير أسبوعي", JobRoleId = jobRoleId,
            Classification = TemplateClassification.Primary, IsActive = true, OwnerId = Guid.NewGuid()
        };
        db.ReportTemplates.Add(template);
        var version = new ReportTemplateVersion
        {
            Id = Guid.NewGuid(), ReportTemplateId = template.Id, VersionNumber = 1, IsPublished = true
        };
        db.ReportTemplateVersions.Add(version);
        await db.SaveChangesAsync();

        var user = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            FullName = "موظّف اختبار", IsActive = true, JobRoleId = jobRoleId
        };
        await users.CreateAsync(user, "Passw0rd#1");
        await users.AddToRoleAsync(user, "Employee");

        if (hasSubmitted)
        {
            db.ReportSubmissions.Add(new ReportSubmission
            {
                ReportTemplateVersionId = version.Id, SubmitterId = user.Id,
                PeriodType = PeriodType.Weekly, PeriodKey = WeekKey,
                Status = SubmissionStatus.Submitted, SubmittedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        return user.Id;
    }

    private static async Task<int> ReminderNotificationCountAsync(WebApplicationFactory<Program> f, Guid userId)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Notifications.CountAsync(n =>
            n.RecipientId == userId && n.Type == ReminderOptions.ReminderType);
    }

    [Fact]
    public async Task NotSubmitted_OnDueDate_CreatesExactlyOneReminder_NoDuplicateOnRerun()
    {
        var f = _factory;
        var userId = await SeedWeeklyReporterAsync(f, hasSubmitted: false);
        var svc = Service(f);

        var first = await svc.RunOnceAsync(DueWednesday);
        var second = await svc.RunOnceAsync(DueWednesday); // الدورة الثانية لا تُكرّر التذكير

        Assert.True(first >= 1);
        Assert.Equal(0, second);
        Assert.Equal(1, await ReminderNotificationCountAsync(f, userId));
    }

    [Fact]
    public async Task AlreadySubmitted_DoesNotRemind()
    {
        var f = _factory;
        var userId = await SeedWeeklyReporterAsync(f, hasSubmitted: true);
        var svc = Service(f);

        await svc.RunOnceAsync(DueWednesday);

        Assert.Equal(0, await ReminderNotificationCountAsync(f, userId));
    }

    [Fact]
    public async Task BeforeDueWindow_DoesNotRemind()
    {
        var f = _factory;
        var userId = await SeedWeeklyReporterAsync(f, hasSubmitted: false);
        var svc = Service(f);

        // الخميس 18 يونيو (بداية الأسبوع) — قبل موعد الموظّف (الأربعاء) ⇒ لا تذكير (LeadDays=0).
        await svc.RunOnceAsync(new DateOnly(2026, 6, 18));

        Assert.Equal(0, await ReminderNotificationCountAsync(f, userId));
    }
}
