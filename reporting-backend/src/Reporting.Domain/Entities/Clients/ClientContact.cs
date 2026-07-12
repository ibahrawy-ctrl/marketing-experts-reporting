using Reporting.Domain.Common;

namespace Reporting.Domain.Entities.Clients;

/// <summary>
/// جهة اتصال لدى العميل (Client 360 — CPW-R1B). يحتوي العميل الواحد على عدد غير محدود من
/// جهات الاتصال. جهة اتصال أساسية واحدة نشطة كحدّ أقصى لكلّ عميل (يُفرَض عبر معاملة + فهرس فريد جزئيّ).
/// التعطيل بدل الحذف افتراضيًّا (نمط تيار العمل). لا تُخزَّن أيّ أسرار/كلمات مرور هنا.
/// </summary>
public class ClientContact : BaseEntity
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>المسمّى الوظيفيّ لجهة الاتصال (اختياريّ).</summary>
    public string? JobTitle { get; set; }

    /// <summary>الإدارة/القسم لدى العميل (اختياريّ).</summary>
    public string? Department { get; set; }

    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? WhatsApp { get; set; }

    /// <summary>رمز طريقة التواصل المفضَّلة من ثوابت الرموز المعتمدة (نصّ فقط — بلا FK).</summary>
    public string? PreferredContactMethodCode { get; set; }

    /// <summary>جهة اتصال أساسية (واحدة نشطة كحدّ أقصى لكلّ عميل).</summary>
    public bool IsPrimary { get; set; }

    /// <summary>جهة اتصال ماليّة (لأغراض الفوترة/المدفوعات).</summary>
    public bool IsFinancialContact { get; set; }

    public string? Notes { get; set; }

    /// <summary>التعطيل بدل الحذف — نمط تيار العمل. BaseEntity لا يحمل IsActive فيُعرَّف هنا.</summary>
    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}
