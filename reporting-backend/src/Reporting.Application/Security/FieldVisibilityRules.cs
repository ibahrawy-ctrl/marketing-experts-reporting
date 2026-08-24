using Reporting.Application.Common;

namespace Reporting.Application.Security;

/// <summary>
/// سياق الرؤية المحسوب خادميًّا (P2-SEC-001). كلّ حقوله تُشتَقّ من التوكن وشجرة الإدارة،
/// ولا يُقبَل أيّ منها من العميل.
/// </summary>
/// <param name="ViewerUserId">المستخدم المُشاهِد.</param>
/// <param name="SubjectUserId">صاحب الملفّ/السجلّ المطلوب.</param>
/// <param name="Roles">أدوار Identity للمُشاهِد.</param>
/// <param name="Relation">علاقة المُشاهِد بالموضوع كما حسبها <c>IScopeResolver</c>.</param>
/// <param name="Permissions">مفاتيح <see cref="AppPermissions"/> الممنوحة صراحةً لهذا المستخدم.</param>
/// <param name="Purpose">سياق الاستدعاء (اسم النقطة/الغرض) — يُستعمل للتدقيق ولقواعد الغرض عند الحاجة.</param>
public sealed record FieldVisibilityContext(
    Guid ViewerUserId,
    Guid SubjectUserId,
    IReadOnlyCollection<string> Roles,
    SubjectRelation Relation,
    IReadOnlyCollection<string> Permissions,
    string? Purpose = null)
{
    private HashSet<string>? _roleSet;
    private HashSet<string>? _permSet;

    private HashSet<string> RoleSet => _roleSet ??= new HashSet<string>(Roles, StringComparer.Ordinal);
    private HashSet<string> PermSet => _permSet ??= new HashSet<string>(Permissions, StringComparer.Ordinal);

    public bool HasRole(string role) => RoleSet.Contains(role);
    public bool HasAnyRole(params string[] roles) => roles.Any(RoleSet.Contains);

    /// <summary>هل مُنِح المفتاح صراحةً؟ لا دور يمنح هذه المفاتيح ضمنًا — ولا حتّى Admin.</summary>
    public bool HasPermission(string key) => PermSet.Contains(key);

    public bool InScope => Relation != SubjectRelation.None;
    public bool IsSelf => Relation == SubjectRelation.Self;
}

/// <summary>
/// مصفوفة الرؤية النقيّة (بلا وصول لقاعدة بيانات) — قابلة للاختبار الوحدويّ مباشرةً.
/// <para>
/// مبدآن حاكمان:
/// (1) <b>تعدّد الأدوار = اتّحاد ما مُنِح صراحةً</b> لا فتح شامل ⇒ القرار = OR على منح كلّ دور على حدة.
/// (2) <b>التصنيفات الحسّاسة لا يمنحها دور إطلاقًا</b> (HrOnly / ManagementConfidential / Financial / Medical)
/// بل مفتاح <see cref="AppPermissions"/> صريح فقط ⇒ Admin وManager وTeamLeader لا يرونها تلقائيًّا.
/// </para>
/// </summary>
public static class FieldVisibilityRules
{
    /// <summary>هل يرى المُشاهِد حقلًا بهذا التصنيف على هذا الموضوع؟</summary>
    public static bool CanSee(FieldVisibilityContext ctx, FieldSensitivity sensitivity)
    {
        // خارج النطاق ⇒ لا شيء إطلاقًا (نقطة النهاية تعيد 404 لا 403).
        if (!ctx.InScope) return false;

        return sensitivity switch
        {
            FieldSensitivity.PublicOperational => true,
            FieldSensitivity.SharedWithEmployee => CanSeeSharedWithEmployee(ctx),
            FieldSensitivity.Internal => CanSeeInternal(ctx),

            // ===== التصنيفات الحسّاسة: مفتاح صريح فقط =====
            FieldSensitivity.HrOnly => ctx.HasPermission(AppPermissions.HrSensitiveRead),
            FieldSensitivity.ManagementConfidential => ctx.HasPermission(AppPermissions.ManagementConfidentialRead),
            FieldSensitivity.FinancialSensitive => ctx.HasPermission(AppPermissions.FinancialSensitiveRead),
            FieldSensitivity.MedicalSensitive => ctx.HasPermission(AppPermissions.MedicalSensitiveRead),

            _ => false
        };
    }

    /// <summary>
    /// «مشترَك مع الموظّف»: صاحب الملفّ نفسه، ومن يشرف عليه تشغيليًّا (قائد فريق/مدير) داخل نطاقه، وHR.
    /// التنفيذيّون وAdmin **لا** يرون هذا التصنيف تلقائيًّا.
    /// </summary>
    private static bool CanSeeSharedWithEmployee(FieldVisibilityContext ctx)
    {
        if (ctx.IsSelf) return true;
        if (ctx.HasRole(Roles.Hr)) return true;
        return ctx.HasAnyRole(Roles.TeamLeader, Roles.Manager) && IsSupervisoryRelation(ctx.Relation);
    }

    /// <summary>
    /// «داخليّ»: إشراف تشغيليّ + HR. الموظّف نفسه **لا** يراه (هذا ما يميّزه عن «مشترَك معه»)،
    /// والتنفيذيّ وAdmin لا يريانه تلقائيًّا.
    /// </summary>
    private static bool CanSeeInternal(FieldVisibilityContext ctx)
    {
        if (ctx.IsSelf) return false;
        if (ctx.HasRole(Roles.Hr)) return true;
        return ctx.HasAnyRole(Roles.TeamLeader, Roles.Manager) && IsSupervisoryRelation(ctx.Relation);
    }

    private static bool IsSupervisoryRelation(SubjectRelation relation) =>
        relation is SubjectRelation.DirectTeam or SubjectRelation.Department or SubjectRelation.Company;

    /// <summary>
    /// هل يظهر القسم أصلًا في استجابة Employee 360؟ القسم غير المصرّح **يغيب من الـJSON**
    /// (لا يُرسَل فارغًا ولا <c>null</c>) كي لا يُسرَّب وجوده.
    /// </summary>
    public static bool CanSeeSection(FieldVisibilityContext ctx, Employee360Section section)
    {
        if (!ctx.InScope) return false;

        // الهويّة التشغيليّة: كلّ من هو داخل النطاق، بما فيه Admin (إدارة الهويّة والنظام).
        if (section == Employee360Section.Identity) return true;

        // Admin لا يكتسب أيّ رؤية موارد بشريّة/أداء/حوكمة تلقائيًّا — الهويّة فقط.
        var adminOnlyViewer = ctx.HasRole(Roles.Admin)
                              && !ctx.HasAnyRole(Roles.Hr, Roles.Manager, Roles.TeamLeader, Roles.Ceo,
                                                 Roles.GeneralManager, Roles.CeoSupport, Roles.Employee)
                              && !ctx.IsSelf;
        if (adminOnlyViewer) return false;

        var self = ctx.IsSelf;
        var hr = ctx.HasRole(Roles.Hr);
        var supervisor = ctx.HasAnyRole(Roles.TeamLeader, Roles.Manager) && IsSupervisoryRelation(ctx.Relation);
        var executive = ctx.HasAnyRole(Roles.Ceo, Roles.GeneralManager, Roles.CeoSupport);

        return section switch
        {
            Employee360Section.OperationalSummary => self || hr || supervisor || executive,
            Employee360Section.Reports => self || hr || supervisor || executive,
            Employee360Section.Kpi => self || hr || supervisor || executive,

            // الإجازات/الطلبات/الأرصدة: تشغيليّ للمشرف وHR وصاحب الملفّ. التنفيذيّ لا يراها (§7).
            Employee360Section.LeaveAndPermissions => self || hr || supervisor,
            Employee360Section.RequestsAndBalances => self || hr || supervisor,

            Employee360Section.AttendanceAndCompliance => self || hr || supervisor || executive,

            // الملاحظات: الظهور مشروط بوجود ما يُرى بعد ترشيح الحسّاسيّة (تفحصه الخدمة أيضًا).
            Employee360Section.Notes => self || hr || supervisor,

            // الحوكمة: صاحب الملفّ يرى المرتبط به، والمشرف/HR/التنفيذيّ حسب التصريح.
            Employee360Section.Governance => self || hr || supervisor || executive,

            Employee360Section.DevelopmentAndTraining => self || hr || supervisor,

            // الخطّ الزمنيّ = اتّحاد أحداث الأقسام المرئيّة ⇒ يظهر لمن يرى قسمًا واحدًا على الأقلّ.
            Employee360Section.Timeline => self || hr || supervisor || executive,

            _ => false
        };
    }

    /// <summary>هل هذا التصنيف يستوجب أثرًا تدقيقيًّا عند الوصول؟ (بلا تسجيل قيمة الحقل نفسه).</summary>
    public static bool IsAuditable(FieldSensitivity sensitivity) =>
        sensitivity is FieldSensitivity.HrOnly
            or FieldSensitivity.ManagementConfidential
            or FieldSensitivity.FinancialSensitive
            or FieldSensitivity.MedicalSensitive;
}
