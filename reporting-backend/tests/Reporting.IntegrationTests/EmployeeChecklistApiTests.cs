using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Checklist;
using Reporting.Application.Common;
using Reporting.Application.Security;
using Reporting.Domain.Entities.Development;
using Reporting.Domain.Entities.Governance;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// P2-HR-010 — قائمة خدمة الموظّف والالتزام.
///
/// <para>العقد الذي تحرسه هذه الاختبارات:</para>
/// <list type="number">
/// <item><b>لا ازدواج بيانات</b> — البند المحسوب يتحرّك بتحرّك مصدره **بلا أن يُخلَق له صفّ**؛
/// وجدول <c>employee_checklist_items</c> لا يحمل يومًا مفتاحًا محسوبًا.</item>
/// <item><b>القراءة ليست تحريرًا</b> — من يرى بندًا لا يُغلِقه بلا مفتاح <c>EmployeeChecklist.Manage</c>.</item>
/// <item><b>الحقل المحجوب غائب لا مُقنَّع</b> — لا يظهر مفتاحه في JSON إطلاقًا.</item>
/// <item><b>404 لا 403 عند مغادرة النطاق</b> — ولا يُستدلّ على وجود موظّف من رمز الاستجابة.</item>
/// </list>
/// </summary>
[Collection("Phase2")]
public class EmployeeChecklistApiTests
{
    private readonly Phase2WebApplicationFactory _factory;

    public EmployeeChecklistApiTests(Phase2WebApplicationFactory factory) => _factory = factory;

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage res) =>
        JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.Clone();

    private static IEnumerable<JsonElement> Items(JsonElement checklist) =>
        checklist.GetProperty("items").EnumerateArray();

    private static IReadOnlyList<string> Keys(JsonElement checklist) =>
        Items(checklist).Select(i => i.GetProperty("key").GetString()!).ToList();

    private static JsonElement Item(JsonElement checklist, string key) =>
        Items(checklist).First(i => i.GetProperty("key").GetString() == key);

    private async Task<T> DbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task<int> ChecklistRowCountAsync(Guid subjectId) =>
        DbAsync(db => db.EmployeeChecklistRecords.CountAsync(r => r.SubjectUserId == subjectId));

    // ═══════════════ ① لا ازدواج بيانات: المحسوب يتحرّك بلا صفّ ═══════════════

    /// <summary>
    /// الحارس المركزيّ للتذكرة: خطّة تحسين تُنشَأ في مصدرها ⇒ البند يتغيّر فورًا،
    /// و<c>employee_checklist_items</c> يبقى **فارغًا تمامًا** لهذا الموظّف.
    /// لو نُسِخ البند لصار للحقيقة نسختان، وسقط قرار على البائتة منهما.
    /// </summary>
    [Fact]
    public async Task Computed_Item_Tracks_Its_Source_Without_Ever_Creating_A_Row()
    {
        var (_, subjectId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (hr, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Hr);

        var before = await JsonAsync(await hr.GetAsync($"/api/employees/{subjectId}/checklist"));
        Assert.Equal(0, Item(before, ChecklistCatalog.ImprovementPlansOpen).GetProperty("openCount").GetInt32());
        Assert.Equal(0, await ChecklistRowCountAsync(subjectId));

        await DbAsync<object?>(async db =>
        {
            db.ImprovementPlans.Add(new ImprovementPlan
            {
                SubjectUserId = subjectId,
                OwnerId = subjectId,
                Title = "خطّة تحسين للاختبار",
                Status = ImprovementPlanStatus.Open,
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            return null;
        });

        var after = await JsonAsync(await hr.GetAsync($"/api/employees/{subjectId}/checklist"));
        var item = Item(after, ChecklistCatalog.ImprovementPlansOpen);

        Assert.Equal(1, item.GetProperty("openCount").GetInt32());
        Assert.Equal("Computed", item.GetProperty("source").GetString());

        // الإثبات الحاسم: لا صفّ وُلِد رغم تغيّر البند.
        Assert.Equal(0, await ChecklistRowCountAsync(subjectId));
    }

    /// <summary>الجدول لا يقبل مفتاحًا محسوبًا حتّى بطلب صريح من حاملٍ لكلّ المفاتيح.</summary>
    [Fact]
    public async Task Computed_Item_Is_Rejected_As_Bad_Request_And_Leaves_The_Table_Empty()
    {
        var (_, subjectId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (hr, _) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Hr,
            permissions: new[] { AppPermissions.EmployeeChecklistManage, AppPermissions.HrSensitiveRead });

        var res = await hr.PutAsJsonAsync(
            $"/api/employees/{subjectId}/checklist/{ChecklistCatalog.ReportsObligations}",
            new { status = EmployeeChecklistStatus.Completed.ToString() });

        // 400 لا 403 ولا 404: المورد ظاهر والمفتاح موجود، لكنّ الطلب نفسه غير سائغ.
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal(0, await ChecklistRowCountAsync(subjectId));
    }

    /// <summary>حارس بنيويّ دائم: لا صفّ في الجدول يحمل مفتاحًا خارج قائمة اليدويّ.</summary>
    [Fact]
    public async Task Table_Never_Holds_A_Non_Manual_Key()
    {
        var manual = ChecklistCatalog.Manual.Select(d => d.Key).ToHashSet(StringComparer.Ordinal);
        var stored = await DbAsync(db =>
            db.EmployeeChecklistRecords.AsNoTracking().Select(r => r.ItemKey).Distinct().ToListAsync());

        Assert.All(stored, key => Assert.Contains(key, manual));
    }

    // ═══════════════ ② اليدويّ: يُخزَّن مرّة واحدة ويُحدَّث في مكانه ═══════════════

    [Fact]
    public async Task Manual_Item_Is_Stored_Once_And_Updated_In_Place()
    {
        var (_, subjectId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (hr, hrId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Hr, permissions: AppPermissions.EmployeeChecklistManage);

        var url = $"/api/employees/{subjectId}/checklist/{ChecklistCatalog.OnboardingOrientation}";

        var first = await hr.PutAsJsonAsync(url, new
        {
            status = EmployeeChecklistStatus.InProgress.ToString(),
            ownerUserId = hrId,
            evidenceReference = "محضر التهيئة رقم ١"
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await hr.PutAsJsonAsync(url, new
        {
            status = EmployeeChecklistStatus.Completed.ToString(),
            ownerUserId = hrId,
            evidenceReference = "محضر التهيئة رقم ٢"
        });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        Assert.Equal(1, await ChecklistRowCountAsync(subjectId));

        var stored = await DbAsync(db => db.EmployeeChecklistRecords.AsNoTracking()
            .FirstAsync(r => r.SubjectUserId == subjectId));
        Assert.Equal(EmployeeChecklistStatus.Completed, stored.Status);
        Assert.Equal("محضر التهيئة رقم ٢", stored.EvidenceReference);
        Assert.Equal(hrId, stored.LastActionByUserId);
    }

    /// <summary>كلّ تحرير يدويّ له أثر تدقيقيّ — البند الذي أُغلِق بلا أثر بلا مالك.</summary>
    [Fact]
    public async Task Manual_Update_Is_Audited()
    {
        var (_, subjectId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (hr, hrId) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Hr, permissions: AppPermissions.EmployeeChecklistManage);

        await hr.PutAsJsonAsync(
            $"/api/employees/{subjectId}/checklist/{ChecklistCatalog.EquipmentHandover}",
            new { status = EmployeeChecklistStatus.Completed.ToString() });

        var log = await DbAsync(db => db.AuditLogs.AsNoTracking()
            .Where(a => a.ActorId == hrId && a.Action == "EmployeeChecklist.Update")
            .OrderByDescending(a => a.CreatedAtUtc).FirstAsync());

        Assert.Contains(ChecklistCatalog.EquipmentHandover, log.DataJson);
        Assert.Contains(subjectId.ToString(), log.DataJson);
    }

    /// <summary>بصمة تزامن بائتة على مورد مرئيّ ومصرَّح به ⟵ 409 لا 400 ولا كتابة صامتة.</summary>
    [Fact]
    public async Task Stale_Concurrency_Stamp_Is_A_Conflict()
    {
        var (_, subjectId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (hr, _) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Hr, permissions: AppPermissions.EmployeeChecklistManage);

        var url = $"/api/employees/{subjectId}/checklist/{ChecklistCatalog.PolicyAcknowledgement}";
        await hr.PutAsJsonAsync(url, new { status = EmployeeChecklistStatus.InProgress.ToString() });

        var res = await hr.PutAsJsonAsync(url, new
        {
            status = EmployeeChecklistStatus.Completed.ToString(),
            concurrencyStamp = "بصمة-قديمة-لا-تطابق"
        });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);

        var stored = await DbAsync(db => db.EmployeeChecklistRecords.AsNoTracking()
            .FirstAsync(r => r.SubjectUserId == subjectId));
        Assert.Equal(EmployeeChecklistStatus.InProgress, stored.Status); // لم تقع كتابة
    }

    // ═══════════════ ③ الحسّاسيّة: المحجوب غائب لا مُقنَّع ═══════════════

    /// <summary>
    /// موظّف على نفسه: يرى المشترَك معه، ولا يرى <c>HrOnly</c> ولا <c>Internal</c>.
    /// الغياب هنا هو العقد — لا مفتاح ولا <c>null</c>.
    /// </summary>
    [Fact]
    public async Task Self_View_Omits_HrOnly_And_Internal_Keys_Entirely()
    {
        var (employee, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var mine = await JsonAsync(await employee.GetAsync("/api/employees/me/checklist"));
        var keys = Keys(mine);
        var raw = mine.GetRawText();

        Assert.True(mine.GetProperty("isSelf").GetBoolean());

        Assert.Contains(ChecklistCatalog.PolicyAcknowledgement, keys);
        Assert.Contains(ChecklistCatalog.ReportsObligations, keys);

        foreach (var hidden in new[]
                 {
                     ChecklistCatalog.EmploymentContractSigned,  // HrOnly
                     ChecklistCatalog.OffboardingClearance,      // HrOnly
                     ChecklistCatalog.NotesRequiringAction,      // Internal
                     ChecklistCatalog.ImprovementPlansOpen       // Internal
                 })
        {
            Assert.DoesNotContain(hidden, keys);
            Assert.DoesNotContain(hidden, raw); // غائب من الحمولة نصًّا، لا مُقنَّعًا
        }
    }

    /// <summary>الدور وحده لا يفتح <c>HrOnly</c>: HR بلا المفتاح النوعيّ لا ترى بندَي الملفّ الحسّاس.</summary>
    [Fact]
    public async Task Hr_Role_Alone_Does_Not_Unlock_HrOnly_Items()
    {
        var (_, subjectId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (plainHr, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Hr);
        var (keyedHr, _) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Hr, permissions: AppPermissions.HrSensitiveRead);

        var without = Keys(await JsonAsync(await plainHr.GetAsync($"/api/employees/{subjectId}/checklist")));
        var with = Keys(await JsonAsync(await keyedHr.GetAsync($"/api/employees/{subjectId}/checklist")));

        Assert.DoesNotContain(ChecklistCatalog.EmploymentContractSigned, without);
        Assert.Contains(ChecklistCatalog.EmploymentContractSigned, with);

        // الفرق مقصور على البنود الحسّاسة وحدها — لا توسيع جانبيّ للرؤية.
        Assert.Equal(
            new[] { ChecklistCatalog.EmploymentContractSigned, ChecklistCatalog.OffboardingClearance }
                .OrderBy(k => k, StringComparer.Ordinal),
            with.Except(without, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal));
    }

    /// <summary>Admin لا يكتسب الحسّاس ضمنًا — الدور الأعلى ليس مفتاحًا.</summary>
    [Fact]
    public async Task Admin_Does_Not_Implicitly_Receive_HrOnly_Items()
    {
        var (_, subjectId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (admin, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Admin);

        var res = await admin.GetAsync($"/api/employees/{subjectId}/checklist");
        if (res.StatusCode == HttpStatusCode.NotFound) return; // خارج نطاق الرؤية أصلًا — أضيق لا أوسع

        var raw = (await JsonAsync(res)).GetRawText();
        Assert.DoesNotContain(ChecklistCatalog.EmploymentContractSigned, raw);
        Assert.DoesNotContain(ChecklistCatalog.OffboardingClearance, raw);
    }

    /// <summary>
    /// عدّاد الملاحظات يُحسَب **بعد** ترشيح الحسّاسيّة: ملاحظة محجوبة لا تُرفَع رقمًا،
    /// وإلّا سرّب الرقمُ وجود ما لا يُقرأ.
    /// </summary>
    [Fact]
    public async Task Note_Count_Is_Computed_After_Sensitivity_Filtering()
    {
        var (_, subjectId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (hr, hrId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Hr);

        await DbAsync<object?>(async db =>
        {
            db.ManagementNotes.Add(new ManagementNote
            {
                EntityType = ManagementNoteEntityType.User,
                EntityId = subjectId,
                AuthorId = hrId,
                Body = "ملاحظة سرّيّة للإدارة العليا وحدها.",
                RequiresAction = true,
                Status = ManagementNoteStatus.Open,
                Sensitivity = (int)FieldSensitivity.ManagementConfidential,
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            return null;
        });

        var view = await JsonAsync(await hr.GetAsync($"/api/employees/{subjectId}/checklist"));
        var notes = Item(view, ChecklistCatalog.NotesRequiringAction);

        // HR ترى البند (Internal) لكنّها لا تحمل مفتاح ManagementConfidential ⇒ الملاحظة خارج العدّ.
        Assert.Equal(0, notes.GetProperty("openCount").GetInt32());
    }

    // ═══════════════ ④ النطاق والتخويل: 404 لمغادرة النطاق، 403 لغياب المفتاح العامّ ═══════════════

    /// <summary>زميل خارج النطاق ⟵ 404: رمز الاستجابة لا يثبت وجود الموظّف.</summary>
    [Fact]
    public async Task Employee_Reaching_A_Colleague_Gets_Not_Found_Not_Forbidden()
    {
        var (employee, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (_, colleagueId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);

        Assert.Equal(HttpStatusCode.NotFound,
            (await employee.GetAsync($"/api/employees/{colleagueId}/checklist")).StatusCode);
    }

    /// <summary>موظّف غير موجود ومَوظّف خارج النطاق ⟵ الرمز نفسه بالضبط.</summary>
    [Fact]
    public async Task Nonexistent_And_Out_Of_Scope_Are_Indistinguishable()
    {
        var (employee, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (_, colleagueId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var ghost = await employee.GetAsync($"/api/employees/{Guid.NewGuid()}/checklist");
        var real = await employee.GetAsync($"/api/employees/{colleagueId}/checklist");

        Assert.Equal(HttpStatusCode.NotFound, ghost.StatusCode);
        Assert.Equal(real.StatusCode, ghost.StatusCode);
    }

    /// <summary>
    /// رؤية البند لا تعني إغلاقه: الموظّف يرى «إقرار السياسات» على نفسه ولا يملك تحريره.
    /// غياب المفتاح العامّ قبل تحديد أيّ مورد ⟵ 403 عند البوّابة (اتّساقًا مع سياسة النظام القائمة).
    /// </summary>
    [Fact]
    public async Task Seeing_An_Item_Does_Not_Grant_Closing_It()
    {
        var (employee, employeeId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var keys = Keys(await JsonAsync(await employee.GetAsync("/api/employees/me/checklist")));
        Assert.Contains(ChecklistCatalog.PolicyAcknowledgement, keys);

        var res = await employee.PutAsJsonAsync(
            $"/api/employees/{employeeId}/checklist/{ChecklistCatalog.PolicyAcknowledgement}",
            new { status = EmployeeChecklistStatus.Completed.ToString() });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal(0, await ChecklistRowCountAsync(employeeId));
    }

    /// <summary>لا دور يمنح مفتاح التحرير ضمنًا — ولا Admin.</summary>
    [Fact]
    public async Task No_Role_Grants_The_Manage_Permission_Implicitly()
    {
        var (_, subjectId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);

        foreach (var role in new[] { Roles.Hr, Roles.Manager, Roles.TeamLeader, Roles.Admin })
        {
            var (client, _) = await Phase2TestAuth.CreateUserAsync(_factory, role);
            var res = await client.PutAsJsonAsync(
                $"/api/employees/{subjectId}/checklist/{ChecklistCatalog.EquipmentHandover}",
                new { status = EmployeeChecklistStatus.Completed.ToString() });

            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        }

        Assert.Equal(0, await ChecklistRowCountAsync(subjectId));
    }

    /// <summary>
    /// المفتاح بيد المُحرِّر لكنّ الموضوع خارج نطاقه ⟵ 404 لا 403.
    /// المفتاح صلاحيّة وظيفيّة، والنطاق سؤال منفصل لا يُجاب عنه بكشف الوجود.
    /// </summary>
    [Fact]
    public async Task Manage_Permission_Outside_Scope_Is_Not_Found()
    {
        var (_, subjectId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (leader, _) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.TeamLeader, permissions: AppPermissions.EmployeeChecklistManage);

        var res = await leader.PutAsJsonAsync(
            $"/api/employees/{subjectId}/checklist/{ChecklistCatalog.EquipmentHandover}",
            new { status = EmployeeChecklistStatus.Completed.ToString() });

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal(0, await ChecklistRowCountAsync(subjectId));
    }

    /// <summary>
    /// بند حسّاس لا يراه المُحرِّر ⟵ 404 لا 403: لو رددنا 403 لأثبتنا وجود البند لمن لا يراه.
    /// </summary>
    [Fact]
    public async Task Editing_An_Invisible_Sensitive_Item_Is_Not_Found()
    {
        var (_, subjectId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (hr, _) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Hr, permissions: AppPermissions.EmployeeChecklistManage);

        var res = await hr.PutAsJsonAsync(
            $"/api/employees/{subjectId}/checklist/{ChecklistCatalog.EmploymentContractSigned}",
            new { status = EmployeeChecklistStatus.Completed.ToString() });

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal(0, await ChecklistRowCountAsync(subjectId));
    }

    /// <summary>مفتاح مجهول ⟵ 404 كالمحجوب تمامًا: لا تُستخرَج خريطة الكتالوج بالتجريب.</summary>
    [Fact]
    public async Task Unknown_Item_Key_Is_Not_Found()
    {
        var (_, subjectId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var (hr, _) = await Phase2TestAuth.CreateUserAsync(
            _factory, Roles.Hr, permissions: AppPermissions.EmployeeChecklistManage);

        var res = await hr.PutAsJsonAsync(
            $"/api/employees/{subjectId}/checklist/no-such-item",
            new { status = EmployeeChecklistStatus.Completed.ToString() });

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Anonymous_Access_Is_Unauthorized()
    {
        var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/employees/me/checklist")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/employees/{Guid.NewGuid()}/checklist")).StatusCode);
    }

    // ═══════════════ ⑤ الملخّص متّسق مع البنود المرئيّة وحدها ═══════════════

    [Fact]
    public async Task Summary_Matches_The_Visible_Items_Exactly()
    {
        var (employee, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var mine = await JsonAsync(await employee.GetAsync("/api/employees/me/checklist"));

        var items = Items(mine).ToList();
        var summary = mine.GetProperty("summary");

        var notApplicable = items.Count(i =>
            i.GetProperty("status").GetString() == nameof(EmployeeChecklistStatus.NotApplicable));
        var completed = items.Count(i =>
            i.GetProperty("status").GetString() == nameof(EmployeeChecklistStatus.Completed));

        Assert.Equal(items.Count - notApplicable, summary.GetProperty("applicable").GetInt32());
        Assert.Equal(completed, summary.GetProperty("completed").GetInt32());
        Assert.Equal(notApplicable, summary.GetProperty("notApplicable").GetInt32());
    }

    /// <summary>«غير منطبق» لا يُقدَّم أبدًا بوصفه إنجازًا في أيّ بند محسوب.</summary>
    [Fact]
    public async Task Not_Applicable_Is_Never_Presented_As_Completed()
    {
        var (employee, _) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var mine = await JsonAsync(await employee.GetAsync("/api/employees/me/checklist"));

        foreach (var item in Items(mine))
        {
            if (item.GetProperty("status").GetString() != nameof(EmployeeChecklistStatus.NotApplicable))
                continue;

            Assert.Equal(0, item.GetProperty("openCount").GetInt32());
            Assert.Equal("غير منطبق", item.GetProperty("statusLabelAr").GetString());
        }
    }
}
