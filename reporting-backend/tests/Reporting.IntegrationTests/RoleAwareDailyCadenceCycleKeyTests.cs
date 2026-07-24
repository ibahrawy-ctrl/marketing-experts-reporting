using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.Submissions;
using Reporting.Domain.Entities.Submissions;
using Reporting.Domain.Entities.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Reporting.Infrastructure.Services;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// ROLE-AWARE-PERSONAL-REPORT-SUBMISSION-ACCESS-R1 — P10: عقد الدورية اليوميّة مع مفتاح الدورة (CycleKey).
/// يُثبِت أنّ العرض الموحّد الشخصيّ للموظّف — بفلتر cadence=Daily ومفتاح دورة أسبوعيّ (2026-W28) —
/// يُرجِع التقارير اليوميّة الفعليّة الواقعة داخل نطاق الدورة (CycleRange السبت→الجمعة) بمفاتيحها القانونيّة
/// كما هي، مع: ظهور تقرير السبت الطوعيّ (السبت مسموح للإنشاء لكنه ليس يوم توقّع)، استبعاد الجمعة كليًّا
/// (لا توقّع ولا صفّ)، غياب «متوقّع مفقود» زائف لليوم الذي قُدِّم فعلًا، ثبات لا-تغيُّريّة Summary.Total==TotalCount،
/// وثبات التقسيم الصفحيّ (Pagination). قراءة/تحقّق فقط — لا يمسّ ScopeResolver ولا التوجيه ولا الهيكل التنظيميّ.
///
/// الساعة الثابتة: الإثنين 2026-07-20 09:00Z (W30 الجارية) ⇒ W28 دورة ماضية مكتملة.
/// CycleRange(W28) = السبت 2026-07-04 → الجمعة 2026-07-10؛ أيّام العمل المتوقَّعة = الأحد 07-05 → الخميس 07-09.
/// </summary>
[Collection("Integration")]
public class RoleAwareDailyCadenceCycleKeyTests
{
    private readonly CustomWebApplicationFactory _factory;
    public RoleAwareDailyCadenceCycleKeyTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static readonly DateTimeOffset Fixed = new(2026, 7, 20, 9, 0, 0, TimeSpan.Zero);
    private static DateTime EarlyAnchor => new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    // مفتاح دورة W28 عبر مصدر الحقيقة (السبت 07-04 → الجمعة 07-10).
    private static string W28Key => ReportingCalendarPolicy.CycleKeyFor(new DateOnly(2026, 7, 8));

    private const string Saturday04 = "2026-07-04"; // السبت (الأرضيّة) — مسموح للإنشاء لكنه ليس يوم توقّع.
    private const string Sunday05 = "2026-07-05";   // الأحد — أوّل يوم عمل متوقَّع.
    private const string Tuesday07 = "2026-07-07";  // الثلاثاء — يوم عمل داخل W28.
    private const string Thursday09 = "2026-07-09"; // الخميس — آخر يوم عمل متوقَّع في W28.
    private const string Friday10 = "2026-07-10";   // الجمعة — عطلة، مستبعَدة كليًّا من التوقّع.

    private sealed class FixedClock : ISystemClock
    {
        public FixedClock(DateTimeOffset now) => UtcNow = now;
        public DateTimeOffset UtcNow { get; }
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        private readonly string[] _roles;
        public TestCurrentUser(Guid userId, params string[] roles) { UserId = userId; _roles = roles; }
        public Guid? UserId { get; }
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => _roles;
        public bool IsInRole(string role) => _roles.Contains(role);
        public bool IsInAnyRole(params string[] roles) => _roles.Intersect(roles).Any();
    }

    private sealed record Seeded(Guid TemplateId, Guid VersionId);

    // مطالبة يوميّة عبر مسمّى المبيعات (SALES_B2C ⇒ ExpectedCadence=Daily) + قالب أساسي منشور (get-or-create).
    private async Task<Seeded> SeedDailyExpectedAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var role = await db.JobRoles.FirstOrDefaultAsync(r => r.Code == "SALES_B2C");
        if (role is null)
        {
            role = new Reporting.Domain.Entities.Org.JobRole { NameAr = "مبيعات يوميّ", Code = "SALES_B2C", IsActive = true };
            db.JobRoles.Add(role);
            await db.SaveChangesAsync();
        }

        var template = await db.ReportTemplates.FirstOrDefaultAsync(
            t => t.JobRoleId == role.Id && t.Classification == TemplateClassification.Primary && t.IsActive);
        Guid versionId;
        if (template is null)
        {
            template = new ReportTemplate
            {
                Title = "قالب المبيعات اليومي",
                JobRoleId = role.Id,
                Classification = TemplateClassification.Primary,
                DefaultPeriodType = PeriodType.Weekly,
                IsActive = true,
                Status = TemplateStatus.Published,
                OwnerId = userId
            };
            db.ReportTemplates.Add(template);
            var version = new ReportTemplateVersion
            {
                ReportTemplateId = template.Id,
                VersionNumber = 1,
                IsPublished = true,
                PublishedAtUtc = EarlyAnchor
            };
            db.ReportTemplateVersions.Add(version);
            await db.SaveChangesAsync();
            versionId = version.Id;
        }
        else
        {
            versionId = (await db.ReportTemplateVersions.FirstAsync(v => v.ReportTemplateId == template.Id && v.IsPublished)).Id;
        }

        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.JobRoleId = role.Id;
        user.CreatedAtUtc = EarlyAnchor;
        user.IsActive = true;
        await db.SaveChangesAsync();
        return new Seeded(template.Id, versionId);
    }

    private async Task InsertDailyAsync(Guid versionId, Guid submitterId, string dayKey, SubmissionStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ReportSubmissions.Add(new ReportSubmission
        {
            ReportTemplateVersionId = versionId,
            SubmitterId = submitterId,
            PeriodType = PeriodType.Daily,
            PeriodKey = dayKey,
            Status = status,
            SubmittedAtUtc = status == SubmissionStatus.Draft ? null : Fixed.UtcDateTime
        });
        await db.SaveChangesAsync();
    }

    private async Task<UnifiedSubmissionOverviewDto> OverviewAsync(Guid actorId, string[] roles, UnifiedSubmissionFilter filter)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = new FixedClock(Fixed);
        var currentUser = new TestCurrentUser(actorId, roles);
        var scopeResolver = new ScopeResolver(db, currentUser);
        var grants = new ReportViewGrantService(db, currentUser, scope.ServiceProvider.GetRequiredService<IAuditService>());
        var templates = new ReportTemplateService(db, currentUser, scopeResolver, null!);
        var expected = new ExpectedSubmissionStatusResolver(db, clock, templates);
        var svc = new SubmissionService(db, currentUser, null!, null!, scopeResolver, null!, null!, grants, expected, clock);
        var result = await svc.GetOverviewAsync(filter);
        Assert.True(result.Succeeded, result.Error);
        Assert.NotNull(result.Value);
        return result.Value!;
    }

    private static IEnumerable<UnifiedSubmissionRowDto> Daily(UnifiedSubmissionOverviewDto o, Guid uid) =>
        o.Items.Where(r => r.SubmitterId == uid && r.PeriodType == PeriodType.Daily);

    // ===== 1) cadence=Daily + مفتاح W28: التقرير اليوميّ الفعليّ داخل الدورة يظهر بمفتاحه القانونيّ =====
    [Fact]
    public async Task P10_DailyCadence_CycleKey_ReturnsActualReportsWithinCycle_CanonicalKeys()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var s = await SeedDailyExpectedAsync(uid);
        await InsertDailyAsync(s.VersionId, uid, Tuesday07, SubmissionStatus.Submitted);

        var o = await OverviewAsync(uid, new[] { Roles.Employee },
            new UnifiedSubmissionFilter(PeriodKey: W28Key, Cadence: SubmissionCadenceFilter.Daily, PageSize: 1000));

        // الصفّ الفعليّ لليوم داخل الدورة ظاهر بمفتاحه الخام القانونيّ (لم يُسقَط ولا يُعاد كتابته).
        var actual = Assert.Single(Daily(o, uid),
            x => x.RowKind == SubmissionRowKind.ExistingSubmission && x.PeriodKey == Tuesday07);
        Assert.True(actual.HasSubmission);
        Assert.Equal(nameof(SubmissionStatus.Submitted), actual.Status);

        // كل صفّ يوميّ يقع فعلًا داخل نطاق الدورة CycleRange (السبت→الجمعة).
        var (start, end) = ReportingCalendarPolicy.CycleRange(W28Key);
        Assert.All(Daily(o, uid), r =>
        {
            Assert.True(ReportingCalendarPolicy.TryCanonicalDay(r.PeriodKey, out var day), r.PeriodKey);
            Assert.True(day >= start && day <= end, $"{r.PeriodKey} خارج نطاق الدورة");
        });
    }

    // ===== 2) السبت الطوعيّ ظاهر كتسليم فعليّ لكن لا يُنتِج «متوقّع مفقود» (السبت ليس يوم توقّع) =====
    [Fact]
    public async Task P10_SaturdayVoluntarySubmission_Visible_NotExpected()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var s = await SeedDailyExpectedAsync(uid);
        await InsertDailyAsync(s.VersionId, uid, Saturday04, SubmissionStatus.Submitted); // السبت مسموح.

        var o = await OverviewAsync(uid, new[] { Roles.Employee },
            new UnifiedSubmissionFilter(PeriodKey: W28Key, Cadence: SubmissionCadenceFilter.Daily, PageSize: 1000));

        // تقرير السبت الفعليّ مرئيّ داخل الدورة.
        var sat = Assert.Single(Daily(o, uid),
            x => x.RowKind == SubmissionRowKind.ExistingSubmission && x.PeriodKey == Saturday04);
        Assert.True(sat.HasSubmission);
        Assert.Equal(DayOfWeek.Saturday, new DateOnly(2026, 7, 4).DayOfWeek);

        // لا صفّ «متوقّع مفقود» للسبت (ليس يوم عمل متوقَّع).
        Assert.DoesNotContain(Daily(o, uid),
            x => x.RowKind == SubmissionRowKind.ExpectedMissingSubmission && x.PeriodKey == Saturday04);
        Assert.False(ReportingCalendarPolicy.IsDailyExpectedBusinessDay(new DateOnly(2026, 7, 4)));
    }

    // ===== 3) الجمعة مستبعَدة كليًّا: لا توقّع ولا أيّ صفّ يوميّ يقع على جمعة =====
    [Fact]
    public async Task P10_Friday_ExcludedEntirely_NoExpectedNoRow()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SeedDailyExpectedAsync(uid);

        var o = await OverviewAsync(uid, new[] { Roles.Employee },
            new UnifiedSubmissionFilter(PeriodKey: W28Key, Cadence: SubmissionCadenceFilter.Daily, PageSize: 1000));

        // لا «متوقّع مفقود» لجمعة الدورة 07-10.
        Assert.DoesNotContain(Daily(o, uid),
            x => x.RowKind == SubmissionRowKind.ExpectedMissingSubmission && x.PeriodKey == Friday10);

        // ولا أيّ صفّ يوميّ (بأيّ نوع) يقع على جمعة إطلاقًا.
        Assert.All(Daily(o, uid), r =>
        {
            Assert.True(ReportingCalendarPolicy.TryCanonicalDay(r.PeriodKey, out var day), r.PeriodKey);
            Assert.NotEqual(DayOfWeek.Friday, day.DayOfWeek);
        });
    }

    // ===== 4) لا «متوقّع مفقود» زائف لليوم الذي قُدِّم فعلًا؛ وبقيّة أيّام العمل تبقى متوقَّعة بمفاتيح قانونيّة =====
    [Fact]
    public async Task P10_NoFalseExpectedForSubmittedDay_OthersRemainExpected_CanonicalKeys()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var s = await SeedDailyExpectedAsync(uid);
        await InsertDailyAsync(s.VersionId, uid, Tuesday07, SubmissionStatus.Submitted);

        var o = await OverviewAsync(uid, new[] { Roles.Employee },
            new UnifiedSubmissionFilter(PeriodKey: W28Key, Cadence: SubmissionCadenceFilter.Daily, PageSize: 1000));

        // لا صفّ توقّع زائف لليوم المُقدَّم.
        Assert.DoesNotContain(Daily(o, uid),
            x => x.RowKind == SubmissionRowKind.ExpectedMissingSubmission && x.PeriodKey == Tuesday07);

        // أيّام العمل الأخرى (الأحد/الخميس) تبقى متوقَّعة بمفاتيح yyyy-MM-dd القانونيّة.
        Assert.Contains(Daily(o, uid),
            x => x.RowKind == SubmissionRowKind.ExpectedMissingSubmission && x.PeriodKey == Sunday05);
        Assert.Contains(Daily(o, uid),
            x => x.RowKind == SubmissionRowKind.ExpectedMissingSubmission && x.PeriodKey == Thursday09);

        // كل صفّ متوقّع يقع على يوم عمل (الأحد→الخميس) وبمفتاح قانونيّ صالح.
        Assert.All(Daily(o, uid).Where(x => x.RowKind == SubmissionRowKind.ExpectedMissingSubmission), r =>
        {
            Assert.True(ReportingCalendarPolicy.IsValidDayKey(r.PeriodKey), r.PeriodKey);
            var day = DateOnly.ParseExact(r.PeriodKey, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            Assert.True(ReportingCalendarPolicy.IsDailyExpectedBusinessDay(day), r.PeriodKey);
        });
    }

    // ===== 5) لا-تغيُّريّة العقد: Summary.Total == TotalCount تحت cadence=Daily + مفتاح الدورة =====
    [Fact]
    public async Task P10_SummaryTotal_EqualsTotalCount_Invariant()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var s = await SeedDailyExpectedAsync(uid);
        await InsertDailyAsync(s.VersionId, uid, Tuesday07, SubmissionStatus.Submitted);

        var o = await OverviewAsync(uid, new[] { Roles.Employee },
            new UnifiedSubmissionFilter(PeriodKey: W28Key, Cadence: SubmissionCadenceFilter.Daily, PageSize: 1000));

        Assert.Equal(o.Summary.Total, o.TotalCount);
        Assert.True(o.TotalCount >= 1);
    }

    // ===== 6) ثبات التقسيم الصفحيّ: تجميع الصفحات يعيد إنتاج القائمة الكاملة بلا ازدواج ولا فقد =====
    [Fact]
    public async Task P10_StablePagination_PagesReconstructFullOrderedList()
    {
        var (_, uid) = await TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var s = await SeedDailyExpectedAsync(uid);
        await InsertDailyAsync(s.VersionId, uid, Tuesday07, SubmissionStatus.Submitted);

        var full = await OverviewAsync(uid, new[] { Roles.Employee },
            new UnifiedSubmissionFilter(PeriodKey: W28Key, Cadence: SubmissionCadenceFilter.Daily, PageSize: 1000));
        var expectedKeys = full.Items.Select(Composite).ToList();
        var total = full.TotalCount;
        Assert.Equal(expectedKeys.Count, total);

        // تجميع صفحات صغيرة (PageSize=2) بالترتيب يجب أن يطابق القائمة الكاملة حرفيًّا.
        const int size = 2;
        var collected = new List<string>();
        for (var page = 1; collected.Count < total; page++)
        {
            var p = await OverviewAsync(uid, new[] { Roles.Employee },
                new UnifiedSubmissionFilter(PeriodKey: W28Key, Cadence: SubmissionCadenceFilter.Daily, Page: page, PageSize: size));
            Assert.Equal(total, p.TotalCount);              // TotalCount ثابت عبر الصفحات.
            Assert.True(p.Items.Count <= size);             // لا تتجاوز الصفحة حجمها.
            Assert.NotEmpty(p.Items);                        // تقدُّم مضمون (لا صفحة فارغة قبل الاكتمال).
            collected.AddRange(p.Items.Select(Composite));
        }

        Assert.Equal(expectedKeys, collected);               // نفس الترتيب، بلا ازدواج ولا فقد.
        Assert.Equal(collected.Count, collected.Distinct().Count());
    }

    private static string Composite(UnifiedSubmissionRowDto r) =>
        $"{r.RowKind}:{r.PeriodType}:{r.PeriodKey}:{r.SubmitterId}";
}
