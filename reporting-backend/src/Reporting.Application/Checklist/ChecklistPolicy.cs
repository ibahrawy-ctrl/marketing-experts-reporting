using Reporting.Domain.Enums;

namespace Reporting.Application.Checklist;

/// <summary>
/// P2-HR-010 — القواعد النقيّة للقائمة (بلا قاعدة بيانات ولا حالة). قابلة للاختبار الوحدويّ مباشرةً.
/// </summary>
public static class ChecklistPolicy
{
    /// <summary>
    /// حالة بند محسوب من عدد بنوده المفتوحة.
    /// <para><b>لا يوجد «قيد التنفيذ» للمحسوب</b>: المصدر إمّا فيه بند مفتوح أو لا،
    /// واختراع حالة وسطى كان سيتطلّب حالةً لا يملكها المصدر ⇒ تخمينًا يُعرَض كحقيقة.</para>
    /// <para>و«غير منطبق» ليس صفرًا: يُمرَّر صراحةً حين لا ينطبق البند أصلًا (لا إسناد، لا وحدة مفعّلة).</para>
    /// </summary>
    public static EmployeeChecklistStatus ComputedStatus(int openCount, bool applicable = true)
    {
        if (!applicable) return EmployeeChecklistStatus.NotApplicable;
        return openCount > 0 ? EmployeeChecklistStatus.NotStarted : EmployeeChecklistStatus.Completed;
    }

    public static string StatusLabelAr(EmployeeChecklistStatus status) => status switch
    {
        EmployeeChecklistStatus.NotStarted => "لم يبدأ",
        EmployeeChecklistStatus.InProgress => "قيد التنفيذ",
        EmployeeChecklistStatus.Completed => "مكتمل",
        EmployeeChecklistStatus.NotApplicable => "غير منطبق",
        _ => "—"
    };

    /// <summary>
    /// تسمية أدقّ للبند المحسوب: «مكتمل» في بند عدّاد يُضلِّل، فالصواب «لا بنود مفتوحة».
    /// </summary>
    public static string ComputedStatusLabelAr(EmployeeChecklistStatus status, int openCount) => status switch
    {
        EmployeeChecklistStatus.NotApplicable => "غير منطبق",
        EmployeeChecklistStatus.Completed => "لا بنود مفتوحة",
        _ => $"{openCount} بندًا مفتوحًا"
    };

    /// <summary>
    /// ملخّص القائمة. <b>غير المنطبق خارج المقام</b> كي لا تُنتِج القسمة نسبةً كاذبة
    /// تُعاقِب موظّفًا على بند لا ينطبق عليه أصلًا.
    /// </summary>
    public static ChecklistSummaryDto Summarize(IReadOnlyList<ChecklistItemDto> items)
    {
        var notApplicable = items.Count(i => i.Status == EmployeeChecklistStatus.NotApplicable);
        var applicable = items.Count - notApplicable;
        var completed = items.Count(i => i.Status == EmployeeChecklistStatus.Completed);
        var open = applicable - completed;
        var mine = items.Count(i => i.RequiresMyAction);

        var ratio = applicable == 0 ? 0m : Math.Round((decimal)completed / applicable, 4);
        return new ChecklistSummaryDto(applicable, completed, open, notApplicable, mine, ratio);
    }

    /// <summary>ترتيب ثابت: ما يحتاج فعلي أوّلًا، ثمّ المفتوح، ثمّ حسب المجموعة والمفتاح.</summary>
    public static IReadOnlyList<ChecklistItemDto> Order(IEnumerable<ChecklistItemDto> items) =>
        items
            .OrderByDescending(i => i.RequiresMyAction)
            .ThenBy(i => i.Status == EmployeeChecklistStatus.Completed
                         || i.Status == EmployeeChecklistStatus.NotApplicable)
            .ThenBy(i => i.GroupAr, StringComparer.Ordinal)
            .ThenBy(i => i.Key, StringComparer.Ordinal)
            .ToList();

    /// <summary>هل الانتقال بين حالتَي بند يدويّ مسموح؟ كلّ الحالات نهائيّة قابلة للتصحيح — عدا العدم.</summary>
    public static bool IsValidManualStatus(EmployeeChecklistStatus status) =>
        Enum.IsDefined(typeof(EmployeeChecklistStatus), status);
}
