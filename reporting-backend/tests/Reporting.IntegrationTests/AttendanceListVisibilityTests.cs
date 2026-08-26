using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Attendance;
using Reporting.Application.Common;
using Reporting.Application.Security;
using Reporting.Infrastructure.Persistence;

namespace Reporting.IntegrationTests;

/// <summary>
/// DEF-P123-RC-001 — تكافؤ قاعدة الرؤية بين سطح القائمة وسطح التفاصيل في وقائع الحضور.
///
/// <para>
/// العيب المُعالَج: <c>GET /api/attendance</c> كانت تُدرِج واقعة سابقة للإرسال (<c>Draft</c>/<c>Cancelled</c>)
/// لموظّفها الموضوع، بينما <c>GET /api/attendance/{id}</c> يردّ عليها <c>404</c> بحقّ. أي إنّ سطح القائمة
/// كان يناقض سطح التفاصيل ويُفشي وجود بلاغ لم يصر رسميًّا بعد.
/// </para>
///
/// <para>
/// الثابت الحاكم الذي تحرسه هذه المجموعة: <b>كلّ صفّ تعيده القائمة يجب أن يكون قابلًا للفتح في التفاصيل</b>
/// بالمستخدم نفسه. القائمة ⊆ التفاصيل — بلا استثناء واحد، ومهما كانت الحالة أو الصفة أو النطاق.
/// </para>
/// </summary>
[Collection("Phase2")]
public class AttendanceListVisibilityTests
{
    private readonly Phase2WebApplicationFactory _factory;

    public AttendanceListVisibilityTests(Phase2WebApplicationFactory factory) => _factory = factory;

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage res) =>
        JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.Clone();

    private static async Task<Guid> TypeIdAsync(HttpClient client, string code)
    {
        var types = await JsonAsync(await client.GetAsync("/api/attendance/types"));
        return types.EnumerateArray().First(t => t.GetProperty("code").GetString() == code)
            .GetProperty("id").GetGuid();
    }

    /// <summary>قائد فريق ومرؤوسه المباشر — أصغر بنية تُنتج علاقة إشراف حقيقيّة عبر <c>ManagerId</c>.</summary>
    private async Task<(HttpClient Leader, Guid LeaderId, HttpClient Employee, Guid EmployeeId)>
        SupervisorAndSubordinateAsync()
    {
        var (leader, leaderId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var (employee, employeeId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: leaderId);
        return (leader, leaderId, employee, employeeId);
    }

    /// <summary>ينشئ بلاغًا. <paramref name="submit"/> = false ⇒ يبقى <c>Draft</c> (لم يُرسَل بعد).</summary>
    private static async Task<JsonElement> ReportAsync(
        HttpClient reporter, Guid subjectId, Guid typeId, bool submit, int dayOffset = 1)
    {
        var res = await reporter.PostAsJsonAsync("/api/attendance", new
        {
            subjectUserId = subjectId,
            incidentTypeId = typeId,
            incidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-dayOffset)),
            startTime = new TimeOnly(9, 30),
            returnTime = new TimeOnly(10, 15),
            description = "واقعة موثَّقة بغرض اختبار الرؤية",
            submitImmediately = submit
        });

        Assert.True(res.StatusCode == HttpStatusCode.OK,
            $"فشل إنشاء البلاغ ({(int)res.StatusCode}): {await res.Content.ReadAsStringAsync()}");
        return await JsonAsync(res);
    }

    private static async Task<(List<Guid> Ids, int TotalCount)> ListAsync(HttpClient client, string query = "")
    {
        var res = await client.GetAsync($"/api/attendance{query}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var page = await JsonAsync(res);
        var ids = page.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetGuid()).ToList();
        return (ids, page.GetProperty("totalCount").GetInt32());
    }

    // ═══════════════ الموظّف الموضوع: الحالات السابقة للإرسال محجوبة عنه ═══════════════

    [Fact]
    public async Task Subject_List_DoesNotContain_DraftIncident()
    {
        var (leader, _, employee, employeeId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(leader, "Late");

        var draft = await ReportAsync(leader, employeeId, typeId, submit: false);
        Assert.Equal("Draft", draft.GetProperty("status").GetString());

        var (ids, _) = await ListAsync(employee);
        Assert.DoesNotContain(draft.GetProperty("id").GetGuid(), ids);
    }

    [Fact]
    public async Task Subject_List_TotalCount_DoesNotReveal_DraftIncident()
    {
        var (leader, _, employee, employeeId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(leader, "Late");

        var (_, before) = await ListAsync(employee);
        await ReportAsync(leader, employeeId, typeId, submit: false);
        var (idsAfter, after) = await ListAsync(employee);

        // العدّاد قناة تسريب مستقلّة عن الصفوف: رقمٌ يتحرّك يكشف وجود المسودّة ولو غابت من items.
        Assert.Equal(before, after);
        Assert.Equal(idsAfter.Count, after);
    }

    [Fact]
    public async Task Subject_Detail_Returns404_ForDraftIncident()
    {
        var (leader, _, employee, employeeId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(leader, "Late");
        var draft = await ReportAsync(leader, employeeId, typeId, submit: false);

        var res = await employee.GetAsync($"/api/attendance/{draft.GetProperty("id").GetGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Subject_CanSee_Incident_AfterSubmission()
    {
        var (leader, _, employee, employeeId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(leader, "Late");

        // جدول AttendanceTransitions: Draft --Submit--> Reported، ثمّ يُشعِر النظامُ الموظّفَ تلقائيًّا
        // فتستقرّ الواقعة على AwaitingEmployee. الحدّ الحاكم ليس اسمًا بعينه بل **مغادرة ما قبل الإرسال**.
        var submitted = await ReportAsync(leader, employeeId, typeId, submit: true);
        var id = submitted.GetProperty("id").GetGuid();
        var status = submitted.GetProperty("status").GetString();
        Assert.NotEqual("Draft", status);
        Assert.NotEqual("Cancelled", status);

        var (ids, _) = await ListAsync(employee);
        Assert.Contains(id, ids);

        var detail = await employee.GetAsync($"/api/attendance/{id}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
    }

    [Fact]
    public async Task Subject_CannotSee_CancelledPreSubmissionIncident()
    {
        var (leader, _, employee, employeeId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(leader, "Late");

        var draft = await ReportAsync(leader, employeeId, typeId, submit: false);
        var id = draft.GetProperty("id").GetGuid();
        var stamp = draft.GetProperty("concurrencyStamp").GetInt32();

        var cancelled = await leader.DeleteAsync($"/api/attendance/{id}?concurrencyStamp={stamp}");
        Assert.True(cancelled.IsSuccessStatusCode,
            $"فشل إلغاء المسودّة ({(int)cancelled.StatusCode}): {await cancelled.Content.ReadAsStringAsync()}");

        // Cancelled لا تُبلَغ إلّا من Draft ⇒ مسودّة عدَل عنها صاحبها: تبقى كأن لم تكن عند الموضوع.
        var (ids, _) = await ListAsync(employee);
        Assert.DoesNotContain(id, ids);

        var detail = await employee.GetAsync($"/api/attendance/{id}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
    }

    // ═══════════════ الصفات الأخرى: لا تضييق زائد ولا اكتشاف غير مشروع ═══════════════

    [Fact]
    public async Task Reporter_CanSee_OwnDraftIncident()
    {
        var (leader, _, _, employeeId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(leader, "Late");
        var draft = await ReportAsync(leader, employeeId, typeId, submit: false);
        var id = draft.GetProperty("id").GetGuid();

        var (ids, _) = await ListAsync(leader);
        Assert.Contains(id, ids);

        // المسودّة وُجِدت ليعدّلها مُبلِّغها أو يلغيها ⇒ حجبها عنه يُبطِل معناها.
        var detail = await leader.GetAsync($"/api/attendance/{id}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
    }

    [Fact]
    public async Task AuthorizedReviewer_CanSee_DraftWithinScope()
    {
        var (leader, _, _, employeeId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(leader, "Late");
        var draft = await ReportAsync(leader, employeeId, typeId, submit: false);
        var id = draft.GetProperty("id").GetGuid();

        // مفتاح صريح لا دور: لا Hr ولا Admin يمنح هذا ضمنًا.
        var (reviewer, _) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Hr, permissions: new[] { AppPermissions.AttendanceReview });

        // المراجع يرى كلّ الوقائع في القاعدة المشتركة ⇒ يُقيَّد المرشِّح بالموظّف كي لا تُزيحها الصفحة.
        var (ids, _) = await ListAsync(reviewer, $"?subjectUserId={employeeId}");
        Assert.Contains(id, ids);

        var detail = await reviewer.GetAsync($"/api/attendance/{id}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
    }

    [Fact]
    public async Task UnrelatedActor_CannotDiscoverDraft()
    {
        var (leader, _, _, employeeId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(leader, "Late");
        var draft = await ReportAsync(leader, employeeId, typeId, submit: false);
        var id = draft.GetProperty("id").GetGuid();

        var (stranger, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var (ids, _) = await ListAsync(stranger);
        Assert.DoesNotContain(id, ids);

        // ولا يكتشفها بمرشِّح مُلفَّق يسمّي الموظّف صراحةً.
        var (filtered, _) = await ListAsync(stranger, $"?subjectUserId={employeeId}");
        Assert.DoesNotContain(id, filtered);
    }

    [Fact]
    public async Task OutOfScopeActor_Gets404()
    {
        var (leader, _, _, employeeId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(leader, "Late");
        var draft = await ReportAsync(leader, employeeId, typeId, submit: false);
        var id = draft.GetProperty("id").GetGuid();

        // قائد فريق آخر لا يشرف على الموظّف: خارج النطاق تمامًا.
        var (otherLeader, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);

        var existing = await otherLeader.GetAsync($"/api/attendance/{id}");
        var missing = await otherLeader.GetAsync($"/api/attendance/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, existing.StatusCode);
        Assert.Equal(missing.StatusCode, existing.StatusCode);

        var (ids, _) = await ListAsync(otherLeader);
        Assert.DoesNotContain(id, ids);
    }

    // ═══════════════ قنوات التسريب غير المباشرة: الترقيم والمرشِّح والعدّاد ═══════════════

    [Fact]
    public async Task Pagination_DoesNotLeakHiddenDraft()
    {
        var (leader, _, employee, employeeId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(leader, "Late");

        // ثلاث وقائع مرسَلة (مرئيّة) + مسودّة واحدة (محجوبة) على أيّام مختلفة كي لا يمنعها كشف التكرار.
        for (var day = 2; day <= 4; day++)
            await ReportAsync(leader, employeeId, typeId, submit: true, dayOffset: day);
        var draftId = (await ReportAsync(leader, employeeId, typeId, submit: false, dayOffset: 5))
            .GetProperty("id").GetGuid();

        var seen = new List<Guid>();
        var (_, total) = await ListAsync(employee, "?page=1&pageSize=1");
        for (var page = 1; page <= total + 1; page++)
        {
            var (ids, _) = await ListAsync(employee, $"?page={page}&pageSize=1");
            if (ids.Count == 0) break;
            seen.AddRange(ids);
        }

        // لا تظهر في أيّ صفحة، ولا يتضخّم العدّاد بها فيوسوس بصفحة إضافيّة فارغة.
        Assert.DoesNotContain(draftId, seen);
        Assert.Equal(total, seen.Count);
    }

    [Fact]
    public async Task Search_DoesNotLeakHiddenDraft()
    {
        var (leader, _, employee, employeeId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(leader, "Late");
        var draftId = (await ReportAsync(leader, employeeId, typeId, submit: false)).GetProperty("id").GetGuid();

        // كلّ مرشِّحات القائمة تُطبَّق **فوق** النطاق لا بدلًا منه: لا مرشِّح يُعيد ما حُجِب.
        foreach (var query in new[]
                 {
                     "?status=Draft",
                     $"?subjectUserId={employeeId}",
                     $"?incidentTypeId={typeId}",
                     $"?fromDate={DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)):yyyy-MM-dd}",
                     "?needsMyAction=true"
                 })
        {
            var (ids, _) = await ListAsync(employee, query);
            Assert.DoesNotContain(draftId, ids);
        }
    }

    [Fact]
    public async Task Summary_DoesNotCountHiddenDraft()
    {
        var (leader, _, employee, employeeId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(leader, "Late");
        await ReportAsync(leader, employeeId, typeId, submit: false);

        // لا يوجد سطح ملخّص منفصل للحضور؛ العدّاد الوحيد هو totalCount تحت كلّ مرشِّح ⇒ يُفحص عليه.
        var (statusIds, statusTotal) = await ListAsync(employee, "?status=Draft");
        Assert.Equal(0, statusTotal);
        Assert.Empty(statusIds);

        var (allIds, allTotal) = await ListAsync(employee);
        Assert.Equal(allIds.Count, allTotal);
    }

    // ═══════════════ حارس التكافؤ: القائمة ⊆ التفاصيل، لكلّ حالة × صفة × نطاق ═══════════════

    [Fact]
    public async Task Attendance_List_And_Detail_UseEquivalentVisibilityRules()
    {
        var (leader, _, employee, employeeId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(leader, "Late");

        var draftId = (await ReportAsync(leader, employeeId, typeId, submit: false, dayOffset: 6))
            .GetProperty("id").GetGuid();
        var reportedId = (await ReportAsync(leader, employeeId, typeId, submit: true, dayOffset: 7))
            .GetProperty("id").GetGuid();

        var toCancel = await ReportAsync(leader, employeeId, typeId, submit: false, dayOffset: 8);
        var cancelledId = toCancel.GetProperty("id").GetGuid();
        await leader.DeleteAsync(
            $"/api/attendance/{cancelledId}?concurrencyStamp={toCancel.GetProperty("concurrencyStamp").GetInt32()}");

        var (reviewer, _) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Hr, permissions: new[] { AppPermissions.AttendanceReview });
        var (stranger, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (otherLeader, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);

        var incidents = new[] { draftId, reportedId, cancelledId };
        var actors = new (string Name, HttpClient Client)[]
        {
            ("الموضوع", employee), ("المُبلِّغ", leader), ("المراجع المخوّل", reviewer),
            ("غريب", stranger), ("خارج النطاق", otherLeader)
        };

        foreach (var (name, client) in actors)
        {
            var (listed, _) = await ListAsync(client, $"?subjectUserId={employeeId}");
            foreach (var id in incidents)
            {
                var inList = listed.Contains(id);
                var detail = await client.GetAsync($"/api/attendance/{id}");
                var inDetail = detail.StatusCode == HttpStatusCode.OK;

                // الثابت: ما تُظهره القائمة يجب أن يُفتَح في التفاصيل. العكس مسموح (تضييق) لا يُسرِّب.
                Assert.True(!inList || inDetail,
                    $"انحراف رؤية: القائمة أظهرت {id} للصفة «{name}» بينما التفاصيل ردّت {(int)detail.StatusCode}.");
            }
        }
    }

    /// <summary>
    /// الحجب يقع في SQL لا في الذاكرة: لو قُوِّم شرط ما قبل الإرسال على العميل لسبقه <c>Count</c>
    /// و<c>Skip/Take</c> فتسرّب الواقعة من العدّاد أو من ترقيم الصفحات. هذا الاختبار يقرأ الاستعلام
    /// المولَّد نفسه ويُثبِت أنّ الشرط جزء من نصّ SQL.
    /// </summary>
    [Fact]
    public void VisibleIncidentPredicate_IsTranslatedToSql_NotEvaluatedOnClient()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var sql = db.AttendanceIncidents.AsNoTracking()
            .Where(AttendanceAccess.VisibleIncidentPredicate(
                viewerUserId: Guid.NewGuid(),
                canReviewOrEscalate: false,
                isOperationalSupervisor: true,
                seesAllSubjects: false,
                scopedSubjectUserIds: new[] { Guid.NewGuid() }))
            .ToQueryString();

        Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Status", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SubjectUserId", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ReportedByUserId", sql, StringComparison.OrdinalIgnoreCase);
    }
}
