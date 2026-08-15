using Microsoft.EntityFrameworkCore;
using Reporting.Application.Projects360;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// حرّاس تحقّق مشتركة لخدمات Project 360 (CPW-R3 · W4 · §18) — **خادميّة بالكامل**.
/// موضعها هنا لا في كلّ خدمة كي لا تتفرّق قاعدة واحدة على خمسة ملفّات فتتباين.
/// </summary>
internal static class Project360Guards
{
    /// <summary>يُفرَّغ النصّ الفارغ/المسافات إلى <c>null</c> بدل تخزين سلسلة بيضاء.</summary>
    internal static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>نسبة مئويّة معلَنة يدويًّا يجب أن تقع في 0..100.</summary>
    internal static (string Message, string Code)? ValidatePercent(decimal value, string fieldNameAr) =>
        value is < 0m or > 100m
            ? ($"{fieldNameAr} يجب أن تكون بين 0 و100.", Project360ErrorCodes.PercentInvalid)
            : null;

    /// <summary>الوزن الخام لا يُفرَض مجموعه، لكنّه لا يكون سالبًا.</summary>
    internal static (string Message, string Code)? ValidateWeight(decimal weight) =>
        weight < 0m
            ? ("الوزن لا يمكن أن يكون سالبًا.", Project360ErrorCodes.WeightInvalid)
            : null;

    /// <summary>ترتيب العرض تصاعديّ غير سالب.</summary>
    internal static (string Message, string Code)? ValidateSortOrder(int sortOrder) =>
        sortOrder < 0
            ? ("ترتيب العرض لا يمكن أن يكون سالبًا.", Project360ErrorCodes.SortOrderInvalid)
            : null;

    /// <summary>تاريخ الاستحقاق ليس قبل تاريخ البداية.</summary>
    internal static (string Message, string Code)? ValidateDateRange(DateOnly? startDate, DateOnly? dueDate) =>
        startDate is DateOnly s && dueDate is DateOnly d && d < s
            ? ("تاريخ الاستحقاق لا يمكن أن يكون قبل تاريخ البداية.", Project360ErrorCodes.DateRangeInvalid)
            : null;

    /// <summary>الاسم المعروض مطلوب.</summary>
    internal static (string Message, string Code)? ValidateName(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? ("الاسم مطلوب.", Project360ErrorCodes.NameRequired)
            : null;

    /// <summary>
    /// أوّل خطأ غير فارغ من سلسلة حرّاس — يجعل الخدمة تُرجِع أوّل سبب حقيقيّ بلا سلّم <c>if</c> طويل.
    /// </summary>
    internal static (string Message, string Code)? FirstError(params (string Message, string Code)?[] checks)
    {
        foreach (var check in checks)
            if (check is not null) return check;
        return null;
    }

    /// <summary>
    /// أسماء رموز كتالوج نشطة لمجال واحد — استعلام واحد لكلّ مجال مهما بلغ عدد الرموز (منع N+1).
    /// </summary>
    internal static async Task<Dictionary<string, string>> ResolveActiveCodeNamesAsync(
        AppDbContext db, string domain, IReadOnlyCollection<string> codes, CancellationToken ct)
    {
        if (codes.Count == 0) return new Dictionary<string, string>();
        return await db.ExecutionTaxonomyValues.AsNoTracking()
            .Where(v => v.IsActive && v.Domain == domain && codes.Contains(v.Code))
            .ToDictionaryAsync(v => v.Code, v => v.NameAr, ct);
    }
}
