using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Application.Documents;
using Reporting.Domain.Entities.Clients;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// المقيّم المركزيّ لصلاحيّة المستندات (CPW-R2).
/// <para>
/// يُبنى السياق مرّة واحدة لكلّ طلب (استعلامان اثنان بحدّ أقصى للفريق/المشاريع)، ثمّ:
/// إمّا يُترجَم إلى شرط SQL عبر <see cref="VisibleFilter"/> في مسار القائمة،
/// أو يُقيَّم في الذاكرة عبر <see cref="Evaluate"/> في مسارات المستند الواحد/التنزيل.
/// </para>
/// </summary>
public class DocumentAccessEvaluator : IDocumentAccessEvaluator
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClientProjectAccess _access;

    public DocumentAccessEvaluator(AppDbContext db, ICurrentUser currentUser, IClientProjectAccess access)
    {
        _db = db;
        _currentUser = currentUser;
        _access = access;
    }

    public async Task<DocumentAccessContext> BuildContextAsync(Guid clientId, CancellationToken ct = default)
    {
        var uid = _currentUser.UserId;
        var roles = _currentUser.Roles.ToList();

        var isAdmin = _currentUser.IsInRole(Roles.Admin);
        var isManagement = _currentUser.IsInAnyRole(DocumentVisibilityPolicy.Management);
        var isFinance = _currentUser.IsInAnyRole(DocumentVisibilityPolicy.Finance);
        var isHrManagement = _currentUser.IsInAnyRole(DocumentVisibilityPolicy.HrManagement);

        var isProjectTeam = isManagement || await IsProjectTeamMemberAsync(clientId, uid, ct);
        var hasClientScope = await HasClientScopeAsync(clientId, uid, ct);

        return new DocumentAccessContext(
            uid, roles, isAdmin, isManagement, isFinance, isHrManagement, isProjectTeam, hasClientScope);
    }

    public DocumentAccess Evaluate(ClientDocument document, DocumentAccessContext context)
    {
        if (context.IsAdmin) return DocumentAccess.Granted;

        var allowed = document.VisibilityType switch
        {
            // صلاحيّة العميل وحدها كافية — لكنّ وصول المالية الوظيفيّ ليس صلاحيّة عميل.
            DocumentVisibilityType.ClientScoped => context.HasClientScopeAccess,
            DocumentVisibilityType.ManagementOnly => context.IsManagement,
            DocumentVisibilityType.ManagementAndFinance => context.IsManagement || context.IsFinance,
            DocumentVisibilityType.FinanceOnly => context.IsFinance,
            DocumentVisibilityType.HRManagementOnly => context.IsHrManagement,
            DocumentVisibilityType.ProjectTeam => context.IsProjectTeam,
            DocumentVisibilityType.CustomRoles => document.AllowedRoles
                .Any(r => context.RoleNames.Any(n => string.Equals(n, r.RoleName, StringComparison.OrdinalIgnoreCase))),
            DocumentVisibilityType.CustomUsers => context.UserId is Guid u && document.AllowedUsers.Any(a => a.UserId == u),
            _ => false
        };

        return allowed ? DocumentAccess.Granted : DocumentAccess.Denied;
    }

    public Expression<Func<ClientDocument, bool>> VisibleFilter(DocumentAccessContext context)
    {
        if (context.IsAdmin) return _ => true;

        // كلّ الأعلام قيم ثابتة مُلتقَطة ⇒ تُترجَم إلى ثوابت في SQL، والقائمتان المخصّصتان
        // تُترجَمان إلى EXISTS على جدولَي الصلاحيّات المفهرسَين (بلا تحميل في الذاكرة).
        var isManagement = context.IsManagement;
        var isFinance = context.IsFinance;
        var isHrManagement = context.IsHrManagement;
        var isProjectTeam = context.IsProjectTeam;
        var hasClientScope = context.HasClientScopeAccess;
        var roleNames = context.RoleNames.ToList();
        var userId = context.UserId ?? Guid.Empty;

        return d =>
            (hasClientScope && d.VisibilityType == DocumentVisibilityType.ClientScoped)
            || (isManagement && d.VisibilityType == DocumentVisibilityType.ManagementOnly)
            || ((isManagement || isFinance) && d.VisibilityType == DocumentVisibilityType.ManagementAndFinance)
            || (isFinance && d.VisibilityType == DocumentVisibilityType.FinanceOnly)
            || (isHrManagement && d.VisibilityType == DocumentVisibilityType.HRManagementOnly)
            || (isProjectTeam && d.VisibilityType == DocumentVisibilityType.ProjectTeam)
            || (d.VisibilityType == DocumentVisibilityType.CustomRoles
                && d.AllowedRoles.Any(r => roleNames.Contains(r.RoleName)))
            || (d.VisibilityType == DocumentVisibilityType.CustomUsers
                && d.AllowedUsers.Any(a => a.UserId == userId));
    }

    /// <summary>
    /// «صلاحيّة العميل» بالمعنى التنظيميّ: مدير العميل نفسه، أو أن يقع العميل داخل نطاق رؤية المستخدم
    /// (<see cref="IClientProjectAccess"/>) — وهي نفس القاعدة المطبَّقة في بوّابة قراءة العميل.
    /// <para>
    /// المالية والموارد البشريّة تصلان إلى <b>مستندات</b> أيّ عميل بحكم وظيفتهما على مستوى الشركة،
    /// لكنّهما لا تكتسبان هذه الصلاحيّة ⇒ لا تريان <c>ClientScoped</c>، بل ما تسمح به سياستهما فقط.
    /// </para>
    /// </summary>
    private async Task<bool> HasClientScopeAsync(Guid clientId, Guid? uid, CancellationToken ct)
    {
        if (uid is Guid me && await _db.Clients.AnyAsync(c => c.Id == clientId && c.AccountManagerId == me, ct))
            return true;

        var visibility = await _access.ResolveAsync(ct);
        return visibility.CanViewClient(clientId);
    }

    /// <summary>
    /// عضويّة فريق تنفيذ العميل: مدير العميل نفسه، أو مدير حساب أحد مشاريعه،
    /// أو عضو في الفريق المسؤول عن أحد مشاريعه (الفريق الأساسيّ أو عضويّة إضافيّة نشطة).
    /// </summary>
    private async Task<bool> IsProjectTeamMemberAsync(Guid clientId, Guid? uid, CancellationToken ct)
    {
        if (uid is not Guid me) return false;

        var isClientAccountManager = await _db.Clients
            .AnyAsync(c => c.Id == clientId && c.AccountManagerId == me, ct);
        if (isClientAccountManager) return true;

        var isProjectAccountManager = await _db.Projects
            .AnyAsync(p => p.ClientId == clientId && p.AccountManagerId == me, ct);
        if (isProjectAccountManager) return true;

        var teamIds = await _db.Projects
            .Where(p => p.ClientId == clientId && p.OwnerTeamId != null)
            .Select(p => p.OwnerTeamId!.Value)
            .Distinct()
            .ToListAsync(ct);
        if (teamIds.Count == 0) return false;

        var primaryTeamId = await _db.Users.Where(u => u.Id == me).Select(u => u.TeamId).FirstOrDefaultAsync(ct);
        if (primaryTeamId is Guid pt && teamIds.Contains(pt)) return true;

        return await _db.UserTeamMemberships
            .AnyAsync(m => m.UserId == me && m.IsActive && teamIds.Contains(m.TeamId), ct);
    }
}
