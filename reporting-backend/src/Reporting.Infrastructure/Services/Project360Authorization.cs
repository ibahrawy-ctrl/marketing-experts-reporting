using Microsoft.EntityFrameworkCore;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Application.Projects360;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// بوّابة الرؤية والتخويل الموحّدة لـProject 360 (CPW-R3 · W4 · §2).
///
/// <para>
/// **صفر توسيع لنطاق الرؤية**: المصدر الوحيد للرؤية هو <see cref="IClientProjectAccess"/> القائم.
/// هذه الطبقة لا تضيف مشروعًا واحدًا إلى ما يراه المستخدم — بل تضيّق **الكتابة** فوقه.
/// </para>
///
/// <para>
/// **الترتيب مقصود**: مصادقة ⟵ رؤية ⟵ وجود. غير المصادَق يعود بـ<c>401</c>؛ أمّا **المشروع
/// غير الموجود** و**المشروع الموجود خارج نطاق المستخدم** فيعودان بعقد **واحد لا يُميَّز**:
/// نفس الرمز <c>project.not_found</c> ونفس الرسالة ونفس الشكل ⟹ <c>404</c>
/// (قرار المالك FINDING-W6-04). فارق الرسالة أو الرمز بين الحالتين كان سيسمح بتعداد المشاريع
/// خارج النطاق (IDOR/BOLA) حتّى مع منع الوصول إليها.
/// </para>
///
/// <para>
/// **ما يبقى 403**: من يرى المشروع فعلًا ولا يملك القدرة المطلوبة عليه — وهو تمييز مقصود
/// لأنّه لا يكشف وجود شيء لا يعرفه المستخدم أصلًا.
/// </para>
/// </summary>
public class Project360Authorization : IProject360Authorization
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClientProjectAccess _access;

    public Project360Authorization(AppDbContext db, ICurrentUser currentUser, IClientProjectAccess access)
    {
        _db = db;
        _currentUser = currentUser;
        _access = access;
    }

    public async Task<Result<Project360Context>> LoadVisibleProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null)
            return Result<Project360Context>.Failure("غير مصرّح.", "auth.unauthenticated");

        // خارج النطاق ⟹ **نفس** عقد «غير موجود» حرفيًّا (رمزًا ورسالةً وشكلًا)، ولا يُستعلَم عن
        // الوجود أصلًا كي لا يتسرّب الفارق عبر التوقيت أو عبر أيّ حقل في الاستجابة.
        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewProject(projectId))
            return Result<Project360Context>.Failure(ProjectNotFoundMessage, Project360ErrorCodes.ProjectNotFound);

        var project = await _db.Projects.AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new Project360Context(
                p.Id, p.ClientId, p.ServiceType, p.Status, p.StartDate, p.EndDate,
                p.ProgressPercent, p.TeamLeaderId, p.AccountManagerId, p.ProjectOwnerId))
            .FirstOrDefaultAsync(ct);

        return project is null
            ? Result<Project360Context>.Failure(ProjectNotFoundMessage, Project360ErrorCodes.ProjectNotFound)
            : Result<Project360Context>.Success(project);
    }

    /// <summary>
    /// **الرسالة العامّة الوحيدة** للحالتين غير المميَّزتين. ثابت واحد كي لا ينحرف نصّ إحداهما
    /// عن الأخرى بتعديل لاحق فيعود التمييز من حيث لا يُقصَد.
    /// </summary>
    internal const string ProjectNotFoundMessage = "المشروع غير موجود.";

    /// <summary>
    /// الكتابة البنيويّة داخل خطّة المشروع: أدوار الإدارة (<c>Admin/CEO/GeneralManager/Manager</c>)
    /// **أو مالك المشروع المسنَد على هذا المشروع بعينه** (P360-WF-R2 §3.2).
    /// قائد الفريق ومدير الحساب **لا** يُنشئان ولا يحذفان أهدافًا أو مؤشّرات أو مخرَجات.
    ///
    /// <para>
    /// مالك المشروع يملك «الخطّة البنيويّة» لمشروعه: الاستراتيجيّة والأهداف والمؤشّرات
    /// والمخرَجات التعاقديّة ومسارات العمل. وهذه صلاحيّة **بالمورد** لا بالدور: امتلاكه
    /// <c>ProjectOwnerId</c> على مشروع لا يمنحه شيئًا على أيّ مشروع آخر.
    /// </para>
    /// </summary>
    public Task<bool> CanManageProject360Async(Project360Context project, CancellationToken ct = default)
    {
        if (_currentUser.IsInAnyRole(Roles.ProjectPlanManagers)) return Task.FromResult(true);
        if (_currentUser.UserId is not Guid uid) return Task.FromResult(false);
        return Task.FromResult(project.ProjectOwnerId == uid);
    }

    /// <summary>
    /// الكتابة التشغيليّة (D-07 · موسَّعة في P360-WF-R2 §3): الإدارة أو **مالك المشروع** أو
    /// قائد الفريق المسؤول أو مدير الحساب المسؤول **عن هذا المشروع بعينه** — لا عن أيّ مشروع
    /// آخر ولو كان مرئيًّا له.
    ///
    /// <para>
    /// مالك المشروع مشمول هنا صراحةً وليس ضمنًا عبر <see cref="CanManageProject360Async"/>:
    /// كلّ قدرة تُفحَص عند نقطة استعمالها، فلو ضاقت البنيويّة لاحقًا بقيت التشغيليّة سليمة.
    /// </para>
    /// </summary>
    public Task<bool> CanUpdateProject360ProgressAsync(Project360Context project, CancellationToken ct = default)
    {
        if (_currentUser.IsInAnyRole(Roles.ProjectPlanManagers)) return Task.FromResult(true);
        if (_currentUser.UserId is not Guid uid) return Task.FromResult(false);
        return Task.FromResult(
            project.ProjectOwnerId == uid ||
            project.TeamLeaderId == uid ||
            project.AccountManagerId == uid);
    }

    /// <summary>
    /// خريطة القدرات = **استدعاء الحرّاس نفسها** لا إعادة كتابة شروطها. أيّ تضييق أو توسيع
    /// لاحق في <see cref="CanManageProject360Async"/> أو <see cref="CanUpdateProject360ProgressAsync"/>
    /// ينعكس هنا تلقائيًّا، فلا تنشأ حالة تعرض فيها الواجهة زرًّا يردّه الخادم.
    ///
    /// <para>
    /// <c>CanWriteGovernance</c> بالدور وحده (<c>Roles.GovernanceWorkspaceUsers</c>) لأنّ ورشة
    /// الحوكمة كيان مستقلّ عن ملكيّة المشروع؛ وهو مُقيَّد أصلًا برؤية المشروع لأنّ الخريطة
    /// لا تُبنى إلّا بعد <see cref="LoadVisibleProjectAsync"/>.
    /// </para>
    /// </summary>
    public async Task<ProjectCapabilitiesDto> BuildCapabilitiesAsync(
        Project360Context project, CancellationToken ct = default)
        => new(
            CanManageStructure: await CanManageProject360Async(project, ct),
            CanOperate: await CanUpdateProject360ProgressAsync(project, ct),
            CanWriteGovernance: _currentUser.IsInAnyRole(Roles.GovernanceWorkspaceUsers));
}
