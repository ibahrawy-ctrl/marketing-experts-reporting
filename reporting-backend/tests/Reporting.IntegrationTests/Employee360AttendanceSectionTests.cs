using System.Net.Http.Json;
using System.Text.Json;
using Reporting.Application.Common;

namespace Reporting.IntegrationTests;

/// <summary>
/// P2-ATT-007 — ربط قسم «الحضور والالتزام» داخل Employee 360.
/// القسم إسقاط قراءة فوق جدول الوقائع نفسه؛ لا نسخة ولا جدول موازٍ،
/// ولا يكشف للموظّف بلاغًا لم يصله بعد.
/// </summary>
[Collection("Phase2")]
public class Employee360AttendanceSectionTests
{
    private readonly Phase2WebApplicationFactory _factory;

    public Employee360AttendanceSectionTests(Phase2WebApplicationFactory factory) => _factory = factory;

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage res) =>
        JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.Clone();

    private static JsonElement Attendance(JsonElement root) =>
        root.GetProperty("sections").GetProperty("attendanceAndCompliance");

    private static async Task<Guid> TypeIdAsync(HttpClient client, string code)
    {
        var types = await JsonAsync(await client.GetAsync("/api/attendance/types"));
        return types.EnumerateArray().First(t => t.GetProperty("code").GetString() == code)
            .GetProperty("id").GetGuid();
    }

    private static async Task<HttpResponseMessage> ReportAsync(
        HttpClient reporter, Guid subjectId, Guid typeId, bool submit) =>
        await reporter.PostAsJsonAsync("/api/attendance", new
        {
            subjectUserId = subjectId,
            incidentTypeId = typeId,
            incidentDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            startTime = "09:30:00",
            returnTime = "10:15:00",
            description = "تأخّر صباحيّ موثَّق للاختبار.",
            submitImmediately = submit
        });

    [Fact]
    public async Task Section_Projects_The_Real_Incident_Table_Not_A_Copy()
    {
        var (leader, leaderId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var (_, employeeId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: leaderId);

        var typeId = await TypeIdAsync(leader, "Late");
        var created = await ReportAsync(leader, employeeId, typeId, submit: true);
        var incidentId = (await JsonAsync(created)).GetProperty("id").GetGuid();

        var section = Attendance(await JsonAsync(
            await leader.GetAsync($"/api/employees/{employeeId}/profile-360?sections=attendanceAndCompliance")));

        Assert.Equal("Ready", section.GetProperty("status").GetString());
        Assert.Equal("Complete", section.GetProperty("dataQuality").GetString());

        var ids = section.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(incidentId, ids);
    }

    [Fact]
    public async Task A_Report_Is_Not_A_Confirmed_Incident_In_The_Profile()
    {
        var (leader, leaderId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var (_, employeeId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: leaderId);

        var typeId = await TypeIdAsync(leader, "Late");
        await ReportAsync(leader, employeeId, typeId, submit: true);

        var section = Attendance(await JsonAsync(
            await leader.GetAsync($"/api/employees/{employeeId}/profile-360?sections=attendanceAndCompliance")));

        // البلاغ حاضر في القائمة، ولا يُحتسب مؤكَّدًا، ولا يُنتج دقائق مؤكَّدة.
        Assert.All(section.GetProperty("items").EnumerateArray(),
            i => Assert.False(i.GetProperty("isConfirmed").GetBoolean()));

        var summary = section.GetProperty("summary");
        Assert.Equal(0, summary.GetProperty("confirmedCount").GetInt32());
        Assert.Equal(0, summary.GetProperty("totalConfirmedMinutes").GetInt32());
        Assert.False(summary.GetProperty("hasPayrollImpact").GetBoolean());
    }

    [Fact]
    public async Task Employee_Does_Not_See_A_Draft_Report_That_Never_Reached_Them()
    {
        var (leader, leaderId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var (employee, employeeId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: leaderId);

        var typeId = await TypeIdAsync(leader, "Late");
        // مسودّة لم تُرسَل: لم يُشعَر بها الموظّف، فلا مكان لها في ملفّه.
        await ReportAsync(leader, employeeId, typeId, submit: false);

        var selfSection = Attendance(await JsonAsync(
            await employee.GetAsync("/api/employees/me/profile-360?sections=attendanceAndCompliance")));

        Assert.Equal("NoData", selfSection.GetProperty("status").GetString());
        Assert.Empty(selfSection.GetProperty("items").EnumerateArray());

        // والمُبلِّغ نفسه يراها — فالغياب ترشيحُ رؤية لا حذفُ صفّ.
        var leaderSection = Attendance(await JsonAsync(
            await leader.GetAsync($"/api/employees/{employeeId}/profile-360?sections=attendanceAndCompliance")));
        Assert.Equal("Ready", leaderSection.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Timeline_Does_Not_Reveal_What_The_Section_Hides()
    {
        var (leader, leaderId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var (employee, employeeId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Employee, managerId: leaderId);

        var typeId = await TypeIdAsync(leader, "Late");
        await ReportAsync(leader, employeeId, typeId, submit: false);

        var timeline = (await JsonAsync(
                await employee.GetAsync("/api/employees/me/profile-360?sections=timeline")))
            .GetProperty("sections").GetProperty("timeline");

        var kinds = timeline.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("kind").GetString()).ToList();
        Assert.DoesNotContain("AttendanceIncident", kinds);
    }
}
