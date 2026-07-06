using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Common;
using Reporting.Application.Notifications;
using Reporting.Infrastructure.Identity;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// مركز التحكم بالبريد (EMAIL-CONTROL-CENTER-R1) — إدارة قوالب/قواعد + معاينة مستقبِلين + تذكير يدويّ DryRun.
/// R1: DryRun فقط — لا SMTP/لا إرسال فعليّ. الكتابة للأدمن حصرًا. لا يمسّ أي سير عمل قائم.
/// التذكير اليدويّ يمرّ عبر القلب الآمن نفسه (EnqueueReportReminderAsync) فيحترم الوضع/التكرار/غياب البريد.
/// </summary>
public class EmailControlService : IEmailControlService
{
    private const int MaxRecipients = 100;
    private static readonly string[] AllowedModes = { "DryRun", "Disabled" };

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IEmailNotificationService _notifications;

    public EmailControlService(
        AppDbContext db,
        UserManager<ApplicationUser> users,
        IEmailNotificationService notifications)
    {
        _db = db;
        _users = users;
        _notifications = notifications;
    }

    // ===== القوالب =====

    public async Task<IReadOnlyList<EmailTemplateDto>> ListTemplatesAsync(CancellationToken ct = default)
    {
        var rows = await _db.EmailTemplates.AsNoTracking()
            .OrderBy(t => t.Category).ThenBy(t => t.NameAr)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<EmailTemplateDto?> GetTemplateAsync(string key, CancellationToken ct = default)
    {
        var row = await _db.EmailTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Key == key, ct);
        return row is null ? null : ToDto(row);
    }

    public async Task<Result<EmailTemplateDto>> UpdateTemplateAsync(string key, UpdateEmailTemplateRequest request, Guid actorId, CancellationToken ct = default)
    {
        var mode = (request.DefaultMode ?? string.Empty).Trim();
        if (!AllowedModes.Contains(mode, StringComparer.OrdinalIgnoreCase))
            return Result<EmailTemplateDto>.Failure("الوضع المسموح في هذا الإصدار هو DryRun أو Disabled فقط.", "email_control.mode_invalid");

        if (string.IsNullOrWhiteSpace(request.NameAr))
            return Result<EmailTemplateDto>.Failure("اسم القالب مطلوب.", "email_control.name_required");
        if (string.IsNullOrWhiteSpace(request.SubjectTemplate))
            return Result<EmailTemplateDto>.Failure("عنوان الرسالة مطلوب.", "email_control.subject_required");
        if (string.IsNullOrWhiteSpace(request.BodyTemplate))
            return Result<EmailTemplateDto>.Failure("متن الرسالة مطلوب.", "email_control.body_required");

        var row = await _db.EmailTemplates.FirstOrDefaultAsync(t => t.Key == key, ct);
        if (row is null)
            return Result<EmailTemplateDto>.Failure("القالب غير موجود.", "email_control.template_not_found");

        row.NameAr = request.NameAr.Trim();
        row.SubjectTemplate = request.SubjectTemplate.Trim();
        row.BodyTemplate = request.BodyTemplate;
        row.IsEnabled = request.IsEnabled;
        row.DefaultMode = NormalizeMode(mode);
        row.UpdatedByUserId = actorId;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Result<EmailTemplateDto>.Success(ToDto(row));
    }

    public async Task<Result<EmailTemplatePreviewDto>> PreviewTemplateAsync(string key, EmailTemplatePreviewRequest request, CancellationToken ct = default)
    {
        var row = await _db.EmailTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Key == key, ct);
        if (row is null)
            return Result<EmailTemplatePreviewDto>.Failure("القالب غير موجود.", "email_control.template_not_found");

        var subjectTemplate = string.IsNullOrWhiteSpace(request.SubjectTemplate) ? row.SubjectTemplate : request.SubjectTemplate!;
        var bodyTemplate = string.IsNullOrWhiteSpace(request.BodyTemplate) ? row.BodyTemplate : request.BodyTemplate!;

        var vars = BuildPreviewVariables(row, request.Variables);
        var subject = ApplyPlaceholders(subjectTemplate, vars);
        var bodyText = ApplyPlaceholders(bodyTemplate, vars);
        var link = vars.TryGetValue("Link", out var l) && !string.IsNullOrWhiteSpace(l) ? l : null;
        var bodyHtml = EmailHtml.Build(subject, bodyText, link);

        return Result<EmailTemplatePreviewDto>.Success(new EmailTemplatePreviewDto(subject, bodyHtml, bodyText));
    }

    // ===== القواعد =====

    public async Task<IReadOnlyList<EmailRuleDto>> ListRulesAsync(CancellationToken ct = default)
    {
        var rows = await _db.EmailRules.AsNoTracking()
            .OrderBy(r => r.TemplateKey).ThenBy(r => r.EventType)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<EmailRuleDto?> GetRuleAsync(Guid id, CancellationToken ct = default)
    {
        var row = await _db.EmailRules.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        return row is null ? null : ToDto(row);
    }

    public async Task<Result<EmailRuleDto>> UpdateRuleAsync(Guid id, UpdateEmailRuleRequest request, Guid actorId, CancellationToken ct = default)
    {
        var mode = (request.Mode ?? string.Empty).Trim();
        if (!AllowedModes.Contains(mode, StringComparer.OrdinalIgnoreCase))
            return Result<EmailRuleDto>.Failure("الوضع المسموح في هذا الإصدار هو DryRun أو Disabled فقط.", "email_control.mode_invalid");

        if (request.CooldownMinutes is < 0)
            return Result<EmailRuleDto>.Failure("مدة التهدئة يجب أن تكون صفرًا أو أكثر.", "email_control.cooldown_invalid");

        var row = await _db.EmailRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (row is null)
            return Result<EmailRuleDto>.Failure("القاعدة غير موجودة.", "email_control.rule_not_found");

        row.IsEnabled = request.IsEnabled;
        row.SendToEmployee = request.SendToEmployee;
        row.SendToManager = request.SendToManager;
        row.SendToTeamLeader = request.SendToTeamLeader;
        row.SendToHr = request.SendToHr;
        row.SendToGovernance = request.SendToGovernance;
        row.SendToAdmin = request.SendToAdmin;
        row.CooldownMinutes = request.CooldownMinutes;
        row.Mode = NormalizeMode(mode);
        row.UpdatedByUserId = actorId;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Result<EmailRuleDto>.Success(ToDto(row));
    }

    // ===== معاينة المستقبِلين =====

    public async Task<Result<RecipientPreviewDto>> PreviewRecipientsAsync(RecipientPreviewRequest request, CancellationToken ct = default)
    {
        var resolved = await ResolveRecipientsAsync(request.ScopeType, request.ScopeId, request.RoleName, request.UserIds, ct);
        if (!resolved.Succeeded)
            return Result<RecipientPreviewDto>.Failure(resolved.Error!, resolved.ErrorCode);

        return Result<RecipientPreviewDto>.Success(BuildPreviewDto(resolved.Value!));
    }

    // ===== تذكير يدويّ DryRun =====

    public async Task<Result<ManualReminderDryRunResultDto>> ManualReminderDryRunAsync(ManualReminderDryRunRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Subject))
            return Result<ManualReminderDryRunResultDto>.Failure("عنوان الرسالة مطلوب.", "email_control.subject_required");
        if (string.IsNullOrWhiteSpace(request.Body))
            return Result<ManualReminderDryRunResultDto>.Failure("متن الرسالة مطلوب.", "email_control.body_required");

        var resolved = await ResolveRecipientsAsync(request.ScopeType, request.ScopeId, request.RoleName, request.UserIds, ct);
        if (!resolved.Succeeded)
            return Result<ManualReminderDryRunResultDto>.Failure(resolved.Error!, resolved.ErrorCode);

        var rows = resolved.Value!;
        var eligible = rows.Where(r => r.Eligible).ToList();
        if (eligible.Count == 0)
            return Result<ManualReminderDryRunResultDto>.Failure("لا يوجد مستقبِلون مؤهَّلون لإرسال التذكير.", "email_control.no_eligible_recipients");
        if (eligible.Count > MaxRecipients)
            return Result<ManualReminderDryRunResultDto>.Failure($"عدد المستقبِلين المؤهَّلين ({eligible.Count}) يتجاوز الحدّ الأقصى ({MaxRecipients}).", "email_control.too_many_recipients");

        var batchId = Guid.NewGuid();
        var subject = request.Subject.Trim();
        var link = string.IsNullOrWhiteSpace(request.Link) ? string.Empty : request.Link!.Trim();

        var created = 0;
        var skipped = 0;
        var duplicate = 0;
        foreach (var r in eligible)
        {
            var outcome = await _notifications.EnqueueReportReminderAsync(new ReportReminderMessage(
                EventType: "manual.reminder",
                RecipientUserId: r.UserId,
                CorrelationKey: $"manual-reminder:{batchId}:{r.UserId}",
                Subject: subject,
                Body: request.Body,
                Link: link,
                EntityId: batchId,
                EntityType: "ManualReminder"), ct);

            switch (outcome)
            {
                case ReportReminderOutcome.Created: created++; break;
                case ReportReminderOutcome.Duplicate: duplicate++; break;
                default: skipped++; break; // Disabled / SkippedNoEmail / Error
            }
        }

        return Result<ManualReminderDryRunResultDto>.Success(new ManualReminderDryRunResultDto(
            BatchId: batchId,
            Total: eligible.Count,
            Created: created,
            Skipped: skipped,
            Duplicate: duplicate,
            Recipients: eligible));
    }

    // ===== حلّ المستقبِلين حسب النطاق =====

    private async Task<Result<List<RecipientPreviewRowDto>>> ResolveRecipientsAsync(
        RecipientScopeType scopeType, Guid? scopeId, string? roleName, List<Guid>? userIds, CancellationToken ct)
    {
        List<ApplicationUser> candidates;

        switch (scopeType)
        {
            case RecipientScopeType.Users:
            {
                if (userIds is null || userIds.Count == 0)
                    return Result<List<RecipientPreviewRowDto>>.Failure("يجب تحديد قائمة مستخدمين.", "email_control.users_required");
                var ids = userIds.Distinct().ToList();
                candidates = await _db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToListAsync(ct);
                break;
            }
            case RecipientScopeType.Team:
            {
                if (scopeId is not { } teamId)
                    return Result<List<RecipientPreviewRowDto>>.Failure("يجب تحديد الفريق.", "email_control.scope_id_required");
                candidates = await _db.Users.AsNoTracking().Where(u => u.TeamId == teamId).ToListAsync(ct);
                break;
            }
            case RecipientScopeType.Department:
            {
                if (scopeId is not { } deptId)
                    return Result<List<RecipientPreviewRowDto>>.Failure("يجب تحديد الإدارة.", "email_control.scope_id_required");
                candidates = await _db.Users.AsNoTracking().Where(u => u.DepartmentId == deptId).ToListAsync(ct);
                break;
            }
            case RecipientScopeType.JobRole:
            {
                if (scopeId is not { } jobRoleId)
                    return Result<List<RecipientPreviewRowDto>>.Failure("يجب تحديد المسمّى الوظيفي.", "email_control.scope_id_required");
                candidates = await _db.Users.AsNoTracking().Where(u => u.JobRoleId == jobRoleId).ToListAsync(ct);
                break;
            }
            case RecipientScopeType.IdentityRole:
            {
                if (string.IsNullOrWhiteSpace(roleName))
                    return Result<List<RecipientPreviewRowDto>>.Failure("يجب تحديد الدور.", "email_control.role_required");
                var inRole = await _users.GetUsersInRoleAsync(roleName.Trim());
                candidates = inRole.ToList();
                break;
            }
            default:
                return Result<List<RecipientPreviewRowDto>>.Failure("نوع النطاق غير مدعوم.", "email_control.scope_invalid");
        }

        var seen = new HashSet<Guid>();
        var rows = new List<RecipientPreviewRowDto>();
        foreach (var u in candidates.OrderBy(u => u.FullName))
        {
            if (!seen.Add(u.Id))
            {
                rows.Add(new RecipientPreviewRowDto(u.Id, u.FullName, u.Email, false, "مكرّر"));
                continue;
            }
            if (!u.IsActive)
            {
                rows.Add(new RecipientPreviewRowDto(u.Id, u.FullName, u.Email, false, "الحساب غير نشط"));
                continue;
            }
            if (string.IsNullOrWhiteSpace(u.Email))
            {
                rows.Add(new RecipientPreviewRowDto(u.Id, u.FullName, u.Email, false, "لا يوجد بريد إلكتروني"));
                continue;
            }
            rows.Add(new RecipientPreviewRowDto(u.Id, u.FullName, u.Email, true, "مؤهَّل"));
        }

        return Result<List<RecipientPreviewRowDto>>.Success(rows);
    }

    private static RecipientPreviewDto BuildPreviewDto(List<RecipientPreviewRowDto> rows)
    {
        var eligible = rows.Count(r => r.Eligible);
        return new RecipientPreviewDto(rows.Count, eligible, rows.Count - eligible, rows);
    }

    // ===== أدوات مساعدة =====

    private static string NormalizeMode(string mode) =>
        string.Equals(mode, "Disabled", StringComparison.OrdinalIgnoreCase) ? "Disabled" : "DryRun";

    private static Dictionary<string, string> BuildPreviewVariables(Domain.Entities.System.EmailTemplate template, Dictionary<string, string>? provided)
    {
        var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // قيم تجريبية افتراضية لكل متغيّر متاح في القالب.
        var available = ParseVariables(template.AvailableVariablesJson);
        foreach (var v in available)
            vars[v] = SampleValue(v);
        // تجاوز بقيم المستخدم إن وُجدت.
        if (provided is not null)
            foreach (var kv in provided)
                vars[kv.Key] = kv.Value ?? string.Empty;
        return vars;
    }

    private static string[] ParseVariables(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try { return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>(); }
        catch { return Array.Empty<string>(); }
    }

    private static string SampleValue(string variable) => variable switch
    {
        "UserName" or "RecipientName" or "ReviewerName" or "EmployeeName" or "RequesterName" => "أحمد محمد",
        "ReportTitle" => "التقرير الأسبوعي",
        "PeriodLabel" => "2026-W27",
        "DueDate" => "2026-07-05",
        "Title" => "بند حوكمة تجريبي",
        "Severity" => "عالية",
        "RequestType" => "خطاب تعريف",
        "Decision" => "موافقة",
        "ExpiryHours" => "24",
        "ConfirmationLink" or "Link" => "https://reports.example/app",
        "Subject" => "عنوان تجريبي",
        "Body" => "هذا نصّ تجريبي للمعاينة.",
        _ => $"[{variable}]"
    };

    /// <summary>استبدال بسيط للمتغيّرات {{Var}} — قبل الترميز الآمن في EmailHtml.Build.</summary>
    private static string ApplyPlaceholders(string template, Dictionary<string, string> vars)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;
        return System.Text.RegularExpressions.Regex.Replace(template, "\\{\\{\\s*(\\w+)\\s*\\}\\}", m =>
        {
            var name = m.Groups[1].Value;
            return vars.TryGetValue(name, out var val) ? val : m.Value;
        });
    }

    private static EmailTemplateDto ToDto(Domain.Entities.System.EmailTemplate t) => new(
        t.Id, t.Key, t.NameAr, t.Category, t.SubjectTemplate, t.BodyTemplate,
        ParseVariables(t.AvailableVariablesJson), t.IsEnabled, t.DefaultMode, t.UpdatedAtUtc);

    private static EmailRuleDto ToDto(Domain.Entities.System.EmailRule r) => new(
        r.Id, r.TemplateKey, r.EventType, r.IsEnabled,
        r.SendToEmployee, r.SendToManager, r.SendToTeamLeader, r.SendToHr, r.SendToGovernance, r.SendToAdmin,
        r.CooldownMinutes, r.Mode, r.UpdatedAtUtc);
}
