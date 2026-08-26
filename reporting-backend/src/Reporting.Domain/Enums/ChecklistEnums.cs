namespace Reporting.Domain.Enums;

/// <summary>
/// P2-HR-010 — حالة بند قائمة خدمة الموظّف والالتزام.
///
/// <para><b>«غير منطبق» ليس «مكتمل» وليس «صفرًا»</b>: البند الذي لا ينطبق على الموظّف
/// يخرج من مقام النسبة كلّه، فلا يُحسَب إنجازًا زائفًا ولا نقصًا زائفًا.</para>
/// </summary>
public enum EmployeeChecklistStatus
{
    /// <summary>لم يبدأ — مطلوب ولم يُتّخذ فيه إجراء.</summary>
    NotStarted = 0,

    /// <summary>قيد التنفيذ.</summary>
    InProgress = 1,

    /// <summary>مكتمل.</summary>
    Completed = 2,

    /// <summary>غير منطبق على هذا الموظّف — يخرج من البسط والمقام معًا.</summary>
    NotApplicable = 3
}
