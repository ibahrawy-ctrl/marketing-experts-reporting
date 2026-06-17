using Reporting.Domain.Enums;

namespace Reporting.Application.Common;

/// <summary>
/// حارس دورية التقارير (المرحلة الحالية): مندوبو المبيعات (B2C/B2B) تقاريرهم «يومية» فقط،
/// وبقية الأدوار «أسبوعية». يُفرض هذا خادميًّا عند إنشاء التسليم بحسب المسمّى الوظيفي للمُرسِل،
/// وليس بالاعتماد على اسم مستخدم بعينه. (التقويم الكامل وآلية الإغلاق مؤجَّلة لمرحلة لاحقة.)
/// </summary>
public static class ReportCadencePolicy
{
    // أكواد المسمّيات الوظيفية التي تُلزَم بالدورية اليومية.
    public static readonly IReadOnlyCollection<string> DailyJobRoleCodes = new[] { "SALES_B2C", "SALES_B2B" };

    /// <summary>الدورية المتوقَّعة لمسمّى وظيفي معيّن (يومي للمبيعات، أسبوعي لغيرهم).</summary>
    public static PeriodType ExpectedCadence(string? jobRoleCode) =>
        jobRoleCode is not null && DailyJobRoleCodes.Contains(jobRoleCode)
            ? PeriodType.Daily
            : PeriodType.Weekly;
}
