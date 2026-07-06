using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reporting.Application.Common;
using Reporting.Application.Notifications;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// خدمة خلفية للتذكير بالتقارير الأسبوعية (V1) — تذكير واحد خفيف قبل/يوم موعد التسليم
/// للمستخدمين النشطين المتوقَّع منهم تقرير أسبوعي ولم يُرسِلوه بعد. بلا تكرار: تذكير واحد
/// لكل (مستخدم، أسبوع) مضمون عبر فحص وجود إشعار تذكير سابق بنفس الرابط. معطّلة افتراضيًا.
/// لا ترسل بريدًا بنفسها — تُنشئ إشعارًا داخل التطبيق، والبريد يخضع لبوابة Email__Enabled وقائمة السماح.
/// </summary>
public class SubmissionReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ReminderOptions _options;
    private readonly ILogger<SubmissionReminderService> _logger;

    public SubmissionReminderService(IServiceScopeFactory scopeFactory, IOptions<ReminderOptions> options, ILogger<SubmissionReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    // حالات «أُرسِل التقرير» — تُعدّ تسليمًا قائمًا فلا يُذكَّر صاحبها.
    private static readonly SubmissionStatus[] SubmittedStatuses =
    {
        SubmissionStatus.Submitted, SubmissionStatus.Returned, SubmissionStatus.ApprovedByDirectManager,
        SubmissionStatus.ApprovedByNextLevel, SubmissionStatus.Escalated, SubmissionStatus.Closed,
        SubmissionStatus.Visible
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromMinutes(Math.Max(15, _options.PollMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_options.Enabled)
                    await RunOnceAsync(null, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SubmissionReminderService cycle failed");
            }

            try { await Task.Delay(delay, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>تشغيل دورة تذكير واحدة. <paramref name="todayOverride"/> لاختبار حتمي مستقل عن ساعة النظام.</summary>
    /// <returns>عدد التذكيرات المُنشأة في هذه الدورة.</returns>
    public async Task<int> RunOnceAsync(DateOnly? todayOverride = null, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var today = todayOverride ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var weekKey = ReportCalendarPolicy.WeekKeyFor(today);
        var (weekStart, weekEnd) = ReportCalendarPolicy.WeekRange(weekKey);
        var link = $"/app/submissions?period={weekKey}";

        // المسمّيات الوظيفية المتوقَّع منها تقرير أسبوعي = قالب أساسي منشور فعّال ودوريّته أسبوعية.
        var reportingRoleIds = await db.ReportTemplates.AsNoTracking()
            .Where(t => t.JobRoleId != null && t.IsActive
                        && t.Classification == TemplateClassification.Primary
                        && db.ReportTemplateVersions.Any(v => v.ReportTemplateId == t.Id && v.IsPublished))
            .Select(t => t.JobRoleId!.Value)
            .Distinct()
            .ToListAsync(ct);
        if (reportingRoleIds.Count == 0) return 0;

        var weeklyRoleIds = (await db.JobRoles.AsNoTracking()
                .Where(j => reportingRoleIds.Contains(j.Id))
                .Select(j => new { j.Id, j.Code })
                .ToListAsync(ct))
            .Where(j => ReportCadencePolicy.ExpectedCadence(j.Code) == PeriodType.Weekly)
            .Select(j => j.Id)
            .ToHashSet();
        if (weeklyRoleIds.Count == 0) return 0;

        var candidates = await db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.JobRoleId != null && weeklyRoleIds.Contains(u.JobRoleId!.Value)
                        && u.Email != null && u.Email != "")
            .Select(u => u.Id)
            .ToListAsync(ct);
        if (candidates.Count == 0) return 0;

        // الدور النظامي (Identity) لكل مرشّح لتحديد موعد التسليم الخاص بدوره.
        var rolesByUser = await UserPrimaryRolesAsync(db, candidates, ct);

        // من سلّم تقرير هذا الأسبوع (أي حالة بعد المسودّة) — يُستثنى.
        var submitted = (await db.ReportSubmissions.AsNoTracking()
            .Where(s => s.PeriodKey == weekKey && s.PeriodType == PeriodType.Weekly
                        && SubmittedStatuses.Contains(s.Status)
                        && candidates.Contains(s.SubmitterId))
            .Select(s => s.SubmitterId)
            .ToListAsync(ct)).ToHashSet();

        // من له إجازة معتمدة تغطّي الأسبوع كاملًا — لا يُذكَّر.
        var onFullWeekLeave = (await db.LeaveRequests.AsNoTracking()
            .Where(r => r.Type == LeaveRequestType.Leave
                        && r.Status == LeaveRequestStatus.HrApproved
                        && candidates.Contains(r.RequesterUserId)
                        && r.StartDate <= weekStart && r.EndDate >= weekEnd)
            .Select(r => r.RequesterUserId)
            .ToListAsync(ct)).ToHashSet();

        // من سبق تذكيره لهذا الأسبوع (إشعار تذكير بنفس الرابط) — لا يُكرَّر.
        var alreadyReminded = (await db.Notifications.AsNoTracking()
            .Where(n => n.Type == ReminderOptions.ReminderType && n.Link == link
                        && candidates.Contains(n.RecipientId))
            .Select(n => n.RecipientId)
            .ToListAsync(ct)).ToHashSet();

        var dueRecipients = new List<Guid>();
        foreach (var id in candidates)
        {
            if (submitted.Contains(id) || onFullWeekLeave.Contains(id) || alreadyReminded.Contains(id))
                continue;

            var role = RoleAccess.PrimaryRole(rolesByUser.GetValueOrDefault(id, new List<string>()));
            var due = ReportCalendarPolicy.DueDateForRole(weekKey, role);
            var window = due.AddDays(-Math.Max(0, _options.LeadDays));

            // التذكير ضمن نافذة [due - LeadDays, due] فقط — قبل/يوم الموعد، لا بعده.
            if (today >= window && today <= due)
                dueRecipients.Add(id);
        }

        if (dueRecipients.Count == 0) return 0;

        await notifications.NotifyManyAsync(
            dueRecipients,
            ReminderOptions.ReminderType,
            "تذكير: لم تُرسِل تقريرك الأسبوعي بعد",
            $"يرجى إرسال {ReportCalendarPolicy.ShortWeekLabel(weekKey)} قبل انتهاء الموعد.",
            link,
            ct);

        _logger.LogInformation("Submission reminders created: {Count} for week {WeekKey}", dueRecipients.Count, weekKey);
        return dueRecipients.Count;
    }

    private static async Task<Dictionary<Guid, List<string>>> UserPrimaryRolesAsync(
        AppDbContext db, IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0) return new Dictionary<Guid, List<string>>();
        var pairs = await (from ur in db.UserRoles
                           join r in db.Roles on ur.RoleId equals r.Id
                           where userIds.Contains(ur.UserId) && r.Name != null
                           select new { ur.UserId, r.Name }).ToListAsync(ct);
        return pairs.GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name!).ToList());
    }
}
