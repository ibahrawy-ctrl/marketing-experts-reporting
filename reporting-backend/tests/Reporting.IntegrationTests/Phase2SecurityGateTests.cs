using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Api.Controllers;
using Reporting.Application.Common;
using Reporting.Application.Security;
using Reporting.Domain.Entities.Governance;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// P2-SEC-011 — البوّابة الأمنيّة النهائيّة للمرحلة الثانية.
///
/// <para><b>لماذا مجموعة مستقلّة رغم وجود فحوص أمنيّة في كلّ مجموعة سطح؟</b> لأنّ الفحوص
/// الموزَّعة تُثبِت سلامة كلّ سطح على حدة، ولا تُثبِت **اتّساق المصفوفة** بينها: أن يكون
/// «خارج النطاق» جوابًا واحدًا في كلّ الأسطح، وأن يكون 403 محصورًا في موضعه المشروع وحده.
/// هذه المجموعة تقرأ الأسطح الأربعة معًا بمعيار واحد.</para>
///
/// <para><b>قاعدة الاستجابة المعتمَدة (تُقاس هنا لا تُوصَف):</b></para>
/// <list type="bullet">
/// <item><b>404</b> — مورد/موظّف خارج النطاق، أو طلب يكشف وجود ما لا يُرى. لا يُحوَّل إلى 403 أبدًا.</item>
/// <item><b>403</b> — صلاحيّة وظيفيّة عامّة مفقودة **قبل تحديد أيّ مورد** (بوّابة السياسة)،
/// أو رفض على مورد **يراه الفاعل أصلًا** (فصل الواجبات) — فالإخفاء هناك كذب لا حماية.</item>
/// <item><b>409</b> — تعارض حالة أو تزامن على مورد مرئيّ ومصرَّح به. <b>400</b> — خطأ حمولة.</item>
/// </list>
///
/// <para><b>شرط الإغلاق:</b> صفر تسريب، والحقل المحجوب <b>غائب من الـJSON</b> لا <c>null</c>.</para>
/// </summary>
[Collection("Phase2")]
public class Phase2SecurityGateTests(Phase2WebApplicationFactory factory)
{
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage res) =>
        JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.Clone();

    private static bool Has(JsonElement el, string prop) => el.TryGetProperty(prop, out _);

    private static async Task<string> RawAsync(HttpResponseMessage res) =>
        await res.Content.ReadAsStringAsync();

    private static async Task<Guid> TypeIdAsync(HttpClient client, string code)
    {
        var types = await JsonAsync(await client.GetAsync("/api/attendance/types"));
        return types.EnumerateArray().First(t => t.GetProperty("code").GetString() == code)
            .GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> ReportOkAsync(HttpClient reporter, Guid subjectId, Guid typeId)
    {
        var res = await reporter.PostAsJsonAsync("/api/attendance", new
        {
            subjectUserId = subjectId,
            incidentTypeId = typeId,
            incidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            startTime = new TimeOnly(9, 30),
            returnTime = new TimeOnly(10, 15),
            description = "بلاغ توثيقيّ لبوّابة الأمن",
            submitImmediately = true
        });
        Assert.True(res.StatusCode == HttpStatusCode.OK,
            $"فشل إنشاء البلاغ ({(int)res.StatusCode}): {await res.Content.ReadAsStringAsync()}");
        return await JsonAsync(res);
    }

    /// <summary>قائد فريق ومرؤوسه المباشر — أصغر بنية تُنتج علاقة إشراف حقيقيّة.</summary>
    private async Task<(HttpClient Leader, Guid LeaderId, HttpClient Employee, Guid EmployeeId)>
        SupervisorAndSubordinateAsync(params string[] leaderPermissions)
    {
        var (leader, leaderId) = await Phase2TestAuth.CreateUserAsync(
            factory, Roles.TeamLeader, null, null, null, leaderPermissions);
        var (employee, employeeId) = await Phase2TestAuth.CreateUserAsync(
            factory, Roles.Employee, managerId: leaderId);
        return (leader, leaderId, employee, employeeId);
    }

    /// <summary>يزرع ملاحظة إداريّة بتصنيف بعينه على موظّف — مادّة اختبار حجب الحقول.</summary>
    private async Task<string> SeedNoteAsync(Guid subjectId, Guid authorId, FieldSensitivity sensitivity)
    {
        var body = $"نصّ ملاحظة {sensitivity} {Guid.NewGuid():N}";
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ManagementNotes.Add(new ManagementNote
        {
            EntityType = ManagementNoteEntityType.User,
            EntityId = subjectId,
            AuthorId = authorId,
            Body = body,
            RequiresAction = false,
            Status = ManagementNoteStatus.Open,
            Sensitivity = (int)sensitivity,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return body;
    }

    // ══════════════════════ ① خارج النطاق ⇒ 404 مطابق لغير الموجود ══════════════════════

    /// <summary>
    /// الموظّف وملفّ زميله: الادّعاء ليس «يُمنَع» بل «لا يُميَّز عن غير الموجود».
    /// المقارنة بمعرّف عشوائيّ هي ما يجعل الادّعاء قابلًا للتكذيب — 403 كان سيؤكّد وجود الزميل.
    /// </summary>
    [Fact]
    public async Task Employee_Reading_A_Colleagues_Profile_Gets_404_Indistinguishable_From_Nonexistent()
    {
        var (_, _, _, colleagueId) = await SupervisorAndSubordinateAsync();
        var (stranger, _) = await Phase2TestAuth.CreateUserAsync(factory, Roles.Employee);

        var existing = await stranger.GetAsync($"/api/employees/{colleagueId}/profile-360");
        var missing = await stranger.GetAsync($"/api/employees/{Guid.NewGuid()}/profile-360");

        Assert.Equal(HttpStatusCode.NotFound, existing.StatusCode);
        Assert.Equal(missing.StatusCode, existing.StatusCode);
    }

    [Fact]
    public async Task Employee_Reading_A_Colleagues_Checklist_Gets_404_Not_403()
    {
        var (_, _, _, colleagueId) = await SupervisorAndSubordinateAsync();
        var (stranger, _) = await Phase2TestAuth.CreateUserAsync(factory, Roles.Employee);

        var existing = await stranger.GetAsync($"/api/employees/{colleagueId}/checklist");
        var missing = await stranger.GetAsync($"/api/employees/{Guid.NewGuid()}/checklist");

        Assert.Equal(HttpStatusCode.NotFound, existing.StatusCode);
        Assert.Equal(missing.StatusCode, existing.StatusCode);
    }

    /// <summary>
    /// قائد فريق أمام فريق آخر: **القراءة والكتابة كلتاهما 404**. الكتابة أخطر هنا لأنّ
    /// 403 عليها كان سيؤكّد للمُبلِّغ أنّ الموظّف موجود ويحمل حسابًا — وهو تعداد مستخدمين مجّانيّ.
    /// </summary>
    [Fact]
    public async Task TeamLeader_Can_Neither_Read_Nor_Report_An_Incident_Outside_Their_Team()
    {
        var (owner, _, _, subjectId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(owner, "Late");
        var incidentId = (await ReportOkAsync(owner, subjectId, typeId)).GetProperty("id").GetGuid();

        // قائد فريق آخر تمامًا: لا يشرف على الموضوع ولا علاقة له بالواقعة.
        var (outsider, _) = await Phase2TestAuth.CreateUserAsync(factory, Roles.TeamLeader);

        var read = await outsider.GetAsync($"/api/attendance/{incidentId}");
        var readMissing = await outsider.GetAsync($"/api/attendance/{Guid.NewGuid()}");
        var write = await outsider.PostAsJsonAsync("/api/attendance", new
        {
            subjectUserId = subjectId,
            incidentTypeId = typeId,
            incidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            startTime = new TimeOnly(9, 0),
            returnTime = new TimeOnly(9, 45),
            description = "محاولة تسجيل خارج الفريق",
            submitImmediately = true
        });

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(readMissing.StatusCode, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, write.StatusCode);
    }

    /// <summary>
    /// المرفق يرث نطاق واقعته: الغريب يصطدم بـ404 قبل أن يُلمَس الملفّ، وصاحب الواقعة ينزّله —
    /// والشقّ الثاني هو ما يمنع اختبارًا ينجح لأنّ التنزيل معطّل للجميع.
    /// </summary>
    [Fact]
    public async Task Attachment_Outside_Scope_Is_404_While_The_Subject_Still_Downloads_It()
    {
        var (leader, _, employee, employeeId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(leader, "Late");
        var incidentId = (await ReportOkAsync(leader, employeeId, typeId)).GetProperty("id").GetGuid();

        using var form = new MultipartFormDataContent();
        var payload = new ByteArrayContent(Encoding.UTF8.GetBytes("دليل اختباريّ"));
        payload.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(payload, "file", "evidence.txt");
        var upload = await leader.PostAsync($"/api/attendance/{incidentId}/attachments", form);
        Assert.True(upload.StatusCode == HttpStatusCode.OK,
            $"فشل رفع الدليل ({(int)upload.StatusCode}): {await upload.Content.ReadAsStringAsync()}");
        var attachmentId = (await JsonAsync(upload)).GetProperty("id").GetGuid();

        var (stranger, _) = await Phase2TestAuth.CreateUserAsync(factory, Roles.Employee);
        var denied = await stranger.GetAsync($"/api/attendance/{incidentId}/attachments/{attachmentId}");
        var allowed = await employee.GetAsync($"/api/attendance/{incidentId}/attachments/{attachmentId}");

        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    /// <summary>
    /// مسارات <c>me</c> لا تُوجَّه بإدخال العميل: حقن <c>userId</c> في سلسلة الاستعلام
    /// لا يغيّر الموضوع. لولا هذا لصار «الذات» بابًا خلفيًّا إلى ملفّ أيّ أحد.
    /// </summary>
    [Fact]
    public async Task Self_Surfaces_Cannot_Be_Steered_To_Another_Subject_By_Query_Injection()
    {
        var (_, _, employee, employeeId) = await SupervisorAndSubordinateAsync();
        var (_, otherId) = await Phase2TestAuth.CreateUserAsync(factory, Roles.Employee);

        var profile = await JsonAsync(await employee.GetAsync(
            $"/api/employees/me/profile-360?userId={otherId}&subjectUserId={otherId}"));
        var checklist = await JsonAsync(await employee.GetAsync(
            $"/api/employees/me/checklist?userId={otherId}"));

        Assert.Equal(employeeId, profile.GetProperty("subjectUserId").GetGuid());
        Assert.Equal(employeeId, checklist.GetProperty("subjectUserId").GetGuid());
        Assert.True(profile.GetProperty("isSelf").GetBoolean());
    }

    // ══════════════════════ ② 403 المشروع: صلاحيّة عامّة قبل تحديد المورد ══════════════════════

    /// <summary>
    /// لوحة العمليّات مورد **عامّ** لا يخصّ موظّفًا بعينه، فلا وجود يُخفى عند بوّابتها ⇒ 403 صحيح.
    /// ودور <c>HR</c> بلا مفتاح يُرفَض تمامًا كالموظّف: الدور وحده لا يمنح شيئًا.
    /// </summary>
    [Fact]
    public async Task HrOperations_Without_The_View_Key_Is_403_For_Employee_And_For_The_Hr_Role_Alike()
    {
        var (employee, _) = await Phase2TestAuth.CreateUserAsync(factory, Roles.Employee);
        var (hrNoKey, _) = await Phase2TestAuth.CreateUserAsync(factory, Roles.Hr);

        foreach (var client in new[] { employee, hrNoKey })
        {
            Assert.Equal(HttpStatusCode.Forbidden,
                (await client.GetAsync("/api/hr-operations/dashboard")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await client.GetAsync("/api/hr-operations/queues/ReportsMissing")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await client.GetAsync("/api/hr-operations/queues/ReportsMissing/export")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await client.GetAsync("/api/obligations")).StatusCode);
        }
    }

    /// <summary>
    /// التصدير مفتاح مستقلّ لا امتداد للرؤية: من يرى اللوحة لا يُصدِّرها بالضرورة.
    /// ولوحة الالتزامات الشخصيّة تبقى مفتوحة لصاحبها — فالمنع ليس إغلاقًا شاملًا.
    /// </summary>
    [Fact]
    public async Task Export_Key_Is_Independent_From_View_And_Personal_Obligations_Stay_Open()
    {
        var (viewer, _) = await Phase2TestAuth.CreateUserAsync(
            factory, Roles.Hr, null, null, null, AppPermissions.HrOperationsView);

        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/hr-operations/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await viewer.GetAsync("/api/hr-operations/queues/ReportsMissing/export")).StatusCode);

        var (employee, _) = await Phase2TestAuth.CreateUserAsync(factory, Roles.Employee);
        Assert.Equal(HttpStatusCode.OK, (await employee.GetAsync("/api/obligations/me")).StatusCode);
    }

    /// <summary>
    /// تحرير بند يدويّ يحتاج مفتاحًا صريحًا — والقراءة لا تمنحه. الفحص على **قائد الفريق المشرف**
    /// عمدًا: هو داخل النطاق ويرى القائمة، فالجواب 403 لا 404 لأنّ المورد مرئيّ له أصلًا.
    /// </summary>
    [Fact]
    public async Task Checklist_Write_Needs_Its_Own_Key_Even_For_A_Supervisor_Who_Can_Read_It()
    {
        var (leader, _, _, employeeId) = await SupervisorAndSubordinateAsync();

        var read = await leader.GetAsync($"/api/employees/{employeeId}/checklist");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var write = await leader.PutAsJsonAsync(
            $"/api/employees/{employeeId}/checklist/onboarding-orientation",
            new { status = "InProgress", note = "محاولة بلا مفتاح" });

        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    // ══════════════════════ ③ إعادة فحص 403 المسجَّلة في CS5 ══════════════════════

    /// <summary>
    /// <b>الحكم على 403 الأولى (تأكيد المُبلِّغ بلاغَه):</b> ليست خروجًا من النطاق — الفاعل
    /// يقرأ الواقعة بنجاح قبل المحاولة، وهذا ما يُقاس هنا صراحةً — بل رفض على مورد مرئيّ.
    /// وهي **مركَّبة**: مفتاح مراجعة مفقود، وفصل واجبات فوقه.
    ///
    /// <para>وهذا الفحص هو ما كشف عيب CS5: الصيغة السابقة كانت تجرّب مُبلِّغًا بلا مفتاح مراجعة،
    /// فتنجح لغياب المفتاح وتترك فصل الواجبات بلا إثبات. المُبلِّغ الحامل للمفتاح كان يؤكّد
    /// بلاغه فعلًا. أُصلِح في <c>AttendanceActorRules</c> وبقي 403 هو الجواب الصحيح.</para>
    /// </summary>
    [Fact]
    public async Task Self_Confirmation_Is_403_On_A_Resource_The_Actor_Provably_Sees_Even_With_The_Review_Key()
    {
        var (leader, _, _, employeeId) = await SupervisorAndSubordinateAsync(
            AppPermissions.AttendanceReview);
        var typeId = await TypeIdAsync(leader, "Late");
        var created = await ReportOkAsync(leader, employeeId, typeId);
        var incidentId = created.GetProperty("id").GetGuid();

        // (1) المورد مرئيّ للفاعل فعلًا — بهذا يسقط احتمال أنّ 403 يخفي خروجًا من النطاق.
        var read = await leader.GetAsync($"/api/attendance/{incidentId}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        // (2) ومع ذلك لا يؤكّد بلاغه بنفسه رغم حمله مفتاح المراجعة.
        var confirm = await leader.PostAsJsonAsync($"/api/attendance/{incidentId}/hr-review", new
        {
            decision = AttendanceHrDecision.Confirm,
            concurrencyStamp = created.GetProperty("concurrencyStamp").GetInt32()
        });
        Assert.Equal(HttpStatusCode.Forbidden, confirm.StatusCode);

        // (3) مراجِع آخر يصطدم على الحالة نفسها بـ**409 لا 403** — وهذا ما يعزل السبب في
        // الفاعل لا في حالة الواقعة. بلا هذه المقارنة كان 403 يقبل التفسيرين.
        var (otherReviewer, _) = await Phase2TestAuth.CreateUserAsync(
            factory, Roles.Hr, null, null, null, AppPermissions.AttendanceReview);
        var byOther = await otherReviewer.PostAsJsonAsync($"/api/attendance/{incidentId}/hr-review", new
        {
            decision = AttendanceHrDecision.Confirm,
            concurrencyStamp = created.GetProperty("concurrencyStamp").GetInt32()
        });
        Assert.Equal(HttpStatusCode.Conflict, byOther.StatusCode);
    }

    /// <summary>
    /// <b>الحكم على 403 الثانية (البلاغ على النفس):</b> لا مورد سابقًا كي يُخفى، والموضوع هو
    /// الفاعل نفسه فوجوده ليس سرًّا يُحمى منه ⇒ 403 صحيح. ويُقاس أثرها: **لا شيء أُنشئ**.
    /// </summary>
    [Fact]
    public async Task Self_Report_Is_403_And_Provably_Creates_Nothing()
    {
        var (leader, _, employee, employeeId) = await SupervisorAndSubordinateAsync();
        var typeId = await TypeIdAsync(leader, "Late");

        var res = await employee.PostAsJsonAsync("/api/attendance", new
        {
            subjectUserId = employeeId,
            incidentTypeId = typeId,
            incidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            startTime = new TimeOnly(9, 30),
            returnTime = new TimeOnly(10, 15),
            description = "بلاغ على النفس",
            submitImmediately = true
        });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);

        var mine = await JsonAsync(await employee.GetAsync($"/api/attendance?subjectUserId={employeeId}"));
        Assert.Empty(mine.GetProperty("items").EnumerateArray());
    }

    // ══════════════════════ ④ الحسّاسيّة: الحقل المحجوب يغيب من الـJSON ══════════════════════

    /// <summary>
    /// ملاحظة الموارد البشريّة: **غائبة من الـJSON** بلا المفتاح، حاضرة معه على الواقعة نفسها.
    /// الشقّان معًا هما ما يفصل «حُجِبت» عن «لم تُكتَب أصلًا».
    /// </summary>
    [Fact]
    public async Task HrNote_Is_Absent_From_Json_Without_The_Sensitivity_Key_And_Present_With_It()
    {
        // بنيتان متطابقتان تمامًا لا تختلفان إلّا في المفتاح — فالفارق في المخرجات يُعزى إليه وحده.
        var (blind, blindNote) = await IncidentWithHrNoteAsync();
        var (seeing, seeingNote) = await IncidentWithHrNoteAsync(AppPermissions.HrSensitiveRead);

        var withoutKey = await blind.Reader.GetAsync($"/api/attendance/{blind.IncidentId}");
        var withKey = await seeing.Reader.GetAsync($"/api/attendance/{seeing.IncidentId}");
        Assert.Equal(HttpStatusCode.OK, withoutKey.StatusCode);
        Assert.Equal(HttpStatusCode.OK, withKey.StatusCode);

        var blindRaw = await RawAsync(withoutKey);
        var blindJson = JsonDocument.Parse(blindRaw).RootElement;
        Assert.False(Has(blindJson, "hrNote"),
            "المفتاح `hrNote` حاضر في JSON لمن لا يملك تصريح الحسّاسيّة — الحجب يجب أن يكون غيابًا لا null.");
        Assert.DoesNotContain(blindNote, blindRaw);

        var seeingJson = await JsonAsync(withKey);
        Assert.True(Has(seeingJson, "hrNote"), "الحقل غاب عمّن يملك المفتاح — لكان الحجب تعطيلًا لا حراسة.");
        Assert.Equal(seeingNote, seeingJson.GetProperty("hrNote").GetString());
    }

    /// <summary>واقعة عليها ملاحظة موارد بشريّة، ومُشاهِد مشرف عليها بالمفاتيح المطلوبة.</summary>
    private async Task<((HttpClient Reader, Guid IncidentId), string Note)> IncidentWithHrNoteAsync(
        params string[] extraPermissions)
    {
        var permissions = extraPermissions.Append(AppPermissions.AttendanceReview).ToArray();
        var (leader, _, _, employeeId) = await SupervisorAndSubordinateAsync(permissions);
        var typeId = await TypeIdAsync(leader, "Late");
        var incidentId = (await ReportOkAsync(leader, employeeId, typeId)).GetProperty("id").GetGuid();

        var note = $"ملاحظة موارد بشريّة داخليّة {Guid.NewGuid():N}";
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var incident = await db.AttendanceIncidents.FirstAsync(i => i.Id == incidentId);
        incident.HrNote = note;
        await db.SaveChangesAsync();

        return ((leader, incidentId), note);
    }

    /// <summary>
    /// ملاحظة مصنّفة <c>HrOnly</c> على موظّف: لا يراها الموظّف نفسه، ولا مديرُه المباشر بحكم
    /// دوره، ويراها من يملك المفتاح الصريح. الدور لا يمنح الحسّاسيّة أبدًا — هذا ما يُقاس.
    /// </summary>
    [Fact]
    public async Task HrOnly_Note_Is_Hidden_From_The_Subject_And_From_Their_Manager_Until_The_Explicit_Key()
    {
        var (blindLeader, blindLeaderId, subject, subjectId) = await SupervisorAndSubordinateAsync();
        var hidden = await SeedNoteAsync(subjectId, blindLeaderId, FieldSensitivity.HrOnly);

        Assert.DoesNotContain(hidden,
            await RawAsync(await subject.GetAsync("/api/employees/me/profile-360")));
        Assert.DoesNotContain(hidden,
            await RawAsync(await blindLeader.GetAsync($"/api/employees/{subjectId}/profile-360")));

        // بنية مطابقة لا تختلف إلّا بالمفتاح — بها يثبت أنّ الغياب حجبٌ لا تعطيل للقسم كلّه.
        var (keyedLeader, keyedLeaderId, _, otherId) =
            await SupervisorAndSubordinateAsync(AppPermissions.HrSensitiveRead);
        var shown = await SeedNoteAsync(otherId, keyedLeaderId, FieldSensitivity.HrOnly);

        Assert.Contains(shown,
            await RawAsync(await keyedLeader.GetAsync($"/api/employees/{otherId}/profile-360")));
    }

    /// <summary>
    /// الملاحظة <c>Internal</c> تُظهِر أنّ الحجب السابق ليس تعطيلًا عامًّا للملاحظات:
    /// المشرف يراها، وصاحب الملفّ لا يراها — وهذا بالضبط تعريف «داخليّ».
    /// </summary>
    [Fact]
    public async Task Internal_Note_Reaches_The_Supervisor_But_Not_The_Subject()
    {
        var (leader, leaderId, employee, employeeId) = await SupervisorAndSubordinateAsync();
        var body = await SeedNoteAsync(employeeId, leaderId, FieldSensitivity.Internal);

        var supervisorRaw = await RawAsync(await leader.GetAsync($"/api/employees/{employeeId}/profile-360"));
        var selfRaw = await RawAsync(await employee.GetAsync("/api/employees/me/profile-360"));

        Assert.Contains(body, supervisorRaw);
        Assert.DoesNotContain(body, selfRaw);
    }

    /// <summary>
    /// <c>Admin</c> منفردًا: هويّة تشغيليّة فقط. الأقسام غير المصرّح بها **تغيب من الخريطة**
    /// ولا تُرسَل فارغة — إرسالها فارغة كان سيؤكّد وجودها ويُسرِّب بنية الملفّ.
    /// </summary>
    [Fact]
    public async Task Admin_Alone_Receives_Identity_Only_And_The_Other_Sections_Are_Absent()
    {
        var (_, _, _, employeeId) = await SupervisorAndSubordinateAsync();
        await SeedNoteAsync(employeeId, employeeId, FieldSensitivity.Internal);

        var (admin, _) = await Phase2TestAuth.CreateUserAsync(factory, Roles.Admin);
        var res = await admin.GetAsync($"/api/employees/{employeeId}/profile-360");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var sections = (await JsonAsync(res)).GetProperty("sections");
        var keys = sections.EnumerateObject().Select(p => p.Name).ToList();

        Assert.Equal(new[] { "identity" }, keys);
        foreach (var forbidden in new[] { "notes", "kpi", "reports", "attendanceAndCompliance", "timeline" })
            Assert.False(Has(sections, forbidden), $"القسم `{forbidden}` وصل إلى Admin منفردًا.");
    }

    /// <summary>
    /// تعدّد الأدوار = **اتّحاد ما مُنِح** لا فتحٌ شامل: (Admin + TeamLeader) على مرؤوس مباشر
    /// يكسب أقسام الإشراف التي يمنحها الدور الثاني، ولا يكسب حقلًا حسّاسًا لأنّ الحسّاسيّة
    /// لا يمنحها دور إطلاقًا. الشقّان معًا هما الادّعاء؛ أحدهما وحده يقبل التفسيرين.
    /// </summary>
    [Fact]
    public async Task Multi_Role_Viewer_Gains_The_Union_Of_Grants_But_No_Sensitive_Field()
    {
        var (dual, dualId) = await Phase2TestAuth.CreateWithRolesAsync(
            factory, new[] { Roles.Admin, Roles.TeamLeader });
        var (_, subjectId) = await Phase2TestAuth.CreateUserAsync(
            factory, Roles.Employee, managerId: dualId);

        var hrOnly = await SeedNoteAsync(subjectId, dualId, FieldSensitivity.HrOnly);
        var visible = await SeedNoteAsync(subjectId, dualId, FieldSensitivity.Internal);

        var res = await dual.GetAsync($"/api/employees/{subjectId}/profile-360");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var raw = await RawAsync(res);
        var sections = JsonDocument.Parse(raw).RootElement.GetProperty("sections");

        // (أ) الاتّحاد: أقسام الإشراف حاضرة رغم أنّ Admin منفردًا لا يراها.
        Assert.True(Has(sections, "notes"));
        Assert.True(Has(sections, "leaveAndPermissions"));
        Assert.Contains(visible, raw);

        // (ب) وليس فتحًا شاملًا: الحسّاس يبقى محجوبًا.
        Assert.DoesNotContain(hrOnly, raw);
    }

    // ══════════════════════ ⑤ الحارس البنيويّ لمصفوفة التخويل ══════════════════════

    /// <summary>
    /// نقاط النهاية التي **لا** تحمل سياسة على مستوى الإجراء، بقرار موثَّق: التخويل فيها يقع في
    /// طبقة الخدمة كي يبقى «خارج النطاق ⇒ 404» موحّدًا ولا يتحوّل إلى 403 كاشف عند البوّابة.
    ///
    /// <para>القائمة **إعلان لا استثناء**: أيّ نقطة جديدة لا تحمل سياسة ولا تُضاف هنا تُسقِط
    /// الحارس، فيُجبَر كاتبها على إعلان قرار التخويل بدل تركه ضمنيًّا.</para>
    /// </summary>
    private static readonly HashSet<string> ServiceEnforcedEndpoints = new(StringComparer.Ordinal)
    {
        // سطح الحضور كلّه — القرار في AttendanceService/AttendanceAccess/AttendanceActorRules.
        "AttendanceController.Types", "AttendanceController.List", "AttendanceController.Get",
        "AttendanceController.Events", "AttendanceController.ReconciliationSuggestions",
        "AttendanceController.Create", "AttendanceController.UpdateDraft", "AttendanceController.Submit",
        "AttendanceController.CancelDraft", "AttendanceController.Withdraw",
        "AttendanceController.Acknowledge", "AttendanceController.Dispute",
        "AttendanceController.HrReview", "AttendanceController.Escalate", "AttendanceController.Close",
        "AttendanceController.UploadAttachment", "AttendanceController.DownloadAttachment",

        // القراءة في Employee 360 والقائمة — النطاق والحسّاسيّة يقرّرهما FieldVisibilityPolicy.
        "EmployeesController.Profile360", "EmployeesController.MyProfile360",
        "EmployeesController.Checklist", "EmployeesController.MyChecklist",

        // التزامات المستخدم نفسه — لا مورد لغيره كي يُخفى.
        "ObligationsController.Mine",
    };

    private static readonly Type[] Phase2Controllers =
    {
        typeof(AttendanceController), typeof(EmployeesController),
        typeof(HrOperationsController), typeof(ObligationsController)
    };

    private static IEnumerable<MethodInfo> ActionsOf(Type controller) =>
        controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any());

    /// <summary>
    /// لا نقطة نهاية بلا قرار تخويل **مُعلَن**: إمّا سياسة على الإجراء، وإمّا إدراج صريح في
    /// قائمة «التخويل في الخدمة». الفحص انعكاسيّ لا نصّيّ كي لا يفلت إجراء يُضاف لاحقًا.
    /// </summary>
    [Fact]
    public void Every_Phase2_Endpoint_Declares_An_Explicit_Authorization_Decision()
    {
        var undeclared = new List<string>();
        var anonymous = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var controller in Phase2Controllers)
        {
            Assert.True(controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any(),
                $"{controller.Name} بلا [Authorize] على مستوى الصنف.");
            Assert.False(controller.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any(),
                $"{controller.Name} يحمل [AllowAnonymous] على مستوى الصنف.");

            foreach (var action in ActionsOf(controller))
            {
                var name = $"{controller.Name}.{action.Name}";
                seen.Add(name);

                if (action.GetCustomAttributes<AllowAnonymousAttribute>().Any())
                {
                    anonymous.Add(name);
                    continue;
                }

                var hasPolicy = action.GetCustomAttributes<AuthorizeAttribute>()
                    .Any(a => !string.IsNullOrWhiteSpace(a.Policy));

                if (!hasPolicy && !ServiceEnforcedEndpoints.Contains(name))
                    undeclared.Add(name);
            }
        }

        Assert.Empty(anonymous);
        Assert.True(undeclared.Count == 0,
            "نقاط نهاية بلا قرار تخويل مُعلَن: " + string.Join(", ", undeclared));

        // ولا مدخلات ميّتة: مدخل يشير إلى إجراء محذوف يُخفي فقدان الحراسة عن إجراء آخر بالاسم نفسه.
        var stale = ServiceEnforcedEndpoints.Except(seen).ToList();
        Assert.True(stale.Count == 0, "مدخلات بائتة في قائمة «التخويل في الخدمة»: " + string.Join(", ", stale));
    }

    /// <summary>
    /// عدد نقاط النهاية مرصود عمدًا: أيّ زيادة تُجبِر على مراجعة هذه المصفوفة قبل الإغلاق،
    /// فلا يتسلّل سطح جديد بلا فحص أمنيّ لأنّ الحارس أعلاه يقبل بإدراجه في القائمة.
    /// </summary>
    [Fact]
    public void Phase2_Endpoint_Surface_Is_The_One_That_Was_Reviewed()
    {
        var counts = Phase2Controllers.ToDictionary(c => c.Name, c => ActionsOf(c).Count());

        Assert.Equal(17, counts[nameof(AttendanceController)]);
        Assert.Equal(5, counts[nameof(EmployeesController)]);
        Assert.Equal(3, counts[nameof(HrOperationsController)]);
        Assert.Equal(2, counts[nameof(ObligationsController)]);
    }
}
