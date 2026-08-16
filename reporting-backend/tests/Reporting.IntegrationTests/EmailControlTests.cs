using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Auth;
using Reporting.Application.Common;
using Reporting.Application.Notifications;
using Reporting.Infrastructure.Identity;
using Reporting.Infrastructure.Persistence;
using Reporting.Infrastructure.Services;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// مركز التحكم بالبريد (EMAIL-CONTROL-CENTER-R1) — كامل المتحكّم للأدمن حصرًا (سياسة EmailControlManage).
/// يتحقّق من: حصر الأدوار (Admin ⇒ 200؛ باقي الأدوار ⇒ 403؛ Anonymous ⇒ 401)،
/// إدارة القوالب/القواعد وأكواد التحقّق، معاينة المستقبِلين وأسباب الأهلية/الاستبعاد،
/// التذكير اليدويّ DryRun (يُنشئ صفوف DryRun عبر القلب الآمن)،
/// بذر 10 قوالب + 7 قواعد، وعدم أي أثر جانبي (لا كتابة على email_outbox، لا إرسال SMTP).
/// R1: DryRun فقط.
/// </summary>
[Collection("Integration")]
public class EmailControlTests
{
    private readonly CustomWebApplicationFactory _factory;

    public EmailControlTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ===== أدوات مساعدة =====

    private async Task<HttpClient> RoleClientAsync(string role)
    {
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test.local";
        const string password = "Passw0rd#1";
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var u = new ApplicationUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                FullName = $"مستخدم {role}", IsActive = true
            };
            await users.CreateAsync(u, password);
            await users.AddToRoleAsync(u, role);
        }
        var c = _factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return c;
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var c = _factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@marketingexperts.local", "Admin#12345"));
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return c;
    }

    /// <summary>ينشئ مستخدمًا نشطًا له بريد عبر Identity. يُرجِع المعرّف.</summary>
    private async Task<Guid> CreateActiveUserAsync(string? role = null)
    {
        var email = $"ecu-{Guid.NewGuid():N}@test.local";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var u = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            FullName = $"مستخدم نشط {Guid.NewGuid():N}", IsActive = true
        };
        await users.CreateAsync(u, "Passw0rd#1");
        if (role is not null) await users.AddToRoleAsync(u, role);
        return u.Id;
    }

    /// <summary>ينشئ مستخدمًا غير نشط له بريد. يُرجِع المعرّف.</summary>
    private async Task<Guid> CreateInactiveUserAsync()
    {
        var email = $"ecu-inactive-{Guid.NewGuid():N}@test.local";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var u = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            FullName = $"مستخدم موقوف {Guid.NewGuid():N}", IsActive = false
        };
        await users.CreateAsync(u, "Passw0rd#1");
        return u.Id;
    }

    /// <summary>ينشئ مستخدمًا نشطًا بلا بريد عبر قاعدة البيانات مباشرة. يُرجِع المعرّف.</summary>
    private async Task<Guid> CreateNoEmailUserAsync()
    {
        var id = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userName = $"noemail-{id:N}";
        db.Users.Add(new ApplicationUser
        {
            Id = id,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = null,
            NormalizedEmail = null,
            EmailConfirmed = false,
            SecurityStamp = Guid.NewGuid().ToString(),
            FullName = "مستخدم بلا بريد",
            IsActive = true
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task<int> OutboxCountAsync(CustomWebApplicationFactory f)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.EmailOutbox.AsNoTracking().CountAsync();
    }

    private static async Task<string?> ProblemTypeAsync(HttpResponseMessage res)
    {
        var json = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("type", out var t) ? t.GetString() : null;
    }

    private async Task<Guid> AnyRuleIdAsync(HttpClient admin)
    {
        var rules = await (await admin.GetAsync("/api/email-control/rules"))
            .Content.ReadFromJsonAsync<List<EmailRuleDto>>(TestJson.Options);
        return rules!.First().Id;
    }

    // ===== 1. حصر الأدوار =====

    [Fact]
    public async Task Anonymous_401()
    {
        var res = await _factory.CreateClient().GetAsync("/api/email-control/templates");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Employee)]
    [InlineData(Roles.TeamLeader)]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Ceo)]
    [InlineData(Roles.GeneralManager)]
    [InlineData(Roles.CeoSupport)]
    public async Task Templates_NonAdmin_403(string role)
    {
        var client = await RoleClientAsync(role);
        var res = await client.GetAsync("/api/email-control/templates");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Rules_NonAdmin_403()
    {
        var client = await RoleClientAsync(Roles.Ceo);
        var res = await client.GetAsync("/api/email-control/rules");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task ManualReminder_NonAdmin_403()
    {
        var client = await RoleClientAsync(Roles.GeneralManager);
        var res = await client.PostAsJsonAsync("/api/email-control/manual-reminders/dry-run",
            new ManualReminderDryRunRequest(RecipientScopeType.Users, "عنوان", "متن", UserIds: new List<Guid> { Guid.NewGuid() }));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Templates_Admin_200()
    {
        var admin = await AdminClientAsync();
        var res = await admin.GetAsync("/api/email-control/templates");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ===== 2. القوالب =====

    [Fact]
    public async Task Templates_List_ContainsSeededTen()
    {
        var admin = await AdminClientAsync();
        var templates = await (await admin.GetAsync("/api/email-control/templates"))
            .Content.ReadFromJsonAsync<List<EmailTemplateDto>>(TestJson.Options);

        Assert.NotNull(templates);
        Assert.True(templates!.Count >= 10);
        var keys = templates.Select(t => t.Key).ToHashSet();
        foreach (var expected in new[]
        {
            "AUTH_EMAIL_CONFIRMATION", "AUTH_RESEND_CONFIRMATION", "REPORT_REMINDER", "REPORT_OVERDUE",
            "REPORT_REVIEW_READY", "GOVERNANCE_ESCALATION", "GOVERNANCE_ACTION_ITEM", "HR_REQUEST_CREATED",
            "HR_REQUEST_DECISION", "MANUAL_REMINDER"
        })
            Assert.Contains(expected, keys);
        // كلها DryRun في R1
        Assert.All(templates, t => Assert.Equal("DryRun", t.DefaultMode));
    }

    [Fact]
    public async Task Template_Get_Known_200()
    {
        var admin = await AdminClientAsync();
        var dto = await (await admin.GetAsync("/api/email-control/templates/MANUAL_REMINDER"))
            .Content.ReadFromJsonAsync<EmailTemplateDto>(TestJson.Options);
        Assert.NotNull(dto);
        Assert.Equal("MANUAL_REMINDER", dto!.Key);
    }

    [Fact]
    public async Task Template_Get_Unknown_404()
    {
        var admin = await AdminClientAsync();
        var res = await admin.GetAsync($"/api/email-control/templates/NOPE_{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Template_Update_EnabledMode_Rejected()
    {
        var admin = await AdminClientAsync();
        var res = await admin.PutAsJsonAsync("/api/email-control/templates/MANUAL_REMINDER",
            new UpdateEmailTemplateRequest("اسم", "عنوان", "متن", true, "Enabled"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("email_control.mode_invalid", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task Template_Update_MissingName_Rejected()
    {
        var admin = await AdminClientAsync();
        var res = await admin.PutAsJsonAsync("/api/email-control/templates/MANUAL_REMINDER",
            new UpdateEmailTemplateRequest("  ", "عنوان", "متن", true, "DryRun"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("email_control.name_required", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task Template_Update_MissingSubject_Rejected()
    {
        var admin = await AdminClientAsync();
        var res = await admin.PutAsJsonAsync("/api/email-control/templates/MANUAL_REMINDER",
            new UpdateEmailTemplateRequest("اسم", "  ", "متن", true, "DryRun"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("email_control.subject_required", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task Template_Update_MissingBody_Rejected()
    {
        var admin = await AdminClientAsync();
        var res = await admin.PutAsJsonAsync("/api/email-control/templates/MANUAL_REMINDER",
            new UpdateEmailTemplateRequest("اسم", "عنوان", "  ", true, "DryRun"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("email_control.body_required", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task Template_Update_UnknownKey_NotFound()
    {
        var admin = await AdminClientAsync();
        var res = await admin.PutAsJsonAsync($"/api/email-control/templates/NOPE_{Guid.NewGuid():N}",
            new UpdateEmailTemplateRequest("اسم", "عنوان", "متن", true, "DryRun"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("email_control.template_not_found", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task Template_Update_Valid_Persists_ThenRestore()
    {
        var admin = await AdminClientAsync();
        const string key = "HR_REQUEST_DECISION";
        var before = await (await admin.GetAsync($"/api/email-control/templates/{key}"))
            .Content.ReadFromJsonAsync<EmailTemplateDto>(TestJson.Options);

        var newName = $"اسم معدّل {Guid.NewGuid():N}";
        var updated = await (await admin.PutAsJsonAsync($"/api/email-control/templates/{key}",
                new UpdateEmailTemplateRequest(newName, before!.SubjectTemplate, before.BodyTemplate, false, "Disabled")))
            .Content.ReadFromJsonAsync<EmailTemplateDto>(TestJson.Options);

        Assert.Equal(newName, updated!.NameAr);
        Assert.False(updated.IsEnabled);
        Assert.Equal("Disabled", updated.DefaultMode);
        Assert.NotNull(updated.UpdatedAtUtc);

        // استعادة الحالة الأصلية (القاعدة مشتركة دائمة)
        await admin.PutAsJsonAsync($"/api/email-control/templates/{key}",
            new UpdateEmailTemplateRequest(before.NameAr, before.SubjectTemplate, before.BodyTemplate, before.IsEnabled, before.DefaultMode));
    }

    // ===== 3. معاينة القالب =====

    [Fact]
    public async Task Template_Preview_ReplacesVariables()
    {
        var admin = await AdminClientAsync();
        var preview = await (await admin.PostAsJsonAsync("/api/email-control/templates/MANUAL_REMINDER/preview",
                new EmailTemplatePreviewRequest(Variables: new Dictionary<string, string>
                {
                    ["Subject"] = "عنوان مخصّص",
                    ["Body"] = "متن مخصّص للمعاينة",
                    ["UserName"] = "سارة"
                })))
            .Content.ReadFromJsonAsync<EmailTemplatePreviewDto>(TestJson.Options);

        Assert.NotNull(preview);
        Assert.Equal("عنوان مخصّص", preview!.Subject);
        Assert.Contains("متن مخصّص للمعاينة", preview.BodyText);
        Assert.Contains("سارة", preview.BodyText);
        Assert.Contains("متن مخصّص للمعاينة", preview.BodyHtml);
    }

    [Fact]
    public async Task Template_Preview_UnknownKey_NotFound()
    {
        var admin = await AdminClientAsync();
        var res = await admin.PostAsJsonAsync($"/api/email-control/templates/NOPE_{Guid.NewGuid():N}/preview",
            new EmailTemplatePreviewRequest());
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("email_control.template_not_found", await ProblemTypeAsync(res));
    }

    // ===== 4. القواعد =====

    [Fact]
    public async Task Rules_List_ContainsSeededSeven()
    {
        var admin = await AdminClientAsync();
        var rules = await (await admin.GetAsync("/api/email-control/rules"))
            .Content.ReadFromJsonAsync<List<EmailRuleDto>>(TestJson.Options);

        Assert.NotNull(rules);
        Assert.True(rules!.Count >= 7);
        var events = rules.Select(r => r.EventType).ToHashSet();
        foreach (var expected in new[]
        {
            "report.reminder", "report.overdue", "report.review_ready", "governance.escalation",
            "governance.action_item", "hr_request.created", "hr_request.decision"
        })
            Assert.Contains(expected, events);
    }

    [Fact]
    public async Task Rule_Get_Unknown_404()
    {
        var admin = await AdminClientAsync();
        var res = await admin.GetAsync($"/api/email-control/rules/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Rule_Update_EnabledMode_Rejected()
    {
        var admin = await AdminClientAsync();
        var id = await AnyRuleIdAsync(admin);
        var res = await admin.PutAsJsonAsync($"/api/email-control/rules/{id}",
            new UpdateEmailRuleRequest(true, true, false, false, false, false, false, 0, "Enabled"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("email_control.mode_invalid", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task Rule_Update_NegativeCooldown_Rejected()
    {
        var admin = await AdminClientAsync();
        var id = await AnyRuleIdAsync(admin);
        var res = await admin.PutAsJsonAsync($"/api/email-control/rules/{id}",
            new UpdateEmailRuleRequest(true, true, false, false, false, false, false, -5, "DryRun"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("email_control.cooldown_invalid", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task Rule_Update_UnknownId_NotFound()
    {
        var admin = await AdminClientAsync();
        var res = await admin.PutAsJsonAsync($"/api/email-control/rules/{Guid.NewGuid()}",
            new UpdateEmailRuleRequest(true, true, false, false, false, false, false, 0, "DryRun"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("email_control.rule_not_found", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task Rule_Update_Valid_Persists_ThenRestore()
    {
        var admin = await AdminClientAsync();
        var rules = await (await admin.GetAsync("/api/email-control/rules"))
            .Content.ReadFromJsonAsync<List<EmailRuleDto>>(TestJson.Options);
        var before = rules!.First();

        var updated = await (await admin.PutAsJsonAsync($"/api/email-control/rules/{before.Id}",
                new UpdateEmailRuleRequest(false, before.SendToEmployee, before.SendToManager, before.SendToTeamLeader,
                    before.SendToHr, before.SendToGovernance, before.SendToAdmin, 99, "DryRun")))
            .Content.ReadFromJsonAsync<EmailRuleDto>(TestJson.Options);

        Assert.False(updated!.IsEnabled);
        Assert.Equal(99, updated.CooldownMinutes);
        Assert.NotNull(updated.UpdatedAtUtc);

        // استعادة
        await admin.PutAsJsonAsync($"/api/email-control/rules/{before.Id}",
            new UpdateEmailRuleRequest(before.IsEnabled, before.SendToEmployee, before.SendToManager, before.SendToTeamLeader,
                before.SendToHr, before.SendToGovernance, before.SendToAdmin, before.CooldownMinutes, before.Mode));
    }

    // ===== 5. معاينة المستقبِلين =====

    [Fact]
    public async Task Recipients_Users_Empty_UsersRequired()
    {
        var admin = await AdminClientAsync();
        var res = await admin.PostAsJsonAsync("/api/email-control/recipients/preview",
            new RecipientPreviewRequest(RecipientScopeType.Users, UserIds: new List<Guid>()));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("email_control.users_required", await ProblemTypeAsync(res));
    }

    [Theory]
    [InlineData(RecipientScopeType.Team)]
    [InlineData(RecipientScopeType.Department)]
    [InlineData(RecipientScopeType.JobRole)]
    public async Task Recipients_MissingScopeId_ScopeIdRequired(RecipientScopeType scope)
    {
        var admin = await AdminClientAsync();
        var res = await admin.PostAsJsonAsync("/api/email-control/recipients/preview",
            new RecipientPreviewRequest(scope));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("email_control.scope_id_required", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task Recipients_IdentityRole_MissingRole_RoleRequired()
    {
        var admin = await AdminClientAsync();
        var res = await admin.PostAsJsonAsync("/api/email-control/recipients/preview",
            new RecipientPreviewRequest(RecipientScopeType.IdentityRole));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("email_control.role_required", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task Recipients_Users_ActiveEligible()
    {
        var admin = await AdminClientAsync();
        var id = await CreateActiveUserAsync();
        var dto = await (await admin.PostAsJsonAsync("/api/email-control/recipients/preview",
                new RecipientPreviewRequest(RecipientScopeType.Users, UserIds: new List<Guid> { id })))
            .Content.ReadFromJsonAsync<RecipientPreviewDto>(TestJson.Options);

        Assert.NotNull(dto);
        Assert.Equal(1, dto!.TotalCandidates);
        Assert.Equal(1, dto.EligibleCount);
        var row = Assert.Single(dto.Rows);
        Assert.True(row.Eligible);
        Assert.Equal("مؤهَّل", row.Reason);
    }

    [Fact]
    public async Task Recipients_Users_Inactive_Excluded()
    {
        var admin = await AdminClientAsync();
        var id = await CreateInactiveUserAsync();
        var dto = await (await admin.PostAsJsonAsync("/api/email-control/recipients/preview",
                new RecipientPreviewRequest(RecipientScopeType.Users, UserIds: new List<Guid> { id })))
            .Content.ReadFromJsonAsync<RecipientPreviewDto>(TestJson.Options);

        Assert.Equal(0, dto!.EligibleCount);
        Assert.Equal(1, dto.ExcludedCount);
        Assert.Equal("الحساب غير نشط", dto.Rows[0].Reason);
    }

    [Fact]
    public async Task Recipients_Users_NoEmail_Excluded()
    {
        var admin = await AdminClientAsync();
        var id = await CreateNoEmailUserAsync();
        var dto = await (await admin.PostAsJsonAsync("/api/email-control/recipients/preview",
                new RecipientPreviewRequest(RecipientScopeType.Users, UserIds: new List<Guid> { id })))
            .Content.ReadFromJsonAsync<RecipientPreviewDto>(TestJson.Options);

        Assert.Equal(0, dto!.EligibleCount);
        Assert.Equal(1, dto.ExcludedCount);
        Assert.Equal("لا يوجد بريد إلكتروني", dto.Rows[0].Reason);
    }

    [Fact]
    public async Task Recipients_Users_MixedCandidates()
    {
        var admin = await AdminClientAsync();
        var active = await CreateActiveUserAsync();
        var inactive = await CreateInactiveUserAsync();
        var noEmail = await CreateNoEmailUserAsync();

        var dto = await (await admin.PostAsJsonAsync("/api/email-control/recipients/preview",
                new RecipientPreviewRequest(RecipientScopeType.Users, UserIds: new List<Guid> { active, inactive, noEmail })))
            .Content.ReadFromJsonAsync<RecipientPreviewDto>(TestJson.Options);

        Assert.Equal(3, dto!.TotalCandidates);
        Assert.Equal(1, dto.EligibleCount);
        Assert.Equal(2, dto.ExcludedCount);
    }

    [Fact]
    public async Task Recipients_IdentityRole_ResolvesRows()
    {
        var admin = await AdminClientAsync();
        var id = await CreateActiveUserAsync(Roles.Employee);

        var dto = await (await admin.PostAsJsonAsync("/api/email-control/recipients/preview",
                new RecipientPreviewRequest(RecipientScopeType.IdentityRole, RoleName: Roles.Employee)))
            .Content.ReadFromJsonAsync<RecipientPreviewDto>(TestJson.Options);

        Assert.NotNull(dto);
        Assert.True(dto!.TotalCandidates >= 1);
        Assert.Contains(dto.Rows, r => r.UserId == id && r.Eligible);
    }

    // ===== 6. التذكير اليدويّ DryRun =====

    [Fact]
    public async Task Manual_MissingSubject_Rejected()
    {
        var admin = await AdminClientAsync();
        var id = await CreateActiveUserAsync();
        var res = await admin.PostAsJsonAsync("/api/email-control/manual-reminders/dry-run",
            new ManualReminderDryRunRequest(RecipientScopeType.Users, "  ", "متن", UserIds: new List<Guid> { id }));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("email_control.subject_required", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task Manual_MissingBody_Rejected()
    {
        var admin = await AdminClientAsync();
        var id = await CreateActiveUserAsync();
        var res = await admin.PostAsJsonAsync("/api/email-control/manual-reminders/dry-run",
            new ManualReminderDryRunRequest(RecipientScopeType.Users, "عنوان", "  ", UserIds: new List<Guid> { id }));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("email_control.body_required", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task Manual_NoEligible_Rejected()
    {
        var admin = await AdminClientAsync();
        var inactive = await CreateInactiveUserAsync();
        var res = await admin.PostAsJsonAsync("/api/email-control/manual-reminders/dry-run",
            new ManualReminderDryRunRequest(RecipientScopeType.Users, "عنوان", "متن", UserIds: new List<Guid> { inactive }));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("email_control.no_eligible_recipients", await ProblemTypeAsync(res));
    }

    [Fact]
    public async Task Manual_Eligible_CreatesDryRunRows_NoOutboxSideEffect()
    {
        var admin = await AdminClientAsync();
        var id = await CreateActiveUserAsync();

        var outboxBefore = await OutboxCountAsync(_factory);

        var result = await (await admin.PostAsJsonAsync("/api/email-control/manual-reminders/dry-run",
                new ManualReminderDryRunRequest(RecipientScopeType.Users, "تذكير تجريبي", "هذا تذكير DryRun تجريبي.",
                    UserIds: new List<Guid> { id })))
            .Content.ReadFromJsonAsync<ManualReminderDryRunResultDto>(TestJson.Options);

        Assert.NotNull(result);
        Assert.Equal(1, result!.Total);
        Assert.Equal(1, result.Created);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(0, result.Duplicate);
        Assert.NotEqual(Guid.Empty, result.BatchId);

        // صفّ DryRun أُنشئ في email_notifications عبر القلب الآمن
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.EmailNotifications.AsNoTracking()
                // EMAIL-DRYRUN-DEDUPLICATION-ISOLATION-R1: صفوف المحاكاة تُخزَّن بمفتاح معزول (بادئة dryrun:)
                .FirstOrDefaultAsync(n => n.CorrelationKey
                    == $"{EmailNotificationService.DryRunCorrelationKeyPrefix}manual-reminder:{result.BatchId}:{id}");
            Assert.NotNull(row);
            Assert.Equal(Domain.Enums.EmailNotificationStatus.DryRun, row!.Status);
            Assert.Equal("manual.reminder", row.EventType);
            Assert.Equal("ManualReminder", row.EntityType);
        }

        // لا أثر على صندوق الصادر (لا إرسال فعليّ)
        Assert.Equal(outboxBefore, await OutboxCountAsync(_factory));
    }
}
