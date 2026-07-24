using Microsoft.AspNetCore.Identity;

namespace Reporting.Infrastructure.Identity;

/// <summary>
/// مستخدم النظام — يرث IdentityUser بمفتاح GUID (أفضل ممارسات ASP.NET Core Identity).
/// لا يوجد عمود Role هنا؛ الأدوار تُدار عبر جداول Identity.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // روابط تنظيمية (تُملأ في المراحل اللاحقة)
    public Guid? DepartmentId { get; set; }
    public Guid? TeamId { get; set; }
    public Guid? JobRoleId { get; set; }
    public Guid? ManagerId { get; set; }

    // تجاوز خطوة قائد الفريق في مسارات الاعتماد (Direct Reporting Override) — قاعدة عامة قابلة لإعادة
    // الاستخدام لأي موظّف يتبع مديره مباشرةً رغم بقائه ضمن فريق له قائد. القيمة الافتراضية false لجميع
    // المستخدمين؛ عند true تُتخطّى خطوة قائد الفريق (إجازة/استئذان/تقرير) ويبدأ المسار من المدير المباشر
    // النشط ثم fallback المعتمد (GM ← CEO/Admin). لا يمسّ الانتماء التشغيلي للفريق ولا مسار KPI.
    public bool BypassTeamLeaderApproval { get; set; }

    // تجاوز صريح لمعتمِد التقارير (ROLE-AWARE-PERSONAL-REPORT-SUBMISSION-ACCESS-R1) — مستقلّ تمامًا عن
    // ManagerId/TeamId والهيكل التنظيمي. NULL (الافتراضي) ⇒ يبقى مسار الاعتماد الحالي دون تغيير. عند
    // ضبطه لمستخدِم موجود ونشط (ليس صاحب التقرير) ⇒ يصبح هو المعتمِد المبدئي وCurrentApproverId مباشرةً
    // دون خطوة قائد فريق/مدير قبله. لا يغيّر نطاق الرؤية ولا الإجازات ولا لوحات المدير. FK إلى AspNetUsers
    // بسلوك Restrict (لا حذف تسلسلي) وفهرس مستقلّ.
    public Guid? ReportApproverOverrideUserId { get; set; }

    // تجاوز صريح لمراجِع KPI (ROLE-AWARE-PERSONAL-REPORT-SUBMISSION-ACCESS-R1) — حقل منفصل تمامًا عن تجاوز
    // اعتماد التقارير أعلاه وعن ManagerId. NULL (الافتراضي) ⇒ يبقى ResolveReviewerAsync الحالي دون تغيير.
    // عند ضبطه لمستخدِم موجود ونشط (ليس الـSubject ولا المقيِّم Evaluator) ⇒ يُستخدَم مراجعًا مباشرةً دون
    // خطوة قائد فريق/مدير قبله. لا يمسّ الرؤية ولا الهيكل. FK إلى AspNetUsers بسلوك Restrict وفهرس مستقلّ.
    public Guid? KpiReviewerOverrideUserId { get; set; }
}
