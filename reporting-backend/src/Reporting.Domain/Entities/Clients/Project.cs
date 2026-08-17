using Reporting.Domain.Common;
using Reporting.Domain.Enums;

namespace Reporting.Domain.Entities.Clients;

/// <summary>
/// مشروع/خدمة يقدَّم لعميل (Phase 6). يربط مخرجات الفِرق بالعميل ونوع الخدمة.
/// </summary>
public class Project : BaseEntity
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string Name { get; set; } = string.Empty;
    public ServiceType ServiceType { get; set; } = ServiceType.Other;
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    /// <summary>الفريق المسؤول عن تنفيذ المشروع.</summary>
    public Guid? OwnerTeamId { get; set; }
    /// <summary>مدير الحساب المسؤول عن المشروع (مرجع مستخدم).</summary>
    public Guid? AccountManagerId { get; set; }

    public string? Notes { get; set; }

    // ===== CPW-R3 — Project 360 Foundation (§5-2) =====
    // أعمدة **إضافيّة بحتة**: كلّها إمّا Nullable أو ذات قيمة افتراضيّة ⟹ صفر Backfill وصفر صفّ يتغيّر عند الهجرة.
    // لم تُمَسّ أيّ خاصّيّة قائمة أعلاه.

    /// <summary>ملخّص تنفيذيّ مختصر للمشروع (سطر إلى فقرة).</summary>
    public string? Summary { get; set; }

    /// <summary>مالك المشروع (مرجع مستخدم بلا مفتاح أجنبيّ صلب — نفس نمط <see cref="AccountManagerId"/>).</summary>
    public Guid? ProjectOwnerId { get; set; }

    /// <summary>قائد الفريق المسؤول تشغيليًّا عن المشروع (D-07). مرجع مستخدم بلا مفتاح أجنبيّ صلب.</summary>
    public Guid? TeamLeaderId { get; set; }

    /// <summary>
    /// نسبة تنفيذ المشروع (0–100).
    ///
    /// <para>
    /// **تغيّر مصدرها في P360-WF-R2 (GAP-02/03)**: كانت «معلَنة يدويًّا» — والحقيقة أنّها لم تكن
    /// معلَنة إطلاقًا لأنّه لم يوجد لها مسار كتابة واحد، فبقيت صفرًا في كلّ صفوف الإنتاج وأبطلت
    /// 0.30 من معادلة الصحّة وجعلت الأخضر مستحيلًا رياضيًّا (GAP-04). صارت الآن **مشتقّة حتميًّا**
    /// من المخرَجات التعاقديّة النشطة بمتوسّط موزون، ويُعاد احتسابها عند كلّ حدث مؤثّر.
    /// </para>
    ///
    /// <para>لا تُكتب يدويًّا بعد اليوم: الرقم بلا مصدر معلن ثقةٌ في غير موضعها.</para>
    /// </summary>
    public decimal ProgressPercent { get; set; }

    /// <summary>
    /// **كيف** حُسِبت <see cref="ProgressPercent"/> (P360-WF-R2 §6-1). تُخزَّن مع النسبة لأنّ الرقم
    /// وحده لا يميّز «صفر لأنّ لا مخرَجات» عن «صفر لأنّ لم يبدأ العمل» عن «لم يُحتسَب بعد».
    /// </summary>
    public ProjectProgressMode ProgressMode { get; set; } = ProjectProgressMode.NotCalculated;

    /// <summary>ختم آخر احتساب للتقدّم — يكشف الرقم البائت. NULL ⟹ لم يُحتسَب قطّ.</summary>
    public DateTime? ProgressCalculatedAtUtc { get; set; }

    /// <summary>عدد المخرَجات النشطة التي دخلت الاحتساب فعلًا — يجعل النسبة قابلة للمراجعة لا مجرّد رقم.</summary>
    public int ProgressSourceDeliverableCount { get; set; }

    /// <summary>خلفيّة المشروع وسياق نشأته.</summary>
    public string? Background { get; set; }

    /// <summary>السياق التجاريّ للعميل الذي يخدمه المشروع.</summary>
    public string? BusinessContext { get; set; }

    /// <summary>
    /// نطاق العمل المتّفق عليه. **سُمّي `ScopeText` عمدًا** لأنّ كلمة `Scope` محجوزة دلاليًّا
    /// لنطاق الرؤية الأمنيّ في هذا النظام (ScopeResolver / ScopeContext) — منعًا لأيّ التباس.
    /// </summary>
    public string? ScopeText { get; set; }

    /// <summary>ما هو خارج نطاق العمل صراحةً.</summary>
    public string? OutOfScope { get; set; }

    /// <summary>تعريف النجاح المتّفق عليه مع العميل.</summary>
    public string? SuccessDefinition { get; set; }

    /// <summary>
    /// تصنيف الصحّة المخزَّن (§5-2). يُعاد كتابته **حتميًّا** من طبقة التطبيق عند كلّ حدث مؤثّر،
    /// ويُخزَّن — لا يُشتقّ وقت القراءة — لتمكين الفرز والفلترة على قوائم المشاريع بلا N+1.
    /// </summary>
    /// <remarks>
    /// **الافتراضيّ <see cref="ProjectHealthStatus.NotEvaluated"/> لا <c>Green</c>** (GAP-05):
    /// مشروع أُنشئ للتوّ لم يُقَس قطّ، وإعلانه سليمًا اطمئنانٌ على ما لم يُفحَص.
    /// </remarks>
    public ProjectHealthStatus HealthStatus { get; set; } = ProjectHealthStatus.NotEvaluated;

    /// <summary>نسبة الصحّة المخزَّنة (0–100) المطابقة لـ<see cref="HealthStatus"/>.</summary>
    public decimal HealthPercent { get; set; }

    /// <summary>ختم آخر احتساب للصحّة — يكشف البيانات البائتة. NULL ⟹ لم تُحتسَب بعد.</summary>
    public DateTime? HealthComputedAtUtc { get; set; }
}
