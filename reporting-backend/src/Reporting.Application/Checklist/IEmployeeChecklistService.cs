using Reporting.Application.Common;

namespace Reporting.Application.Checklist;

/// <summary>
/// P2-HR-010 — قائمة خدمة الموظّف والالتزام.
///
/// <para>المحسوب يُشتَقّ لحظيًّا من مصادره ولا يُخزَّن، واليدويّ وحده له صفّ.
/// الموظّف خارج نطاق المُشاهِد ⇒ فشل «غير موجود» (404 لا 403) بنفس شكل غير الموجود.</para>
/// </summary>
public interface IEmployeeChecklistService
{
    /// <summary>قائمة موظّف بعينه — بعد فرض النطاق وترشيح حسّاسيّة كلّ بند.</summary>
    Task<Result<EmployeeChecklistDto>> GetAsync(Guid subjectUserId, CancellationToken ct = default);

    /// <summary>قائمة المستخدم الحاليّ عن نفسه — المعرّف من التوكن حصرًا.</summary>
    Task<Result<EmployeeChecklistDto>> GetForSelfAsync(CancellationToken ct = default);

    /// <summary>
    /// تحديث بند **يدويّ**. مفتاح محسوب ⇒ 400 صريح لا كتابة صامتة.
    /// تعارض بصمة التزامن ⇒ 409 على مورد مرئيّ ومُصرَّح به.
    /// </summary>
    Task<Result<ChecklistItemDto>> UpdateManualItemAsync(
        Guid subjectUserId, string itemKey, UpdateChecklistItemCommand command, CancellationToken ct = default);
}
