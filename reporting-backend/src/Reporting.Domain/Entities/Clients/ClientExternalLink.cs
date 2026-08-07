using Reporting.Domain.Common;

namespace Reporting.Domain.Entities.Clients;

/// <summary>
/// رابط خارجيّ مهمّ مرتبط بالعميل (CPW-R1B2 — «الروابط المهمّة»).
/// مرجع عنوان فقط: لا يُخزَّن هنا — ولا في أيّ حقل — أيّ كلمة مرور أو رمز وصول
/// أو مفتاح API أو كوكي أو رمز استرداد أو بيانات جلسة أو اعتماد OAuth.
/// الخدمة ترفض حفظ الرابط إن حمل ما يشبه سرًّا (بارامترات token/key/password…).
/// لا حذف فعليّ من الواجهة — التعطيل (<see cref="IsActive"/>) فقط.
/// </summary>
public class ClientExternalLink : BaseEntity
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>العنوان المطلق (http/https حصرًا، بلا اعتمادات مضمَّنة).</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>رمز التصنيف من ثوابت الرموز المعتمَدة (نصّ فقط — بلا FK).</summary>
    public string CategoryCode { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public Guid CreatedByUserId { get; set; }
}
