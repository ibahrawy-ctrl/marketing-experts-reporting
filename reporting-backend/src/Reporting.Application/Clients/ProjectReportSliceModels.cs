using Reporting.Domain.Enums;

namespace Reporting.Application.Clients;

// ======================================================================
// شريحة المشروع من تسليم تقرير (PROJECT360-PROJECT-SCOPED-REPORT-NAVIGATION-FIX-R1)
//
// **المشكلة التي تعالجها هذه النماذج**: تقرير الموظّف الأسبوعيّ الواحد قد يحمل عمل
// عدّة مشروعات في حقل قسم متكرّر واحد، وكلّ عنصر فيه يحمل `projectId` صريحًا. فتح
// التسليم كاملًا من داخل مشروع معيّن كان يعرض عمل المشروعات الأخرى.
//
// **قرار التصميم**: الشريحة تُبنى **في الخادم** ولا تُرسَل الحمولة الكاملة إطلاقًا.
// ما لا يحمل `projectId` مطابقًا لا يغادر الخادم أصلًا — لا إخفاء في الواجهة.
//
// **لماذا لا يوجد عدّاد إجماليّ للعناصر**: أيّ رقم يكشف كم عنصرًا يخصّ مشروعات
// أخرى هو تسريب بحدّ ذاته، فلا يُعاد سوى ما يخصّ هذا المشروع.
// ======================================================================

/// <summary>
/// حقل قسم متكرّر بعد التصفية: تعريف الحقل (للعرض) + **عناصر هذا المشروع فقط**.
/// <para><c>ConfigJson</c> وصفٌ للقالب لا بيانات تشغيليّة، فهو محايد بين المشروعات
/// وتحتاجه الواجهة لتبني عناوين الحقول الفرعيّة بنفس منطق العرض القائم.</para>
/// </summary>
public sealed record ProjectReportSliceFieldDto(
    Guid TemplateFieldId,
    string Label,
    string? ConfigJson,
    int Order,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Entries);

/// <summary>
/// مساهمة تسليم واحد في مشروع واحد — البيانات الوصفيّة اللازمة فقط + الشريحة.
/// </summary>
public sealed record ProjectReportSliceDto(
    Guid SubmissionId,
    Guid ProjectId,
    string ProjectName,
    Guid ClientId,
    string ClientName,
    Guid SubmitterId,
    string? SubmitterName,
    string? TemplateTitle,
    PeriodType PeriodType,
    string PeriodKey,
    SubmissionStatus Status,
    DateTime? SubmittedAtUtc,
    IReadOnlyList<ProjectReportSliceFieldDto> Fields);
