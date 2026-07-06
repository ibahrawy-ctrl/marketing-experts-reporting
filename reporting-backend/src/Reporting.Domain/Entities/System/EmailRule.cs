using Reporting.Domain.Common;

namespace Reporting.Domain.Entities.System;

/// <summary>
/// قاعدة بريد بسيطة لكل نوع حدث (EMAIL-CONTROL-CENTER-R1) — إلى مَن يُرسَل + تبريد + وضع.
/// إضافيّة بحتة؛ لا تُشغِّل أي إرسال فعليّ في R1 (DryRun فقط). لا تمسّ أي سير عمل قائم.
/// </summary>
public class EmailRule : BaseEntity
{
    /// <summary>مفتاح القالب المرتبط (EmailTemplate.Key).</summary>
    public string TemplateKey { get; set; } = string.Empty;

    /// <summary>نوع الحدث (مثل report.reminder) — يميّز القاعدة.</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>هل القاعدة مُفعَّلة؟</summary>
    public bool IsEnabled { get; set; } = true;

    // مستقبِلو الحدث (أعلام بسيطة) — تُستخدَم لاحقًا للربط الفعليّ (خارج R1).
    public bool SendToEmployee { get; set; }
    public bool SendToManager { get; set; }
    public bool SendToTeamLeader { get; set; }
    public bool SendToHr { get; set; }
    public bool SendToGovernance { get; set; }
    public bool SendToAdmin { get; set; }

    /// <summary>فترة تبريد بالدقائق لمنع التكرار (nullable = بلا تبريد).</summary>
    public int? CooldownMinutes { get; set; }

    /// <summary>الوضع — DryRun فقط في R1 (Enabled/Real مرفوض خادميًّا).</summary>
    public string Mode { get; set; } = "DryRun";

    /// <summary>مُعرّف آخر من عدّل القاعدة (Admin فقط)، nullable.</summary>
    public Guid? UpdatedByUserId { get; set; }
}
