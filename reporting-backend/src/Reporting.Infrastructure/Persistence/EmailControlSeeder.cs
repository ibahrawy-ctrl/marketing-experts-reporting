using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Domain.Entities.System;

namespace Reporting.Infrastructure.Persistence;

/// <summary>
/// بذر قوالب/قواعد مركز التحكم بالبريد (EMAIL-CONTROL-CENTER-R1) — 10 قوالب أساسية + قواعد تشغيلية.
/// DryRun فقط، IsEnabled=true. إدراج مرة واحدة (idempotent بالمفتاح) — لا يحذف/يعدّل القائم.
/// إضافيّ بحت: لا يمسّ email_notifications/email_outbox/notifications ولا أي سير عمل.
/// </summary>
public static class EmailControlSeeder
{
    private record TplDef(string Key, string NameAr, string Category, string Subject, string Body, string[] Variables);
    private record RuleDef(string TemplateKey, string EventType, bool Emp, bool Mgr, bool Tl, bool Hr, bool Gov, bool Admin, int? Cooldown);

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();

        var existingKeys = (await db.EmailTemplates.Select(t => t.Key).ToListAsync()).ToHashSet();
        foreach (var def in TemplateDefs)
        {
            if (existingKeys.Contains(def.Key)) continue;
            db.EmailTemplates.Add(new EmailTemplate
            {
                Key = def.Key,
                NameAr = def.NameAr,
                Category = def.Category,
                SubjectTemplate = def.Subject,
                BodyTemplate = def.Body,
                AvailableVariablesJson = JsonSerializer.Serialize(def.Variables),
                IsEnabled = true,
                DefaultMode = "DryRun"
            });
        }

        var existingRules = (await db.EmailRules.Select(r => new { r.TemplateKey, r.EventType }).ToListAsync())
            .Select(r => $"{r.TemplateKey}|{r.EventType}").ToHashSet();
        foreach (var r in RuleDefs)
        {
            if (existingRules.Contains($"{r.TemplateKey}|{r.EventType}")) continue;
            db.EmailRules.Add(new EmailRule
            {
                TemplateKey = r.TemplateKey,
                EventType = r.EventType,
                IsEnabled = true,
                SendToEmployee = r.Emp,
                SendToManager = r.Mgr,
                SendToTeamLeader = r.Tl,
                SendToHr = r.Hr,
                SendToGovernance = r.Gov,
                SendToAdmin = r.Admin,
                CooldownMinutes = r.Cooldown,
                Mode = "DryRun"
            });
        }

        await db.SaveChangesAsync();
    }

    private static readonly TplDef[] TemplateDefs =
    {
        // Confirmation (بنية جاهزة للمستقبل — لا منطق تأكيد يُنفَّذ في R1)
        new("AUTH_EMAIL_CONFIRMATION", "تأكيد البريد الإلكتروني", "Confirmation",
            "تأكيد بريدك في نظام تقارير خبراء التسويق",
            "مرحبًا {{UserName}}،\nلإكمال تفعيل حسابك يرجى تأكيد بريدك الإلكتروني عبر الرابط أدناه. الرابط صالح لمدة {{ExpiryHours}} ساعة.",
            new[] { "UserName", "ConfirmationLink", "ExpiryHours" }),
        new("AUTH_RESEND_CONFIRMATION", "إعادة إرسال تأكيد البريد", "Confirmation",
            "إعادة إرسال رابط تأكيد البريد",
            "مرحبًا {{UserName}}،\nطلبت إعادة إرسال رابط تأكيد البريد. الرابط صالح لمدة {{ExpiryHours}} ساعة.",
            new[] { "UserName", "ConfirmationLink", "ExpiryHours" }),

        // Reports
        new("REPORT_REMINDER", "تذكير بتسليم التقرير", "Reports",
            "تذكير: تسليم {{ReportTitle}} — {{PeriodLabel}}",
            "مرحبًا {{UserName}}،\nهذا تذكير بتسليم تقرير «{{ReportTitle}}» لفترة {{PeriodLabel}}. الموعد النهائي: {{DueDate}}.",
            new[] { "UserName", "ReportTitle", "PeriodLabel", "DueDate", "Link" }),
        new("REPORT_OVERDUE", "تنبيه تأخّر التقرير", "Reports",
            "تنبيه: تأخّر تسليم {{ReportTitle}} — {{PeriodLabel}}",
            "مرحبًا {{UserName}}،\nتأخّر تسليم تقرير «{{ReportTitle}}» لفترة {{PeriodLabel}} (كان مستحقًّا في {{DueDate}}). يرجى التسليم في أقرب وقت.",
            new[] { "UserName", "ReportTitle", "PeriodLabel", "DueDate", "Link" }),
        new("REPORT_REVIEW_READY", "تقرير جاهز للمراجعة", "Reports",
            "تقرير بانتظار مراجعتك — {{ReportTitle}}",
            "مرحبًا {{ReviewerName}}،\nقدّم {{EmployeeName}} تقرير «{{ReportTitle}}» لفترة {{PeriodLabel}} وهو بانتظار مراجعتك.",
            new[] { "ReviewerName", "EmployeeName", "ReportTitle", "PeriodLabel", "Link" }),

        // Governance
        new("GOVERNANCE_ESCALATION", "تصعيد حوكمة", "Governance",
            "تصعيد حوكمة: {{Title}}",
            "مرحبًا {{RecipientName}}،\nتمّ تصعيد بند حوكمة «{{Title}}» بدرجة أهمية {{Severity}}. يرجى الاطلاع واتخاذ اللازم.",
            new[] { "RecipientName", "Title", "Severity", "Link" }),
        new("GOVERNANCE_ACTION_ITEM", "إجراء حوكمة/متابعة", "Governance",
            "إجراء متابعة: {{Title}}",
            "مرحبًا {{RecipientName}}،\nأُسنِد إليك إجراء متابعة «{{Title}}» بموعد استحقاق {{DueDate}}.",
            new[] { "RecipientName", "Title", "DueDate", "Link" }),

        // HR
        new("HR_REQUEST_CREATED", "طلب موارد بشرية جديد", "HR",
            "طلب موارد بشرية جديد — {{RequestType}}",
            "مرحبًا {{RecipientName}}،\nقدّم {{RequesterName}} طلب موارد بشرية من نوع «{{RequestType}}» بانتظار المعالجة.",
            new[] { "RecipientName", "RequesterName", "RequestType", "Link" }),
        new("HR_REQUEST_DECISION", "قرار على طلب الموارد البشرية", "HR",
            "قرار على طلبك — {{RequestType}}",
            "مرحبًا {{UserName}}،\nتمّ اتخاذ قرار «{{Decision}}» على طلبك من نوع «{{RequestType}}».",
            new[] { "UserName", "RequestType", "Decision", "Link" }),

        // Common
        new("MANUAL_REMINDER", "تذكير يدويّ", "Common",
            "{{Subject}}",
            "مرحبًا {{UserName}}،\n{{Body}}",
            new[] { "UserName", "Subject", "Body", "Link" })
    };

    private static readonly RuleDef[] RuleDefs =
    {
        new("REPORT_REMINDER",       "report.reminder",        Emp: true,  Mgr: false, Tl: false, Hr: false, Gov: false, Admin: false, Cooldown: 1440),
        new("REPORT_OVERDUE",        "report.overdue",         Emp: true,  Mgr: true,  Tl: true,  Hr: false, Gov: false, Admin: false, Cooldown: 1440),
        new("REPORT_REVIEW_READY",   "report.review_ready",    Emp: false, Mgr: true,  Tl: true,  Hr: false, Gov: false, Admin: false, Cooldown: null),
        new("GOVERNANCE_ESCALATION", "governance.escalation",  Emp: false, Mgr: true,  Tl: false, Hr: false, Gov: true,  Admin: true,  Cooldown: null),
        new("GOVERNANCE_ACTION_ITEM","governance.action_item", Emp: true,  Mgr: false, Tl: false, Hr: false, Gov: true,  Admin: false, Cooldown: null),
        new("HR_REQUEST_CREATED",    "hr_request.created",     Emp: false, Mgr: false, Tl: false, Hr: true,  Gov: false, Admin: false, Cooldown: null),
        new("HR_REQUEST_DECISION",   "hr_request.decision",    Emp: true,  Mgr: false, Tl: false, Hr: false, Gov: false, Admin: false, Cooldown: null)
    };
}
