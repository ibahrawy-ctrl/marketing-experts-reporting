using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.HrOperations;
using Reporting.Application.Security;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// P2-HR-009 — لوحة عمليّات الموارد البشريّة وطوابير الإجراءات.
///
/// <para>ما يُثبَت هنا هو ما لا يُرى بالعين في الواجهة:</para>
/// <list type="number">
/// <item><b>البطاقة لا تخالف تفصيلها بنيويًّا</b> — العدد يأتي من المجموعة ذاتها لا من عدّ مستقلّ.</item>
/// <item><b>لا رقم خارج النطاق</b> — عدّاد المُشاهِد لا يحمل صفًّا لا يستطيع فتحه.</item>
/// <item><b>الرؤية ليست تصديرًا</b> — مفتاحان منفصلان، وكلّ تصدير له أثر في <c>AuditLog</c>.</item>
/// <item><b>404 لا 403 عند مغادرة النطاق</b> — لا يُستدلّ على وجود موظّف من رمز الاستجابة.</item>
/// </list>
/// </summary>
[Collection("Phase2")]
public class HrOperationsApiTests
{
    private readonly Phase2WebApplicationFactory _factory;

    public HrOperationsApiTests(Phase2WebApplicationFactory factory) => _factory = factory;

    private const int Cycles = 4;

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage res) =>
        JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.Clone();

    private static JsonElement Card(JsonElement dashboard, string key) =>
        dashboard.GetProperty("cards").EnumerateArray()
            .First(c => c.GetProperty("key").GetString() == key);

    private static IEnumerable<JsonElement> Rows(JsonElement queue) =>
        queue.GetProperty("rows").EnumerateArray();

    private static async Task<Guid> AttendanceTypeIdAsync(HttpClient client, string code)
    {
        var types = await JsonAsync(await client.GetAsync("/api/attendance/types"));
        return types.EnumerateArray().First(t => t.GetProperty("code").GetString() == code)
            .GetProperty("id").GetGuid();
    }

    /// <summary>بلاغ حضور مُقدَّم فعلًا ⇒ الواقعة تنتظر ردّ الموظّف (الطابور ٦).</summary>
    private static async Task<Guid> ReportIncidentAsync(HttpClient reporter, Guid subjectId, Guid typeId)
    {
        var res = await reporter.PostAsJsonAsync("/api/attendance", new
        {
            subjectUserId = subjectId,
            incidentTypeId = typeId,
            incidentDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            startTime = "09:30:00",
            returnTime = "10:15:00",
            description = "تأخّر صباحيّ موثَّق للاختبار.",
            submitImmediately = true
        });
        res.EnsureSuccessStatusCode();
        return (await JsonAsync(res)).GetProperty("id").GetGuid();
    }

    // ═══════════════ ① التخويل: مفتاح صريح لا يمنحه أيّ دور ضمنًا ═══════════════

    [Fact]
    public async Task Dashboard_Requires_An_Explicit_Permission_No_Role_Grants_Implicitly()
    {
        var (employee, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (leader, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var (hr, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Hr);
        var (admin, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Admin);

        // غياب المفتاح العامّ قبل تحديد أيّ مورد ⇒ 403 عند البوّابة (لا كشف عن مورد بعينه).
        foreach (var client in new[] { employee, leader, hr, admin })
        {
            Assert.Equal(HttpStatusCode.Forbidden,
                (await client.GetAsync("/api/hr-operations/dashboard")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await client.GetAsync("/api/hr-operations/queues/reports-missing")).StatusCode);
        }
    }

    /// <summary>الرؤية لا تُورِث التصدير: إخراج البيانات خارج النظام قرار منح مستقلّ.</summary>
    [Fact]
    public async Task Viewing_Does_Not_Grant_Exporting()
    {
        var (viewer, _) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Hr, permissions: AppPermissions.HrOperationsView);

        Assert.Equal(HttpStatusCode.OK,
            (await viewer.GetAsync("/api/hr-operations/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await viewer.GetAsync("/api/hr-operations/queues/reports-missing/export")).StatusCode);
    }

    [Fact]
    public async Task Export_Permission_Alone_Does_Not_Open_The_Dashboard()
    {
        var (exporter, _) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Hr, permissions: AppPermissions.HrOperationsExport);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await exporter.GetAsync("/api/hr-operations/dashboard")).StatusCode);
    }

    // ═══════════════ ② شكل اللوحة: أحد عشر طابورًا كاملة ═══════════════

    [Fact]
    public async Task Dashboard_Returns_All_Eleven_Queues_With_Scope_Context()
    {
        var (viewer, _) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Manager, permissions: AppPermissions.HrOperationsView);

        var body = await JsonAsync(await viewer.GetAsync(
            $"/api/hr-operations/dashboard?recentCycles={Cycles}"));

        var keys = body.GetProperty("cards").EnumerateArray()
            .Select(c => c.GetProperty("key").GetString()!).ToList();

        Assert.Equal(11, keys.Count);
        Assert.Equal(HrOperationsCatalog.All.Select(HrOperationsCatalog.Key).ToList(), keys);

        // النطاق يُعلَن مع الأرقام كي لا يُقرأ رقم خارج سياقه.
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("scope").GetProperty("scopeType").GetString()));
        Assert.NotEmpty(body.GetProperty("periodKeys").EnumerateArray());
    }

    [Fact]
    public async Task An_Unknown_Queue_Key_Is_Not_Found()
    {
        var (viewer, _) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Manager, permissions: AppPermissions.HrOperationsView);

        Assert.Equal(HttpStatusCode.NotFound,
            (await viewer.GetAsync("/api/hr-operations/queues/no-such-queue")).StatusCode);
    }

    // ═══════════════ ③ البطاقة = تفصيلها (الضمانة البنيويّة) ═══════════════

    /// <summary>
    /// أخطر عيب في أيّ لوحة عمليّات: بطاقة تقول «٧» وتفصيل يعرض «٤». هنا العددان
    /// مشتقّان من المجموعة نفسها، ونثبته على طابور فيه صفوف حقيقيّة وتحت مرشِّح مضيِّق.
    /// </summary>
    [Fact]
    public async Task Every_Card_Count_Equals_Its_Drilldown_Total_Under_The_Same_Filter()
    {
        var (viewer, viewerId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.TeamLeader,
            permissions: new[] { AppPermissions.HrOperationsView, AppPermissions.AttendanceReport });
        var (_, employeeId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: viewerId);

        var typeId = await AttendanceTypeIdAsync(viewer, "Late");
        await ReportIncidentAsync(viewer, employeeId, typeId);

        foreach (var query in new[] { $"recentCycles={Cycles}", $"recentCycles={Cycles}&userId={employeeId}" })
        {
            var dashboard = await JsonAsync(await viewer.GetAsync($"/api/hr-operations/dashboard?{query}"));

            foreach (var card in dashboard.GetProperty("cards").EnumerateArray())
            {
                var key = card.GetProperty("key").GetString()!;
                var detail = await JsonAsync(await viewer.GetAsync(
                    $"/api/hr-operations/queues/{key}?{query}&pageSize=200"));

                Assert.Equal(card.GetProperty("count").GetInt32(), detail.GetProperty("totalCount").GetInt32());
                Assert.Equal(card.GetProperty("breachedCount").GetInt32(),
                    detail.GetProperty("breachedCount").GetInt32());
            }
        }
    }

    /// <summary>الواقعة المُبلَّغ عنها تظهر فعلًا في طابورها — البطاقة ليست صفرًا زائفًا.</summary>
    [Fact]
    public async Task A_Submitted_Incident_Appears_In_The_Awaiting_Employee_Queue()
    {
        var (viewer, viewerId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.TeamLeader,
            permissions: new[] { AppPermissions.HrOperationsView, AppPermissions.AttendanceReport });
        var (_, employeeId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: viewerId);

        var typeId = await AttendanceTypeIdAsync(viewer, "Late");
        var incidentId = await ReportIncidentAsync(viewer, employeeId, typeId);

        var queue = await JsonAsync(await viewer.GetAsync(
            "/api/hr-operations/queues/attendance-awaiting-employee?pageSize=200"));

        var row = Rows(queue).Single(r => r.GetProperty("entityId").GetGuid() == incidentId);
        Assert.Equal(employeeId, row.GetProperty("subjectUserId").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("nextActionAr").GetString()));

        // مهلة جارية معلومة، ولم تُخرَق بعد لأنّ البلاغ قُدِّم للتوّ.
        Assert.False(row.GetProperty("slaBreached").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, row.GetProperty("slaDueAtUtc").ValueKind);
    }

    // ═══════════════ ④ لا رقم ولا صفّ خارج النطاق ═══════════════

    [Fact]
    public async Task A_Leader_Never_Sees_A_Row_About_Another_Leaders_Team()
    {
        var (leader, leaderId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.TeamLeader,
            permissions: new[] { AppPermissions.HrOperationsView, AppPermissions.AttendanceReport });
        var (_, ownEmployeeId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: leaderId);

        var (stranger, strangerLeaderId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.TeamLeader, permissions: AppPermissions.AttendanceReport);
        var (_, strangerEmployeeId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: strangerLeaderId);

        var typeId = await AttendanceTypeIdAsync(stranger, "Late");
        var foreignIncident = await ReportIncidentAsync(stranger, strangerEmployeeId, typeId);
        var ownIncident = await ReportIncidentAsync(leader, ownEmployeeId, typeId);

        var queue = await JsonAsync(await leader.GetAsync(
            "/api/hr-operations/queues/attendance-awaiting-employee?pageSize=200"));

        var ids = Rows(queue).Select(r => r.GetProperty("entityId").GetGuid()).ToList();
        Assert.Contains(ownIncident, ids);
        Assert.DoesNotContain(foreignIncident, ids);

        // ولا موضوعٌ من خارج النطاق يتسلّل إلى أيّ صفّ.
        Assert.DoesNotContain(Rows(queue),
            r => r.GetProperty("subjectUserId").GetGuid() == strangerEmployeeId);
    }

    /// <summary>موظّف خارج النطاق لا يُميَّز عن موظّف معدوم — لا في الرمز ولا في النصّ.</summary>
    [Fact]
    public async Task Out_Of_Scope_UserId_Is_Indistinguishable_From_A_Nonexistent_One()
    {
        var (leader, leaderId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.TeamLeader, permissions: AppPermissions.HrOperationsView);
        await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee, managerId: leaderId);

        var (_, strangerLeaderId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var (_, strangerId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: strangerLeaderId);

        static async Task<string> ShapeAsync(HttpResponseMessage res)
        {
            var body = await JsonAsync(res);
            return string.Join("|", body.EnumerateObject()
                .Where(p => p.Name != "traceId")
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => $"{p.Name}={p.Value}"));
        }

        foreach (var path in new[]
                 {
                     "/api/hr-operations/dashboard?userId=",
                     "/api/hr-operations/queues/reports-missing?userId="
                 })
        {
            var outOfScope = await leader.GetAsync(path + strangerId);
            var nonexistent = await leader.GetAsync(path + Guid.NewGuid());

            Assert.Equal(HttpStatusCode.NotFound, outOfScope.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, nonexistent.StatusCode);
            Assert.Equal(await ShapeAsync(nonexistent), await ShapeAsync(outOfScope));
        }
    }

    // ═══════════════ ⑤ المرشِّحات تضيّق ولا توسّع ═══════════════

    [Fact]
    public async Task Filters_Only_Narrow_And_Never_Introduce_A_Foreign_Row()
    {
        var (leader, leaderId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.TeamLeader,
            permissions: new[] { AppPermissions.HrOperationsView, AppPermissions.AttendanceReport });
        var (_, firstId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: leaderId);
        var (_, secondId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: leaderId);

        var typeId = await AttendanceTypeIdAsync(leader, "Late");
        await ReportIncidentAsync(leader, firstId, typeId);
        await ReportIncidentAsync(leader, secondId, typeId);

        const string q = "/api/hr-operations/queues/attendance-awaiting-employee?pageSize=200";
        var all = await JsonAsync(await leader.GetAsync(q));
        var narrowed = await JsonAsync(await leader.GetAsync($"{q}&userId={firstId}"));

        Assert.True(narrowed.GetProperty("totalCount").GetInt32() < all.GetProperty("totalCount").GetInt32());
        Assert.All(Rows(narrowed), r => Assert.Equal(firstId, r.GetProperty("subjectUserId").GetGuid()));

        var allIds = Rows(all).Select(r => r.GetProperty("entityId").GetGuid()).ToHashSet();
        Assert.All(Rows(narrowed), r => Assert.Contains(r.GetProperty("entityId").GetGuid(), allIds));
    }

    [Fact]
    public async Task OverdueOnly_Filter_Keeps_Breached_Rows_Only()
    {
        var (viewer, _) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Manager, permissions: AppPermissions.HrOperationsView);

        foreach (var key in HrOperationsCatalog.All.Select(HrOperationsCatalog.Key))
        {
            var body = await JsonAsync(await viewer.GetAsync(
                $"/api/hr-operations/queues/{key}?overdueOnly=true&pageSize=200"));
            Assert.All(Rows(body), r => Assert.True(r.GetProperty("slaBreached").GetBoolean()));
        }
    }

    // ═══════════════ ⑥ التصفّح محدود وحتميّ ═══════════════

    [Fact]
    public async Task Page_Size_Is_Capped_And_Paging_Is_Deterministic()
    {
        var (leader, leaderId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.TeamLeader,
            permissions: new[] { AppPermissions.HrOperationsView, AppPermissions.AttendanceReport });

        var typeId = await AttendanceTypeIdAsync(leader, "Late");
        for (var i = 0; i < 3; i++)
        {
            var (_, memberId) = await Phase2TestAuth.CreateUserAsync(
                _factory, Roles.Employee, managerId: leaderId);
            await ReportIncidentAsync(leader, memberId, typeId);
        }

        const string q = "/api/hr-operations/queues/attendance-awaiting-employee";

        // سقف بنيويّ: حجم صفحة ضخم لا يفتح الطابور كلّه دفعةً.
        var capped = await JsonAsync(await leader.GetAsync($"{q}?pageSize=10000"));
        Assert.Equal(HrOperationsPolicy.MaxPageSize, capped.GetProperty("pageSize").GetInt32());

        // صفحتان متتاليتان لا تتقاطعان، ونداءان على البيانات نفسها يعطيان الترتيب نفسه.
        var p1 = await JsonAsync(await leader.GetAsync($"{q}?page=1&pageSize=2"));
        var p2 = await JsonAsync(await leader.GetAsync($"{q}?page=2&pageSize=2"));
        var p1Again = await JsonAsync(await leader.GetAsync($"{q}?page=1&pageSize=2"));

        var first = Rows(p1).Select(r => r.GetProperty("entityId").GetGuid()).ToList();
        var second = Rows(p2).Select(r => r.GetProperty("entityId").GetGuid()).ToList();

        Assert.Equal(first, Rows(p1Again).Select(r => r.GetProperty("entityId").GetGuid()).ToList());
        Assert.Empty(first.Intersect(second));
    }

    // ═══════════════ ⑦ التصدير: نفس الصفوف + أثر تدقيق دائم ═══════════════

    [Fact]
    public async Task Export_Returns_Exactly_The_Rows_Of_The_Drilldown()
    {
        var (viewer, leaderId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.TeamLeader,
            permissions: new[]
            {
                AppPermissions.HrOperationsView,
                AppPermissions.HrOperationsExport,
                AppPermissions.AttendanceReport
            });
        var (_, employeeId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: leaderId);

        var typeId = await AttendanceTypeIdAsync(viewer, "Late");
        var incidentId = await ReportIncidentAsync(viewer, employeeId, typeId);

        var detail = await JsonAsync(await viewer.GetAsync(
            "/api/hr-operations/queues/attendance-awaiting-employee?pageSize=200"));

        var res = await viewer.GetAsync("/api/hr-operations/queues/attendance-awaiting-employee/export");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var bytes = await res.Content.ReadAsByteArrayAsync();
        var csv = Encoding.UTF8.GetString(bytes);

        // BOM كي تفتحه Excel بالعربيّة دون تشويه.
        Assert.True(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);

        var dataLines = csv.TrimEnd('\r', '\n').Split('\n').Skip(1).ToList();
        Assert.Equal(detail.GetProperty("totalCount").GetInt32(), dataLines.Count);
        Assert.Contains(incidentId.ToString(), csv);
    }

    [Fact]
    public async Task Every_Export_Leaves_An_Audit_Trace()
    {
        var (exporter, exporterId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Hr,
            permissions: new[] { AppPermissions.HrOperationsView, AppPermissions.HrOperationsExport });

        var before = await CountExportAuditsAsync(exporterId);
        var res = await exporter.GetAsync("/api/hr-operations/queues/reports-missing/export");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var after = await CountExportAuditsAsync(exporterId);

        Assert.Equal(before + 1, after);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var log = await db.AuditLogs
            .Where(a => a.ActorId == exporterId && a.Action == "HrOperations.Export")
            .OrderByDescending(a => a.CreatedAtUtc).FirstAsync();

        Assert.Contains("reports-missing", log.DataJson);
    }

    /// <summary>من لا يرى الطابور لا يُصدِّره — والتصدير لا يتجاوز النطاق كما لا يتجاوزه العرض.</summary>
    [Fact]
    public async Task Export_Respects_Scope_Exactly_Like_The_View()
    {
        var (leader, leaderId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.TeamLeader,
            permissions: new[]
            {
                AppPermissions.HrOperationsView,
                AppPermissions.HrOperationsExport,
                AppPermissions.AttendanceReport
            });
        var (_, ownId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: leaderId);

        var (stranger, strangerLeaderId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.TeamLeader, permissions: AppPermissions.AttendanceReport);
        var (_, strangerEmployeeId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: strangerLeaderId);

        var typeId = await AttendanceTypeIdAsync(leader, "Late");
        var foreignIncident = await ReportIncidentAsync(stranger, strangerEmployeeId, typeId);
        var ownIncident = await ReportIncidentAsync(leader, ownId, typeId);

        var csv = await (await leader.GetAsync(
            "/api/hr-operations/queues/attendance-awaiting-employee/export")).Content.ReadAsStringAsync();

        Assert.Contains(ownIncident.ToString(), csv);
        Assert.DoesNotContain(foreignIncident.ToString(), csv);
        Assert.DoesNotContain(strangerEmployeeId.ToString(), csv);

        // وخارج النطاق في التصدير أيضًا «غير موجود» لا «ممنوع».
        Assert.Equal(HttpStatusCode.NotFound, (await leader.GetAsync(
            $"/api/hr-operations/queues/reports-missing/export?userId={strangerEmployeeId}")).StatusCode);
    }

    // ═══════════════ ⑧ لا نصّ حسّاس في الصفّ ولا أثر ماليّ ═══════════════

    /// <summary>
    /// السطر عنوانٌ ومسار لا محضر: وصف الواقعة وردّ الموظّف وملاحظات الموارد البشريّة
    /// تبقى في مصدرها حيث تُفرَض رؤية الحقل كاملةً.
    /// </summary>
    [Fact]
    public async Task Rows_Never_Carry_Free_Text_From_The_Incident()
    {
        var (viewer, leaderId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.TeamLeader,
            permissions: new[] { AppPermissions.HrOperationsView, AppPermissions.AttendanceReport });
        var (_, employeeId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: leaderId);

        var typeId = await AttendanceTypeIdAsync(viewer, "Late");
        await ReportIncidentAsync(viewer, employeeId, typeId);

        var raw = await (await viewer.GetAsync(
            "/api/hr-operations/queues/attendance-awaiting-employee?pageSize=200")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("تأخّر صباحيّ موثَّق للاختبار", raw);
        Assert.DoesNotContain("description", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hrNotes", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("employeeResponse", raw, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>لوحة عمليّات ⇒ قراءة بحتة: لا حركة رصيد ولا معاملة ماليّة تنشأ من فتحها.</summary>
    [Fact]
    public async Task Opening_The_Dashboard_Creates_No_Financial_Movement()
    {
        var (viewer, viewerId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.TeamLeader,
            permissions: new[] { AppPermissions.HrOperationsView, AppPermissions.AttendanceReport });
        var (_, employeeId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: viewerId);

        var typeId = await AttendanceTypeIdAsync(viewer, "Late");
        await ReportIncidentAsync(viewer, employeeId, typeId);

        var before = await CountLedgerAsync(employeeId);

        await viewer.GetAsync($"/api/hr-operations/dashboard?recentCycles={Cycles}");
        await viewer.GetAsync("/api/hr-operations/queues/attendance-awaiting-employee?pageSize=200");

        Assert.Equal(before, await CountLedgerAsync(employeeId));
    }

    // ═══════════════ ⑨ العلم ليس تخويلًا لكنّه يُخفي السطح ═══════════════

    [Fact]
    public async Task Attendance_Queues_Are_Present_When_The_Attendance_Flag_Is_On()
    {
        var (viewer, _) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Manager, permissions: AppPermissions.HrOperationsView);

        var dashboard = await JsonAsync(await viewer.GetAsync("/api/hr-operations/dashboard"));

        // بيئة الاختبار تُشغّل علم الحضور ⇒ الطوابير الأربعة موجودة بأعدادها لا مطفأة.
        foreach (var q in HrOperationsCatalog.All.Where(HrOperationsPolicy.IsAttendanceQueue))
            Assert.True(Card(dashboard, HrOperationsCatalog.Key(q)).GetProperty("count").GetInt32() >= 0);
    }

    // ═══════════════ أدوات قياس معزولة ═══════════════

    private async Task<int> CountExportAuditsAsync(Guid actorId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.AuditLogs.CountAsync(a =>
            a.ActorId == actorId && a.Action == "HrOperations.Export");
    }

    private async Task<int> CountLedgerAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.EmployeeBalanceLedger.CountAsync(l => l.EmployeeId == userId);
    }
}
