namespace Reporting.Application.Notifications;

// EMAIL-CONTROL-CENTER-R1 — نماذج مركز التحكم بالبريد (قوالب/قواعد/معاينة مستقبِلين/تذكير يدويّ DryRun).
// DryRun فقط: لا إرسال فعليّ، لا SMTP، الكتابة للأدمن حصرًا. إضافيّ بحت — لا يمسّ أي سير عمل.

/// <summary>قالب بريد (عرض) — بلا أسرار.</summary>
public record EmailTemplateDto(
    Guid Id,
    string Key,
    string NameAr,
    string Category,
    string SubjectTemplate,
    string BodyTemplate,
    string[] AvailableVariables,
    bool IsEnabled,
    string DefaultMode,
    DateTime? UpdatedAtUtc);

/// <summary>تعديل قالب بريد (Admin فقط). DefaultMode يُقبَل فقط DryRun/Disabled في R1.</summary>
public record UpdateEmailTemplateRequest(
    string NameAr,
    string SubjectTemplate,
    string BodyTemplate,
    bool IsEnabled,
    string DefaultMode);

/// <summary>طلب معاينة قالب — استبدال متغيّرات بسيطة (بيانات تجريبية اختيارية).</summary>
public record EmailTemplatePreviewRequest(
    string? SubjectTemplate = null,
    string? BodyTemplate = null,
    Dictionary<string, string>? Variables = null);

/// <summary>نتيجة معاينة قالب — عنوان + HTML آمن + نصّ.</summary>
public record EmailTemplatePreviewDto(string Subject, string BodyHtml, string BodyText);

/// <summary>قاعدة بريد (عرض).</summary>
public record EmailRuleDto(
    Guid Id,
    string TemplateKey,
    string EventType,
    bool IsEnabled,
    bool SendToEmployee,
    bool SendToManager,
    bool SendToTeamLeader,
    bool SendToHr,
    bool SendToGovernance,
    bool SendToAdmin,
    int? CooldownMinutes,
    string Mode,
    DateTime? UpdatedAtUtc);

/// <summary>تعديل قاعدة بريد (Admin فقط). Mode يُقبَل فقط DryRun/Disabled في R1.</summary>
public record UpdateEmailRuleRequest(
    bool IsEnabled,
    bool SendToEmployee,
    bool SendToManager,
    bool SendToTeamLeader,
    bool SendToHr,
    bool SendToGovernance,
    bool SendToAdmin,
    int? CooldownMinutes,
    string Mode);

/// <summary>نطاق اختيار المستقبِلين.</summary>
public enum RecipientScopeType
{
    Users = 0,        // قائمة معرّفات مستخدمين صريحة
    Team = 1,         // كل أعضاء فريق
    Department = 2,   // كل موظفي إدارة
    JobRole = 3,      // كل من يحمل مسمّى وظيفيًّا
    IdentityRole = 4  // كل من يحمل دور Identity (بالاسم)
}

/// <summary>طلب معاينة مستقبِلين قبل إنشاء أي رسائل.</summary>
public record RecipientPreviewRequest(
    RecipientScopeType ScopeType,
    Guid? ScopeId = null,
    string? RoleName = null,
    List<Guid>? UserIds = null);

/// <summary>صفّ مستقبِل مُحتمَل مع سبب الأهلية/الاستبعاد.</summary>
public record RecipientPreviewRowDto(
    Guid UserId,
    string FullName,
    string? Email,
    bool Eligible,
    string Reason);

/// <summary>نتيجة معاينة المستقبِلين — مؤهَّلون + مستبعَدون بأسبابهم.</summary>
public record RecipientPreviewDto(
    int TotalCandidates,
    int EligibleCount,
    int ExcludedCount,
    List<RecipientPreviewRowDto> Rows);

/// <summary>طلب تذكير يدويّ DryRun — معاينة أولًا ثم إنشاء صفوف DryRun.</summary>
public record ManualReminderDryRunRequest(
    RecipientScopeType ScopeType,
    string Subject,
    string Body,
    string? Link = null,
    Guid? ScopeId = null,
    string? RoleName = null,
    List<Guid>? UserIds = null);

/// <summary>نتيجة تذكير يدويّ DryRun — دفعة واحدة، صفّ لكل مستقبِل (Status=DryRun).</summary>
public record ManualReminderDryRunResultDto(
    Guid BatchId,
    int Total,
    int Created,
    int Skipped,
    int Duplicate,
    List<RecipientPreviewRowDto> Recipients);
