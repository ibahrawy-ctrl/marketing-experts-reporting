using Reporting.Application.Common;

namespace Reporting.Application.Notifications;

/// <summary>
/// مركز التحكم بالبريد (EMAIL-CONTROL-CENTER-R1) — إدارة قوالب/قواعد + معاينة مستقبِلين + تذكير يدويّ DryRun.
/// R1: DryRun فقط (لا SMTP/لا إرسال فعليّ). الكتابة للأدمن حصرًا؛ لا يمسّ أي سير عمل قائم.
/// </summary>
public interface IEmailControlService
{
    // القوالب
    Task<IReadOnlyList<EmailTemplateDto>> ListTemplatesAsync(CancellationToken ct = default);
    Task<EmailTemplateDto?> GetTemplateAsync(string key, CancellationToken ct = default);
    Task<Result<EmailTemplateDto>> UpdateTemplateAsync(string key, UpdateEmailTemplateRequest request, Guid actorId, CancellationToken ct = default);
    Task<Result<EmailTemplatePreviewDto>> PreviewTemplateAsync(string key, EmailTemplatePreviewRequest request, CancellationToken ct = default);

    // القواعد
    Task<IReadOnlyList<EmailRuleDto>> ListRulesAsync(CancellationToken ct = default);
    Task<EmailRuleDto?> GetRuleAsync(Guid id, CancellationToken ct = default);
    Task<Result<EmailRuleDto>> UpdateRuleAsync(Guid id, UpdateEmailRuleRequest request, Guid actorId, CancellationToken ct = default);

    // معاينة المستقبِلين (قبل إنشاء أي رسائل)
    Task<Result<RecipientPreviewDto>> PreviewRecipientsAsync(RecipientPreviewRequest request, CancellationToken ct = default);

    // تذكير يدويّ DryRun (معاينة أولًا داخليًّا، ثم إنشاء صفوف DryRun)
    Task<Result<ManualReminderDryRunResultDto>> ManualReminderDryRunAsync(ManualReminderDryRunRequest request, Guid actorId, CancellationToken ct = default);
}
