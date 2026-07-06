using Reporting.Application.Common;
using Reporting.Application.Dashboard;
using Reporting.Application.Reports;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// اللوحة التنفيذية (ERDS Phase 6 — Preview) — قراءة فقط، طبقة عرض فقط.
/// تُركّب فوق محرّكَي التجميع (Phase 4 مبيعات B2C/B2B، Phase 5/5.5 تنفيذ Pods/عملاء/مشاريع)
/// ولا تنفّذ أيّ استعلام قاعدة بيانات بنفسها ولا تُعيد حساب أيّ مؤشّر من مؤشّرات المحرّكات.
/// كل ما تفعله: استدعاء نتائج المحرّكات (المحكومة بالنطاق داخلها عبر IScopeResolver) ثم إعادة
/// تجميع/تشكيل هذه النتائج المجمّعة مسبقًا في DTOs مستقلّة. أيّ نسبة معروضة هنا (إنتاجية/معدّل)
/// مشتقّة من مجاميع المحرّك الجاهزة فقط — لا استعلام إضافي، لا مسّ لأيّ تسليم/قالب/اعتماد.
/// </summary>
public class ExecutiveDashboardService : IExecutiveDashboardService
{
    private readonly IReportingAggregationService _sales;
    private readonly IPodExecutionAggregationService _exec;
    private readonly ICurrentUser _currentUser;

    private const int TopN = 10;

    public ExecutiveDashboardService(
        IReportingAggregationService sales,
        IPodExecutionAggregationService exec,
        ICurrentUser currentUser)
    {
        _sales = sales;
        _exec = exec;
        _currentUser = currentUser;
    }

    // مستوى العرض المشتقّ من الدور (يطابق دلالة محرّكات التجميع).
    private string ViewLevel()
        => RoleAccess.ScopeTypeFor(RoleAccess.PrimaryRole(_currentUser.Roles)) switch
        {
            "own" => "self",
            "team" => "team",
            "department" => "department",
            _ => "summary",
        };

    private static PodExecutionFilter Pod(ExecutiveDashboardFilter f) =>
        new(f.PeriodType, f.PeriodKey, f.TeamId, f.EmployeeId, f.Client, f.Project);

    private static AggregationFilter SalesFilter(ExecutiveDashboardFilter f) =>
        new(f.PeriodType, f.PeriodKey, f.EmployeeId, f.TeamId, null, null);

    // نسبة مباشرة آمنة القسمة (لكل ساعة)، مقرّبة لمنزلتين — اشتقاق عرض من مجاميع جاهزة (لا إعادة حساب مؤشّر محرّك).
    private static decimal Per(decimal num, decimal den) => den > 0 ? Math.Round(num / den, 2) : 0m;

    // ترتيب مستوى الخطر لاختيار «الأسوأ» عبر الصفوف (نصّ حرّ عربي/إنجليزي) — نفس منطق محرّك التنفيذ.
    private static int RiskRank(string risk)
    {
        var r = (risk ?? string.Empty).Trim().ToLowerInvariant();
        if (r.Length == 0) return 0;
        if (r.Contains("حرج") || r.Contains("critical") || r.Contains("جدا") || r.Contains("جدًا")) return 4;
        if (r.Contains("مرتفع") || r.Contains("عال") || r.Contains("high")) return 3;
        if (r.Contains("متوسط") || r.Contains("medium") || r.Contains("mid")) return 2;
        if (r.Contains("منخفض") || r.Contains("low") || r.Contains("بسيط")) return 1;
        return 0;
    }

    private static string WorstRisk(IEnumerable<string> risks)
    {
        string worst = string.Empty;
        int worstRank = -1;
        foreach (var r in risks)
        {
            var rank = RiskRank(r ?? string.Empty);
            if (rank > worstRank) { worstRank = rank; worst = r ?? string.Empty; }
        }
        return worst;
    }

    private static bool HasText(string? s) => !string.IsNullOrWhiteSpace(s);

    // ───────────────────────── 1) Overview ─────────────────────────

    public async Task<Result<DashboardOverviewDto>> GetOverviewAsync(ExecutiveDashboardFilter filter, CancellationToken ct = default)
    {
        var podR = await _exec.AggregateByPodAsync(Pod(filter), ct);
        if (!podR.Succeeded) return Result<DashboardOverviewDto>.Failure(podR.Error!, podR.ErrorCode);
        var b2cR = await _sales.AggregateB2cByCourseAsync(SalesFilter(filter), ct);
        if (!b2cR.Succeeded) return Result<DashboardOverviewDto>.Failure(b2cR.Error!, b2cR.ErrorCode);
        var b2bR = await _sales.AggregateB2bByServiceAsync(SalesFilter(filter), ct);
        if (!b2bR.Succeeded) return Result<DashboardOverviewDto>.Failure(b2bR.Error!, b2bR.ErrorCode);

        var pods = podR.Value!.Rows;
        var b2c = b2cR.Value!.Rows;
        var b2b = b2bR.Value!.Rows;

        var dto = new DashboardOverviewDto(
            WorkHours: pods.Sum(r => r.WorkHours) + b2c.Sum(r => r.WorkHours) + b2b.Sum(r => r.WorkHours),
            Clients: pods.Where(r => HasText(r.Client)).Select(r => r.Client).Distinct().Count(),
            Projects: pods.Where(r => HasText(r.Project)).Select(r => (r.Client, r.Project)).Distinct().Count(),
            Revenue: pods.Sum(r => r.Revenue) + b2c.Sum(r => r.Revenue) + b2b.Sum(r => r.Revenue),
            Leads: pods.Sum(r => r.Leads) + b2c.Sum(r => r.Leads) + b2b.Sum(r => r.Leads),
            Sales: b2c.Sum(r => r.Sales) + b2b.Sum(r => r.Won),
            Content: pods.Sum(r => r.ContentPieces),
            Designs: pods.Sum(r => r.DesignsCompleted),
            Videos: pods.Sum(r => r.VideosCompleted),
            PublishedPosts: pods.Sum(r => r.PostsPublished),
            ViewLevel: ViewLevel());

        return Result<DashboardOverviewDto>.Success(dto);
    }

    // ───────────────────────── 2) Sales ─────────────────────────

    public async Task<Result<DashboardSalesDto>> GetSalesAsync(ExecutiveDashboardFilter filter, CancellationToken ct = default)
    {
        var b2cR = await _sales.AggregateB2cByCourseAsync(SalesFilter(filter), ct);
        if (!b2cR.Succeeded) return Result<DashboardSalesDto>.Failure(b2cR.Error!, b2cR.ErrorCode);
        var b2bR = await _sales.AggregateB2bByServiceAsync(SalesFilter(filter), ct);
        if (!b2bR.Succeeded) return Result<DashboardSalesDto>.Failure(b2bR.Error!, b2bR.ErrorCode);

        var b2c = b2cR.Value!.Rows;
        var b2b = b2bR.Value!.Rows;

        var b2cRows = b2c.Select(r => new DashboardSalesRowDto(
            r.PeriodKey, r.EmployeeId, r.EmployeeName, r.TeamId, r.Course,
            r.WorkHours, r.Leads, r.Sales, r.Revenue, r.ConversionRate, r.RevenuePerHour)).ToList();

        var b2bRows = b2b.Select(r => new DashboardSalesRowDto(
            r.PeriodKey, r.EmployeeId, r.EmployeeName, r.TeamId, r.Service,
            r.WorkHours, r.Leads, r.Won, r.Revenue, r.WinRate, r.RevenuePerHour)).ToList();

        var kpis = new DashboardSalesKpisDto(
            TotalLeads: b2c.Sum(r => r.Leads) + b2b.Sum(r => r.Leads),
            TotalSales: b2c.Sum(r => r.Sales) + b2b.Sum(r => r.Won),
            TotalRevenue: b2c.Sum(r => r.Revenue) + b2b.Sum(r => r.Revenue),
            TotalWorkHours: b2c.Sum(r => r.WorkHours) + b2b.Sum(r => r.WorkHours),
            B2cSales: b2c.Sum(r => r.Sales),
            B2cRevenue: b2c.Sum(r => r.Revenue),
            B2bWon: b2b.Sum(r => r.Won),
            B2bRevenue: b2b.Sum(r => r.Revenue));

        return Result<DashboardSalesDto>.Success(new DashboardSalesDto(kpis, b2cRows, b2bRows, ViewLevel()));
    }

    // ───────────────────────── 3) Pods ─────────────────────────

    public async Task<Result<DashboardPodsDto>> GetPodsAsync(ExecutiveDashboardFilter filter, CancellationToken ct = default)
    {
        var podR = await _exec.AggregateByPodAsync(Pod(filter), ct);
        if (!podR.Succeeded) return Result<DashboardPodsDto>.Failure(podR.Error!, podR.ErrorCode);

        var pods = podR.Value!.Rows
            .GroupBy(r => new { r.TeamId, r.TeamName })
            .Select(g =>
            {
                var wh = g.Sum(r => r.WorkHours);
                var content = g.Sum(r => r.ContentPieces);
                var designs = g.Sum(r => r.DesignsCompleted);
                var videos = g.Sum(r => r.VideosCompleted);
                var published = g.Sum(r => r.PostsPublished);
                return new DashboardPodDto(
                    g.Key.TeamId, g.Key.TeamName, wh, content, designs, videos, published,
                    Delayed: g.Sum(r => r.DelayedItems),
                    Revenue: g.Sum(r => r.Revenue),
                    Productivity: Per(content + designs + videos + published, wh));
            })
            .OrderByDescending(p => p.WorkHours)
            .ToList();

        return Result<DashboardPodsDto>.Success(new DashboardPodsDto(pods, ViewLevel()));
    }

    // ───────────────────────── 4) Clients ─────────────────────────

    public async Task<Result<DashboardClientsDto>> GetClientsAsync(ExecutiveDashboardFilter filter, CancellationToken ct = default)
    {
        var cliR = await _exec.AggregateByClientAsync(Pod(filter), ct);
        if (!cliR.Succeeded) return Result<DashboardClientsDto>.Failure(cliR.Error!, cliR.ErrorCode);

        var clients = cliR.Value!.Rows
            .GroupBy(r => r.Client)
            .Select(g => new DashboardClientDto(
                Client: g.Key,
                WorkHours: g.Sum(r => r.TotalWorkHours),
                Projects: g.Where(r => HasText(r.Project)).Select(r => r.Project).Distinct().Count(),
                Revenue: g.Sum(r => r.TotalRevenue),
                Spend: g.Sum(r => r.TotalSpend),
                Content: g.Sum(r => r.TotalContentPieces),
                Designs: g.Sum(r => r.TotalDesigns),
                Videos: g.Sum(r => r.TotalVideos),
                Posts: g.Sum(r => r.TotalPublishedPosts),
                RiskLevel: WorstRisk(g.Select(r => r.RiskLevel))))
            .OrderByDescending(c => c.WorkHours)
            .ToList();

        return Result<DashboardClientsDto>.Success(new DashboardClientsDto(clients, ViewLevel()));
    }

    // ───────────────────────── 5) Projects ─────────────────────────

    public async Task<Result<DashboardProjectsDto>> GetProjectsAsync(ExecutiveDashboardFilter filter, CancellationToken ct = default)
    {
        var projR = await _exec.AggregateByProjectAsync(Pod(filter), ct);
        if (!projR.Succeeded) return Result<DashboardProjectsDto>.Failure(projR.Error!, projR.ErrorCode);
        // الإيراد ليس ضمن تجميع المشاريع — نُركّبه من تجميع العملاء (لكل عميل/مشروع) عند وجوده.
        var cliR = await _exec.AggregateByClientAsync(Pod(filter), ct);
        if (!cliR.Succeeded) return Result<DashboardProjectsDto>.Failure(cliR.Error!, cliR.ErrorCode);

        var revenueByCp = cliR.Value!.Rows
            .GroupBy(r => (r.Client, r.Project))
            .ToDictionary(g => g.Key, g => g.Sum(r => r.TotalRevenue));

        var projects = projR.Value!.Rows
            .GroupBy(r => new { r.Client, r.Project })
            .Select(g => new DashboardProjectDto(
                Client: g.Key.Client,
                Project: g.Key.Project,
                WorkHours: g.Sum(r => r.WorkHours),
                CompletionRate: Math.Round(g.Average(r => r.CompletionRate), 1),
                DelayedTasks: g.Sum(r => r.DelayedTasks),
                BlockedTasks: g.Sum(r => r.BlockedTasks),
                ProgressPercent: Math.Round(g.Average(r => r.ProgressPercentAvg), 1),
                Revenue: revenueByCp.TryGetValue((g.Key.Client, g.Key.Project), out var rev) ? rev : 0m))
            .OrderByDescending(p => p.WorkHours)
            .ToList();

        return Result<DashboardProjectsDto>.Success(new DashboardProjectsDto(projects, ViewLevel()));
    }

    // ───────────────────────── 6) Workload ─────────────────────────

    public async Task<Result<DashboardWorkloadDto>> GetWorkloadAsync(ExecutiveDashboardFilter filter, CancellationToken ct = default)
    {
        var podR = await _exec.AggregateByPodAsync(Pod(filter), ct);
        if (!podR.Succeeded) return Result<DashboardWorkloadDto>.Failure(podR.Error!, podR.ErrorCode);

        var rows = podR.Value!.Rows;

        var employees = rows
            .GroupBy(r => new { r.EmployeeId, r.EmployeeName, r.TeamId, r.TeamName })
            .Select(g =>
            {
                var wh = g.Sum(r => r.WorkHours);
                var output = g.Sum(r => r.ContentPieces + r.DesignsCompleted + r.VideosCompleted + r.PostsPublished);
                return new DashboardWorkloadEmployeeDto(
                    g.Key.EmployeeId, g.Key.EmployeeName, g.Key.TeamId, g.Key.TeamName,
                    TotalWorkHours: wh,
                    ProjectsCount: g.Where(r => HasText(r.Project)).Select(r => r.Project).Distinct().Count(),
                    ClientsCount: g.Where(r => HasText(r.Client)).Select(r => r.Client).Distinct().Count(),
                    WorkUnits: g.Where(r => HasText(r.Client) || HasText(r.Project)).Select(r => (r.Client, r.Project)).Distinct().Count(),
                    Productivity: Per(output, wh));
            })
            .OrderByDescending(e => e.TotalWorkHours)
            .ToList();

        var teams = rows
            .GroupBy(r => new { r.TeamId, r.TeamName })
            .Select(g =>
            {
                var wh = g.Sum(r => r.WorkHours);
                var output = g.Sum(r => r.ContentPieces + r.DesignsCompleted + r.VideosCompleted + r.PostsPublished);
                return new DashboardWorkloadTeamDto(
                    g.Key.TeamId, g.Key.TeamName,
                    TotalWorkHours: wh,
                    ProjectsCount: g.Where(r => HasText(r.Project)).Select(r => r.Project).Distinct().Count(),
                    ClientsCount: g.Where(r => HasText(r.Client)).Select(r => r.Client).Distinct().Count(),
                    WorkUnits: g.Where(r => HasText(r.Client) || HasText(r.Project)).Select(r => (r.Client, r.Project)).Distinct().Count(),
                    Productivity: Per(output, wh));
            })
            .OrderByDescending(t => t.TotalWorkHours)
            .ToList();

        return Result<DashboardWorkloadDto>.Success(new DashboardWorkloadDto(teams, employees, ViewLevel()));
    }

    // ───────────────────────── 7) Risks ─────────────────────────

    public async Task<Result<DashboardRisksDto>> GetRisksAsync(ExecutiveDashboardFilter filter, CancellationToken ct = default)
    {
        var podR = await _exec.AggregateByPodAsync(Pod(filter), ct);
        if (!podR.Succeeded) return Result<DashboardRisksDto>.Failure(podR.Error!, podR.ErrorCode);
        var projR = await _exec.AggregateByProjectAsync(Pod(filter), ct);
        if (!projR.Succeeded) return Result<DashboardRisksDto>.Failure(projR.Error!, projR.ErrorCode);

        var podRows = podR.Value!.Rows;
        var projRows = projR.Value!.Rows;

        var topRiskyProjects = projRows
            .GroupBy(r => new { r.Client, r.Project })
            .Select(g => new DashboardRiskyProjectDto(
                g.Key.Client, g.Key.Project,
                WorstRisk(g.Select(r => r.RiskLevel)),
                g.Sum(r => r.DelayedTasks), g.Sum(r => r.BlockedTasks),
                Math.Round(g.Average(r => r.ProgressPercentAvg), 1)))
            .Where(p => RiskRank(p.RiskLevel) > 0 || p.DelayedTasks > 0 || p.BlockedTasks > 0)
            .OrderByDescending(p => RiskRank(p.RiskLevel))
            .ThenByDescending(p => p.DelayedTasks + p.BlockedTasks)
            .Take(TopN)
            .ToList();

        var topDelayedClients = podRows
            .Where(r => HasText(r.Client))
            .GroupBy(r => r.Client)
            .Select(g => new DashboardDelayedClientDto(g.Key, g.Sum(r => r.DelayedItems), g.Sum(r => r.MissedPosts)))
            .Where(c => c.DelayedItems > 0 || c.MissedPosts > 0)
            .OrderByDescending(c => c.DelayedItems)
            .ThenByDescending(c => c.MissedPosts)
            .Take(TopN)
            .ToList();

        var topPressuredPods = podRows
            .GroupBy(r => new { r.TeamId, r.TeamName })
            .Select(g => new DashboardPressuredPodDto(
                g.Key.TeamId, g.Key.TeamName,
                g.Sum(r => r.WorkHours), g.Sum(r => r.DelayedItems), g.Sum(r => r.BlockedTasks)))
            .OrderByDescending(p => p.DelayedItems + p.BlockedTasks)
            .ThenByDescending(p => p.WorkHours)
            .Take(TopN)
            .ToList();

        var topBlockedTasks = projRows
            .GroupBy(r => new { r.Client, r.Project })
            .Select(g => new DashboardBlockedTasksDto(g.Key.Client, g.Key.Project, g.Sum(r => r.BlockedTasks)))
            .Where(b => b.BlockedTasks > 0)
            .OrderByDescending(b => b.BlockedTasks)
            .Take(TopN)
            .ToList();

        // معدّل التأخير مؤشّر جاهز على صفّ المحرّك (ProductivityIndicators.DelayRate) — لا يُعاد حسابه، يُرتَّب فقط.
        var topDelayRate = podRows
            .Where(r => r.ProductivityIndicators.DelayRate > 0)
            .OrderByDescending(r => r.ProductivityIndicators.DelayRate)
            .Take(TopN)
            .Select(r => new DashboardDelayRateDto(
                r.TeamId, r.TeamName, r.EmployeeId, r.EmployeeName, r.Client, r.Project,
                r.ProductivityIndicators.DelayRate))
            .ToList();

        var dto = new DashboardRisksDto(
            topRiskyProjects, topDelayedClients, topPressuredPods, topBlockedTasks, topDelayRate, ViewLevel());
        return Result<DashboardRisksDto>.Success(dto);
    }
}
