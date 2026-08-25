using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Security;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.IntegrationTests;

/// <summary>
/// P2-ATT-006 — سطح وقائع الحضور على قاعدة المرحلة الثانية المعزولة.
///
/// <para>ما تُثبِته هذه المجموعة تحديدًا:</para>
/// <list type="number">
/// <item>خارج النطاق يُعطي <b>404</b> مطابقًا لغير الموجود — لا 403 ولا قائمة فارغة كاشفة.</item>
/// <item>لا أحد يؤكّد بلاغه بنفسه، ولا يقفز أحد فوق حقّ الموظّف في الردّ.</item>
/// <item>الحقل غير المصرّح به <b>يغيب من الـJSON</b> ولا يُرسَل <c>null</c>.</item>
/// <item>لا انتقال هنا يُنشئ خصمًا أو حركة رصيد — تُفحص جداول الأرصدة قبل/بعد.</item>
/// </list>
/// </summary>
[Collection("Phase2")]
public class AttendanceApiTests
{
    private readonly Phase2WebApplicationFactory _factory;

    public AttendanceApiTests(Phase2WebApplicationFactory factory) => _factory = factory;

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage res) =>
        JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.Clone();

    private static bool Has(JsonElement el, string prop) => el.TryGetProperty(prop, out _);

    /// <summary>يجلب معرّف نوع من الكتالوج المبذور — لا يخترع نوعًا ولا يكتب في الكتالوج.</summary>
    private static async Task<Guid> TypeIdAsync(HttpClient client, string code)
    {
        var types = await JsonAsync(await client.GetAsync("/api/attendance/types"));
        var match = types.EnumerateArray().First(t => t.GetProperty("code").GetString() == code);
        return match.GetProperty("id").GetGuid();
    }

    /// <summary>
    /// قائد فريق ومرؤوسه المباشر — أصغر بنية تُنتج علاقة <c>DirectTeam</c> حقيقيّة.
    /// الإسناد بـ<c>ManagerId</c> وحده لأنّ <c>ScopeResolver</c> يبني النطاق من شجرة الإدارة.
    /// </summary>
    private async Task<(HttpClient Leader, Guid LeaderId, HttpClient Employee, Guid EmployeeId)>
        SupervisorAndSubordinateAsync()
    {
        var (leader, leaderId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var (employee, employeeId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: leaderId);
        return (leader, leaderId, employee, employeeId);
    }

    private static async Task<HttpResponseMessage> ReportAsync(
        HttpClient reporter, Guid subjectId, Guid typeId, bool submit = true, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/attendance")
        {
            Content = JsonContent.Create(new
            {
                subjectUserId = subjectId,
                incidentTypeId = typeId,
                incidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                // نوع "Late" يستلزم الوقتين بحكم الكتالوج — تُرسَل دائمًا كي لا يتغيّر الفشل المتوقَّع.
                startTime = new TimeOnly(9, 30),
                returnTime = new TimeOnly(10, 15),
                description = "تأخّر موثَّق بغرض الاختبار",
                submitImmediately = submit
            })
        };
        if (idempotencyKey is not null) request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await reporter.SendAsync(request);
    }

    /// <summary>يُبلِّغ ويؤكّد النجاح — فشل الإنشاء يظهر بنصّه بدل انهيار غامض عند قراءة مفتاح مفقود.</summary>
    private static async Task<JsonElement> ReportOkAsync(HttpClient reporter, Guid subjectId, Guid typeId)
    {
        var res = await ReportAsync(reporter, subjectId, typeId);
        Assert.True(res.StatusCode == HttpStatusCode.OK,
            $"فشل إنشاء البلاغ ({(int)res.StatusCode}): {await res.Content.ReadAsStringAsync()}");
        return await JsonAsync(res);
    }

    // ═══════════════════════════ النطاق والتسريب ═══════════════════════════

    [Fact]
    public async Task Incident_Outside_Scope_Returns_404_Identical_To_Nonexistent()
    {
        var (leader, _, _, employeeId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(leader, "Late");
        var created = await ReportOkAsync(leader, employeeId, typeId);
        var incidentId = created.GetProperty("id").GetGuid();

        // غريب تمامًا: لا يشرف ولا أُبلِغ عنه ولا يملك مفتاح مراجعة.
        var (stranger, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var existing = await stranger.GetAsync($"/api/attendance/{incidentId}");
        var missing = await stranger.GetAsync($"/api/attendance/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, existing.StatusCode);
        Assert.Equal(missing.StatusCode, existing.StatusCode);
    }

    [Fact]
    public async Task List_Never_Leaks_Incidents_From_Outside_The_Viewer_Scope()
    {
        var (leader, _, _, employeeId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(leader, "Late");
        var created = await ReportOkAsync(leader, employeeId, typeId);
        var incidentId = created.GetProperty("id").GetGuid();

        var (stranger, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);

        // مرشِّح مُلفَّق يطلب موظّفًا خارج النطاق صراحةً — يجب ألّا يوسّعه.
        var page = await JsonAsync(await stranger.GetAsync($"/api/attendance?subjectUserId={employeeId}"));
        var ids = page.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetGuid()).ToList();

        Assert.DoesNotContain(incidentId, ids);
    }

    // ═══════════════════════════ فصل الواجبات ═══════════════════════════

    [Fact]
    public async Task Reporter_Cannot_Confirm_Their_Own_Report()
    {
        var (leader, _, _, employeeId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(leader, "Late");
        var created = await ReportOkAsync(leader, employeeId, typeId);

        var res = await leader.PostAsJsonAsync(
            $"/api/attendance/{created.GetProperty("id").GetGuid()}/hr-review",
            new { decision = AttendanceHrDecision.Confirm, concurrencyStamp = created.GetProperty("concurrencyStamp").GetInt32() });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Employee_Cannot_Report_An_Incident_On_Themselves()
    {
        var (leader, _, employee, employeeId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(leader, "Late");

        var res = await ReportAsync(employee, employeeId, typeId);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Hr_Review_Requires_The_Explicit_Key_And_Is_Not_Granted_By_The_Hr_Role()
    {
        var (leader, _, _, employeeId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(leader, "Late");
        var created = await ReportOkAsync(leader, employeeId, typeId);
        var incidentId = created.GetProperty("id").GetGuid();

        // دور Hr بلا مفتاح Attendance.Review: لا يراجع، ولا يرى الواقعة أصلًا.
        var (hrNoKey, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Hr);

        var read = await hrNoKey.GetAsync($"/api/attendance/{incidentId}");
        var write = await hrNoKey.PostAsJsonAsync($"/api/attendance/{incidentId}/hr-review",
            new { decision = AttendanceHrDecision.Confirm, concurrencyStamp = 1 });

        // 404 لا 403: الدور وحده لا يمنح رؤية ولا مراجعة، وإخفاء الوجود هو الجواب الصحيح
        // لمن هو خارج دائرة الواقعة — 403 كان سيؤكّد له أنّها موجودة.
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, write.StatusCode);
    }

    // ═══════════════════════════ حقّ الموظّف ═══════════════════════════

    [Fact]
    public async Task Confirmation_Is_Impossible_Before_The_Employee_Gets_Their_Say()
    {
        var (leader, _, _, employeeId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(leader, "Late");
        var created = await ReportOkAsync(leader, employeeId, typeId);
        var incidentId = created.GetProperty("id").GetGuid();

        var (hr, _) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Hr, permissions: new[] { AppPermissions.AttendanceReview });

        var detail = await JsonAsync(await hr.GetAsync($"/api/attendance/{incidentId}"));
        var res = await hr.PostAsJsonAsync($"/api/attendance/{incidentId}/hr-review", new
        {
            decision = AttendanceHrDecision.Confirm,
            concurrencyStamp = detail.GetProperty("concurrencyStamp").GetInt32()
        });

        // الحالة لم تبلغ AwaitingHr بعد ⇒ الانتقال نفسه غير مشروع، بصرف النظر عن الصلاحيّة.
        // 409 لأنّ الطلب يناقض حالة المورد القائمة؛ والختم كان سليمًا فلا يُفسَّر الرفض بالتزامن.
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("بانتظار ردّ الموظّف", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Employee_Dispute_Then_Hr_Confirm_Walks_The_Full_Legal_Path()
    {
        var (leader, _, employee, employeeId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(leader, "Late");
        var created = await ReportOkAsync(leader, employeeId, typeId);
        var incidentId = created.GetProperty("id").GetGuid();

        var seen = await JsonAsync(await employee.GetAsync($"/api/attendance/{incidentId}"));
        Assert.Equal("AwaitingEmployee", seen.GetProperty("status").GetString());

        var disputed = await employee.PostAsJsonAsync($"/api/attendance/{incidentId}/dispute", new
        {
            response = "كنت في مهمّة خارجيّة موثّقة.",
            concurrencyStamp = seen.GetProperty("concurrencyStamp").GetInt32()
        });
        Assert.Equal(HttpStatusCode.OK, disputed.StatusCode);

        var (hr, _) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Hr, permissions: new[] { AppPermissions.AttendanceReview });

        var forHr = await JsonAsync(await hr.GetAsync($"/api/attendance/{incidentId}"));
        Assert.Equal("AwaitingHr", forHr.GetProperty("status").GetString());

        var confirmed = await hr.PostAsJsonAsync($"/api/attendance/{incidentId}/hr-review", new
        {
            decision = AttendanceHrDecision.Confirm,
            note = "روجعت الأدلّة.",
            concurrencyStamp = forHr.GetProperty("concurrencyStamp").GetInt32()
        });
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);

        var final = await JsonAsync(confirmed);
        Assert.Equal("Confirmed", final.GetProperty("status").GetString());
        Assert.True(final.GetProperty("isOfficialIncident").GetBoolean());
    }

    [Fact]
    public async Task Correction_Returns_To_The_Employee_Instead_Of_Confirming_Silently()
    {
        var (leader, _, employee, employeeId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(leader, "Late");
        var created = await ReportOkAsync(leader, employeeId, typeId);
        var incidentId = created.GetProperty("id").GetGuid();

        var seen = await JsonAsync(await employee.GetAsync($"/api/attendance/{incidentId}"));
        await employee.PostAsJsonAsync($"/api/attendance/{incidentId}/acknowledge",
            new { response = "أقرّ.", concurrencyStamp = seen.GetProperty("concurrencyStamp").GetInt32() });

        var (hr, _) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Hr, permissions: new[] { AppPermissions.AttendanceReview });

        var forHr = await JsonAsync(await hr.GetAsync($"/api/attendance/{incidentId}"));
        var corrected = await hr.PostAsJsonAsync($"/api/attendance/{incidentId}/hr-review", new
        {
            decision = AttendanceHrDecision.Correct,
            correctedDescription = "الوصف بعد التصحيح.",
            concurrencyStamp = forHr.GetProperty("concurrencyStamp").GetInt32()
        });
        Assert.Equal(HttpStatusCode.OK, corrected.StatusCode);

        // التصحيح جوهريّ ⇒ يعود إلى الموظّف، ولا يصبح واقعة رسميّة بمجرّد التصحيح.
        var afterCorrection = await JsonAsync(await hr.GetAsync($"/api/attendance/{incidentId}"));
        Assert.Equal("AwaitingEmployee", afterCorrection.GetProperty("status").GetString());
        Assert.False(afterCorrection.GetProperty("isOfficialIncident").GetBoolean());
    }

    // ═══════════════════════════ الترشيح الحقليّ ═══════════════════════════

    [Fact]
    public async Task HrNote_Is_Absent_From_Json_For_Viewers_Without_The_Sensitivity_Key()
    {
        var (leader, _, employee, employeeId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(leader, "Late");
        var created = await ReportOkAsync(leader, employeeId, typeId);
        var incidentId = created.GetProperty("id").GetGuid();

        var seen = await JsonAsync(await employee.GetAsync($"/api/attendance/{incidentId}"));
        await employee.PostAsJsonAsync($"/api/attendance/{incidentId}/acknowledge",
            new { response = "أقرّ.", concurrencyStamp = seen.GetProperty("concurrencyStamp").GetInt32() });

        var (hr, _) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Hr,
            permissions: new[] { AppPermissions.AttendanceReview, AppPermissions.HrSensitiveRead });

        var forHr = await JsonAsync(await hr.GetAsync($"/api/attendance/{incidentId}"));
        await hr.PostAsJsonAsync($"/api/attendance/{incidentId}/hr-review", new
        {
            decision = AttendanceHrDecision.Confirm,
            note = "ملاحظة داخليّة لا تخرج من الموارد البشريّة.",
            concurrencyStamp = forHr.GetProperty("concurrencyStamp").GetInt32()
        });

        var withKey = await JsonAsync(await hr.GetAsync($"/api/attendance/{incidentId}"));
        var employeeView = await JsonAsync(await employee.GetAsync($"/api/attendance/{incidentId}"));
        var leaderView = await JsonAsync(await leader.GetAsync($"/api/attendance/{incidentId}"));

        Assert.True(Has(withKey, "hrNote"));
        // الغياب هو الحماية — لا قيمة فارغة ولا null.
        Assert.False(Has(employeeView, "hrNote"));
        Assert.False(Has(leaderView, "hrNote"));
    }

    [Fact]
    public async Task AllowedActions_Reflect_The_Viewer_And_Not_A_Fixed_List()
    {
        var (leader, _, employee, employeeId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(leader, "Late");
        var created = await ReportOkAsync(leader, employeeId, typeId);
        var incidentId = created.GetProperty("id").GetGuid();

        var employeeActions = (await JsonAsync(await employee.GetAsync($"/api/attendance/{incidentId}")))
            .GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()).ToList();
        var leaderActions = (await JsonAsync(await leader.GetAsync($"/api/attendance/{incidentId}")))
            .GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()).ToList();

        Assert.Contains("Acknowledge", employeeActions);
        Assert.Contains("Dispute", employeeActions);
        Assert.DoesNotContain("HrConfirm", employeeActions);

        Assert.DoesNotContain("Acknowledge", leaderActions);
        Assert.DoesNotContain("HrConfirm", leaderActions);
    }

    // ═══════════════════════════ التزامن والتكرار ═══════════════════════════

    [Fact]
    public async Task Stale_ConcurrencyStamp_Returns_409_And_Changes_Nothing()
    {
        var (leader, _, employee, employeeId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(leader, "Late");
        var created = await ReportOkAsync(leader, employeeId, typeId);
        var incidentId = created.GetProperty("id").GetGuid();

        var seen = await JsonAsync(await employee.GetAsync($"/api/attendance/{incidentId}"));
        var stamp = seen.GetProperty("concurrencyStamp").GetInt32();

        var first = await employee.PostAsJsonAsync($"/api/attendance/{incidentId}/acknowledge",
            new { response = "أقرّ.", concurrencyStamp = stamp });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var replay = await employee.PostAsJsonAsync($"/api/attendance/{incidentId}/acknowledge",
            new { response = "إقرار مكرَّر.", concurrencyStamp = stamp });
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
    }

    [Fact]
    public async Task Same_IdempotencyKey_Does_Not_Create_A_Second_Incident()
    {
        var (leader, _, _, employeeId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(leader, "Late");
        var key = Guid.NewGuid().ToString("N");

        var first = await JsonAsync(await ReportAsync(leader, employeeId, typeId, idempotencyKey: key));
        var second = await JsonAsync(await ReportAsync(leader, employeeId, typeId, idempotencyKey: key));

        Assert.Equal(first.GetProperty("id").GetGuid(), second.GetProperty("id").GetGuid());
    }

    // ═══════════════════════════ الخطّ الزمنيّ والعزل الماليّ ═══════════════════════════

    [Fact]
    public async Task Timeline_Records_Every_Transition_Including_The_System_Ones()
    {
        var (leader, leaderId, employee, employeeId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(leader, "Late");
        var created = await ReportOkAsync(leader, employeeId, typeId);
        var incidentId = created.GetProperty("id").GetGuid();

        var seen = await JsonAsync(await employee.GetAsync($"/api/attendance/{incidentId}"));
        await employee.PostAsJsonAsync($"/api/attendance/{incidentId}/acknowledge",
            new { response = "أقرّ.", concurrencyStamp = seen.GetProperty("concurrencyStamp").GetInt32() });

        var events = await JsonAsync(await leader.GetAsync($"/api/attendance/{incidentId}/events"));
        var actions = events.EnumerateArray().Select(e => e.GetProperty("action").GetString()).ToList();

        Assert.Contains("Submit", actions);
        // الإخطار والإحالة انتقالا نظام — يجب أن يظهرا رغم أنّ فاعلهما ليس مستخدمًا.
        Assert.Contains("NotifyEmployee", actions);
        Assert.Contains("Acknowledge", actions);
        Assert.Contains("SendToHr", actions);

        var systemEvent = events.EnumerateArray()
            .First(e => e.GetProperty("action").GetString() == "NotifyEmployee");
        Assert.Equal(Guid.Empty, systemEvent.GetProperty("actorUserId").GetGuid());
        Assert.NotEqual(leaderId, systemEvent.GetProperty("actorUserId").GetGuid());
    }

    [Fact]
    public async Task Confirming_An_Incident_Creates_No_Balance_Movement_Whatsoever()
    {
        var (leader, _, employee, employeeId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(leader, "Late");

        var before = await CountBalanceRowsAsync();

        var created = await ReportOkAsync(leader, employeeId, typeId);
        var incidentId = created.GetProperty("id").GetGuid();

        var seen = await JsonAsync(await employee.GetAsync($"/api/attendance/{incidentId}"));
        await employee.PostAsJsonAsync($"/api/attendance/{incidentId}/acknowledge",
            new { response = "أقرّ.", concurrencyStamp = seen.GetProperty("concurrencyStamp").GetInt32() });

        var (hr, _) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Hr, permissions: new[] { AppPermissions.AttendanceReview });
        var forHr = await JsonAsync(await hr.GetAsync($"/api/attendance/{incidentId}"));
        await hr.PostAsJsonAsync($"/api/attendance/{incidentId}/hr-review", new
        {
            decision = AttendanceHrDecision.Confirm,
            concurrencyStamp = forHr.GetProperty("concurrencyStamp").GetInt32()
        });

        Assert.Equal(before, await CountBalanceRowsAsync());
    }

    /// <summary>دفتر الأرصدة كلّه — أيّ زيادة تعني أنّ الحضور مسّ الرواتب، وهو محظور مطلقًا.</summary>
    private async Task<int> CountBalanceRowsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.EmployeeBalanceLedger.CountAsync();
    }
}
