using Reporting.Domain.Common;

namespace Reporting.Domain.Entities.System;

/// <summary>
/// قالب بريد قابل للتحرير (EMAIL-CONTROL-CENTER-R1) — عنوان + متن + متغيّرات + تفعيل.
/// إضافيّ بحت؛ لا يمسّ email_notifications/email_outbox ولا أي سير عمل. DryRun فقط في R1.
/// </summary>
public class EmailTemplate : BaseEntity
{
    /// <summary>مفتاح ثابت فريد (مثل REPORT_REMINDER) — يُستخدَم للربط والبذر (idempotent).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>الاسم العربي المعروض في لوحة التحكم.</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>التصنيف (Common/Reports/Governance/HR/Confirmation) — للتجميع في الواجهة.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>قالب العنوان — يدعم متغيّرات {{variable}}.</summary>
    public string SubjectTemplate { get; set; } = string.Empty;

    /// <summary>قالب المتن (نصّ عربي) — يدعم متغيّرات {{variable}}. يُحوَّل لـ HTML آمن عند المعاينة.</summary>
    public string BodyTemplate { get; set; } = string.Empty;

    /// <summary>قائمة المتغيّرات المتاحة (JSON مصفوفة نصوص) — إعلاميّة للواجهة، nullable.</summary>
    public string? AvailableVariablesJson { get; set; }

    /// <summary>هل القالب مُفعَّل؟ (لا يُرسِل شيئًا فعليًّا في R1 — DryRun فقط).</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>الوضع الافتراضي — DryRun فقط في R1 (Enabled/Real ممنوع خادميًّا).</summary>
    public string DefaultMode { get; set; } = "DryRun";

    /// <summary>مُعرّف آخر من عدّل القالب (Admin فقط)، nullable.</summary>
    public Guid? UpdatedByUserId { get; set; }
}
