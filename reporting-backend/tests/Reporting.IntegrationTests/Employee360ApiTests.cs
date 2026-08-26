using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Security;
using Reporting.Domain.Entities.Governance;
using Reporting.Domain.Entities.Leave;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.IntegrationTests;

/// <summary>
/// P2-EMP-002 — سطح Employee 360 على قاعدة المرحلة الثانية المعزولة.
/// الفحص الأمنيّ هنا على **غياب** المفاتيح من الـJSON لا على كونها <c>null</c>.
/// </summary>
[Collection("Phase2")]
public class Employee360ApiTests
{
    private readonly Phase2WebApplicationFactory _factory;

    public Employee360ApiTests(Phase2WebApplicationFactory factory) => _factory = factory;

    private static readonly string[] AllSectionKeys =
    {
        "identity", "operationalSummary", "reports", "kpi", "leaveAndPermissions",
        "requestsAndBalances", "attendanceAndCompliance", "notes", "governance",
        "developmentAndTraining", "timeline"
    };

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage res)
    {
        var text = await res.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static IReadOnlyList<string> SectionKeys(JsonElement root) =>
        root.GetProperty("sections").EnumerateObject().Select(p => p.Name).ToList();

    // ===== الذات =====

    [Fact]
    public async Task Self_Alias_Returns_All_Eleven_Sections()
    {
        var (client, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var res = await client.GetAsync("/api/employees/me/profile-360");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var root = await ReadJsonAsync(res);
        Assert.True(root.GetProperty("isSelf").GetBoolean());

        var keys = SectionKeys(root);
        foreach (var expected in AllSectionKeys)
            Assert.Contains(expected, keys);
        Assert.Equal(AllSectionKeys.Length, keys.Count);
    }

    [Fact]
    public async Task Id_Route_Still_Works_For_Self_Alongside_The_Me_Alias()
    {
        var (client, userId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var res = await client.GetAsync($"/api/employees/{userId}/profile-360");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.True((await ReadJsonAsync(res)).GetProperty("isSelf").GetBoolean());
    }

    [Fact]
    public async Task Every_Section_Carries_Status_And_DataQuality()
    {
        var (client, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var root = await ReadJsonAsync(await client.GetAsync("/api/employees/me/profile-360"));

        foreach (var section in root.GetProperty("sections").EnumerateObject())
        {
            var status = section.Value.GetProperty("status").GetString();
            Assert.Contains(status, new[] { "Ready", "NoData", "Partial", "Error" });
            Assert.False(string.IsNullOrWhiteSpace(section.Value.GetProperty("dataQuality").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(section.Value.GetProperty("titleAr").GetString()));
        }
    }

    [Fact]
    public async Task Attendance_Section_Declares_Itself_Unavailable_Instead_Of_Inventing_Data()
    {
        var (client, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var root = await ReadJsonAsync(await client.GetAsync("/api/employees/me/profile-360"));

        var attendance = root.GetProperty("sections").GetProperty("attendanceAndCompliance");
        Assert.Equal(0, attendance.GetProperty("items").GetArrayLength());
    }

    // ===== خارج النطاق =====

    [Fact]
    public async Task Employee_Requesting_A_Colleague_Gets_404_Not_403()
    {
        var (client, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (_, colleagueId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var res = await client.GetAsync($"/api/employees/{colleagueId}/profile-360");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Unknown_User_And_OutOfScope_User_Are_Indistinguishable()
    {
        var (client, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (_, colleagueId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var missing = await client.GetAsync($"/api/employees/{Guid.NewGuid()}/profile-360");
        var outOfScope = await client.GetAsync($"/api/employees/{colleagueId}/profile-360");

        Assert.Equal(missing.StatusCode, outOfScope.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    // ===== الإشراف =====

    [Fact]
    public async Task TeamLeader_Sees_Direct_Report_But_Not_Outside_His_Team()
    {
        var (leaderClient, leaderId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var (_, memberId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee, managerId: leaderId);
        var (_, outsiderId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var inScope = await leaderClient.GetAsync($"/api/employees/{memberId}/profile-360");
        Assert.Equal(HttpStatusCode.OK, inScope.StatusCode);
        Assert.Equal("DirectTeam", (await ReadJsonAsync(inScope)).GetProperty("viewerRelation").GetString());

        var outside = await leaderClient.GetAsync($"/api/employees/{outsiderId}/profile-360");
        Assert.Equal(HttpStatusCode.NotFound, outside.StatusCode);
    }

    // ===== غياب القسم غير المصرَّح به =====

    [Fact]
    public async Task Executive_Does_Not_Receive_Hr_Sections_At_All()
    {
        var (ceoClient, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Ceo);
        var (_, employeeId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var res = await ceoClient.GetAsync($"/api/employees/{employeeId}/profile-360");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var keys = SectionKeys(await ReadJsonAsync(res));
        Assert.DoesNotContain("leaveAndPermissions", keys);
        Assert.DoesNotContain("requestsAndBalances", keys);
        Assert.DoesNotContain("notes", keys);
        Assert.Contains("kpi", keys);
        Assert.Contains("reports", keys);
    }

    // ===== غياب الحقل غير المصرَّح به =====

    [Fact]
    public async Task Leave_Reason_Key_Is_Absent_Without_The_Explicit_Hr_Permission()
    {
        var (leaderClient, leaderId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.TeamLeader);
        var (_, memberId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee, managerId: leaderId);
        await SeedLeaveAsync(memberId, "سبب حسّاس لا يجوز تسريبه");

        var root = await ReadJsonAsync(await leaderClient.GetAsync($"/api/employees/{memberId}/profile-360"));
        var items = root.GetProperty("sections").GetProperty("leaveAndPermissions").GetProperty("items");

        Assert.True(items.GetArrayLength() > 0);
        foreach (var item in items.EnumerateArray())
            Assert.False(item.TryGetProperty("reason", out _), "مفتاح السبب يجب ألّا يُسلسَل أصلًا.");
    }

    [Fact]
    public async Task Leave_Reason_Appears_Only_With_The_Explicit_Hr_Permission()
    {
        var (hrClient, _) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Hr, null, null, null, AppPermissions.HrSensitiveRead);
        var (_, employeeId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SeedLeaveAsync(employeeId, "سبب مرئيّ لصاحب الإذن");

        var root = await ReadJsonAsync(await hrClient.GetAsync($"/api/employees/{employeeId}/profile-360"));
        var items = root.GetProperty("sections").GetProperty("leaveAndPermissions").GetProperty("items");

        Assert.True(items.GetArrayLength() > 0);
        Assert.Contains(items.EnumerateArray(), i => i.TryGetProperty("reason", out var r)
            && r.GetString() == "سبب مرئيّ لصاحب الإذن");
    }

    [Fact]
    public async Task Legacy_Internal_Note_Is_Not_Serialized_To_The_Employee_Himself()
    {
        var (client, userId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SeedNoteAsync(userId, sensitivity: null, body: "ملاحظة داخليّة تاريخيّة");

        var root = await ReadJsonAsync(await client.GetAsync("/api/employees/me/profile-360"));
        var notes = root.GetProperty("sections").GetProperty("notes").GetProperty("items");

        Assert.DoesNotContain(notes.EnumerateArray(),
            n => n.GetProperty("body").GetString() == "ملاحظة داخليّة تاريخيّة");
    }

    [Fact]
    public async Task Note_Shared_With_Employee_Is_Serialized_To_Him()
    {
        var (client, userId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);
        await SeedNoteAsync(userId, (int)FieldSensitivity.SharedWithEmployee, "ملاحظة مشتركة معك");

        var root = await ReadJsonAsync(await client.GetAsync("/api/employees/me/profile-360"));
        var notes = root.GetProperty("sections").GetProperty("notes").GetProperty("items");

        Assert.Contains(notes.EnumerateArray(),
            n => n.GetProperty("body").GetString() == "ملاحظة مشتركة معك");
    }

    // ===== انتقاء الأقسام =====

    [Fact]
    public async Task Sections_Filter_Narrows_The_Response_Without_Breaking_Authorization()
    {
        var (client, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var root = await ReadJsonAsync(
            await client.GetAsync("/api/employees/me/profile-360?sections=Identity,Kpi"));

        var keys = SectionKeys(root);
        Assert.Equal(2, keys.Count);
        Assert.Contains("identity", keys);
        Assert.Contains("kpi", keys);
    }

    [Fact]
    public async Task Requesting_An_Unauthorized_Section_Explicitly_Still_Does_Not_Return_It()
    {
        var (ceoClient, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Ceo);
        var (_, employeeId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var root = await ReadJsonAsync(
            await ceoClient.GetAsync($"/api/employees/{employeeId}/profile-360?sections=LeaveAndPermissions"));

        Assert.Empty(SectionKeys(root));
    }

    // ===== المسار القائم لم يتغيّر =====

    [Fact]
    public async Task Existing_Dashboard_Employee_Profile_Route_Still_Responds()
    {
        var (client, userId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var res = await client.GetAsync($"/api/dashboard/employee-profile/{userId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ===== بذور =====

    private async Task SeedLeaveAsync(Guid userId, string reason)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.LeaveRequests.Add(new LeaveRequest
        {
            RequesterUserId = userId,
            Type = LeaveRequestType.Leave,
            StartDate = new DateOnly(2026, 8, 2),
            EndDate = new DateOnly(2026, 8, 3),
            Reason = reason,
            Status = LeaveRequestStatus.Submitted,
            CurrentStep = LeaveRequestStep.TeamLeader
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedNoteAsync(Guid userId, int? sensitivity, string body)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ManagementNotes.Add(new ManagementNote
        {
            EntityType = ManagementNoteEntityType.User,
            EntityId = userId,
            AuthorId = userId,
            NoteType = ManagementNoteType.Documentation,
            Body = body,
            Status = ManagementNoteStatus.Open,
            Sensitivity = sensitivity
        });
        await db.SaveChangesAsync();
    }
}
