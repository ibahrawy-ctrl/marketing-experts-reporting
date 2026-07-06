using Microsoft.EntityFrameworkCore;
using Reporting.Application.Common;
using Reporting.Application.Governance;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// التطبيق الموحّد لدليل الحوكمة (GOV-DIRECTORY-SCOPE-FIX-R1): مصدر وحيد لقوائم اختيار المستخدمين/الإدارات/الفِرق
/// في ورشة الحوكمة وإجراءات الحوكمة والتصعيدات. يوحّد سياسة الحسّاسية ويطبّق ScopeResolver للقراءة فقط.
///
/// القواعد:
/// <list type="bullet">
/// <item>الرؤية الواسعة (Admin/CEO/GM/CeoSupport): كل المستخدمين النشطين شاملًا الحسّاسين + كل الإدارات + كل الفِرق (لكل الأغراض).</item>
/// <item><b>EscalationTarget</b> لغير أصحاب الرؤية الواسعة: كل النشطين عدا الحسّاسين + كل الإدارات + كل الفِرق
///   (يحافظ على دلالة الرفع المتقاطع دون توسيع الرؤية الفعلية على التصعيدات).</item>
/// <item><b>Workspace / ActionItemAssignee</b> لغير أصحاب الرؤية الواسعة: نطاق الملكية فقط —
///   HR: مستخدمو إدارته (عدا الحسّاسين) + نفسه؛ Manager/TeamLeader/Employee: نطاق ScopeResolver (عدا الحسّاسين) + نفسه؛
///   والإدارات/الفِرق تُشتقّ من مجموعة المستخدمين الناتجة + إدارة/فريق المستخدم الحالي (بلا تسريب كامل الهيكل).</item>
/// </list>
/// لا يكشف أيّ بند/إجراء/تصعيد — مراجع اختيار فقط.
/// </summary>
public class GovernanceDirectoryService : IGovernanceDirectoryService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IScopeResolver _scope;

    public GovernanceDirectoryService(AppDbContext db, ICurrentUser currentUser, IScopeResolver scope)
    {
        _db = db;
        _currentUser = currentUser;
        _scope = scope;
    }

    // سياسة الحسّاسية الموحّدة: Admin/CEO/GM/CeoSupport. (HR ليس حسّاسًا.)
    private static readonly string[] SensitiveAccountRoles = { Roles.Admin, Roles.Ceo, Roles.GeneralManager, Roles.CeoSupport };

    // الرؤية الواسعة الموحّدة للحوكمة (مطابقة لمجموعات الـWideViewers في الوحدات الثلاث).
    private bool IsWideViewer => _currentUser.IsInAnyRole(Roles.Admin, Roles.Ceo, Roles.GeneralManager, Roles.CeoSupport);
    private bool IsHr => _currentUser.IsInRole(Roles.Hr);

    public async Task<Result<GovernanceDirectoryDto>> GetDirectoryAsync(GovernanceDirectoryPurpose purpose, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<GovernanceDirectoryDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var sensitive = await SensitiveUserIdsAsync(ct);

        // الرؤية الواسعة: الكلّ شاملًا الحسّاسين + كل الإدارات + كل الفِرق.
        if (IsWideViewer)
            return Result<GovernanceDirectoryDto>.Success(await BuildFullDirectoryAsync(includeSensitive: true, sensitive, ct));

        // التصعيد المتقاطع: كل النشطين عدا الحسّاسين + كل الإدارات + كل الفِرق (دلالة الرفع المتقاطع).
        if (purpose == GovernanceDirectoryPurpose.EscalationTarget)
            return Result<GovernanceDirectoryDto>.Success(await BuildFullDirectoryAsync(includeSensitive: false, sensitive, ct));

        // Workspace / ActionItemAssignee لغير أصحاب الرؤية الواسعة: نطاق الملكية فقط.
        var scopedUserIds = await ResolveScopedUserIdsAsync(uid, ct);
        scopedUserIds.Add(uid); // المستخدم الحالي دائمًا ضمن نطاقه (الإسناد للذات).
        scopedUserIds.ExceptWith(sensitive);

        var users = await _db.Users.AsNoTracking()
            .Where(u => u.IsActive && scopedUserIds.Contains(u.Id))
            .OrderBy(u => u.FullName)
            .Select(u => new GovernanceDirectoryUserDto(u.Id, u.FullName, u.DepartmentId, u.TeamId))
            .ToListAsync(ct);

        // الإدارات/الفِرق تُشتقّ من المستخدمين الناتجين + إدارة/فريق المستخدم الحالي (بلا تسريب كامل الهيكل).
        var (ownDept, ownTeam) = await _db.Users.Where(u => u.Id == uid)
            .Select(u => new ValueTuple<Guid?, Guid?>(u.DepartmentId, u.TeamId))
            .FirstOrDefaultAsync(ct);

        var deptIds = users.Where(u => u.DepartmentId is not null).Select(u => u.DepartmentId!.Value).ToHashSet();
        if (ownDept is Guid od) deptIds.Add(od);
        var teamIds = users.Where(u => u.TeamId is not null).Select(u => u.TeamId!.Value).ToHashSet();
        if (ownTeam is Guid ot) teamIds.Add(ot);

        var departments = await _db.Departments.AsNoTracking()
            .Where(d => deptIds.Contains(d.Id))
            .OrderBy(d => d.NameAr)
            .Select(d => new GovernanceDirectoryDepartmentDto(d.Id, d.NameAr))
            .ToListAsync(ct);

        var teams = await _db.Teams.AsNoTracking()
            .Where(t => teamIds.Contains(t.Id))
            .OrderBy(t => t.NameAr)
            .Select(t => new GovernanceDirectoryTeamDto(t.Id, t.NameAr, t.DepartmentId))
            .ToListAsync(ct);

        return Result<GovernanceDirectoryDto>.Success(new GovernanceDirectoryDto(users, departments, teams));
    }

    /// <summary>قائمة كاملة على مستوى الشركة (تُستخدَم للرؤية الواسعة وللتصعيد المتقاطع).</summary>
    private async Task<GovernanceDirectoryDto> BuildFullDirectoryAsync(bool includeSensitive, HashSet<Guid> sensitive, CancellationToken ct)
    {
        var rows = await _db.Users.AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.FullName, u.DepartmentId, u.TeamId })
            .ToListAsync(ct);
        var users = rows
            .Where(u => includeSensitive || !sensitive.Contains(u.Id))
            .Select(u => new GovernanceDirectoryUserDto(u.Id, u.FullName, u.DepartmentId, u.TeamId))
            .ToList();

        var departments = await _db.Departments.AsNoTracking()
            .OrderBy(d => d.NameAr)
            .Select(d => new GovernanceDirectoryDepartmentDto(d.Id, d.NameAr))
            .ToListAsync(ct);

        var teams = await _db.Teams.AsNoTracking()
            .OrderBy(t => t.NameAr)
            .Select(t => new GovernanceDirectoryTeamDto(t.Id, t.NameAr, t.DepartmentId))
            .ToListAsync(ct);

        return new GovernanceDirectoryDto(users, departments, teams);
    }

    /// <summary>
    /// مجموعة معرّفات المستخدمين ضمن نطاق المستخدم الحالي (لأغراض Workspace/ActionItemAssignee، غير الواسع):
    /// HR = مستخدمو إدارته؛ غيره = نطاق ScopeResolver (إن كان SeesAll ⇒ كل النشطين).
    /// </summary>
    private async Task<HashSet<Guid>> ResolveScopedUserIdsAsync(Guid uid, CancellationToken ct)
    {
        if (IsHr)
        {
            var ownDept = await _db.Users.Where(u => u.Id == uid).Select(u => u.DepartmentId).FirstOrDefaultAsync(ct);
            if (ownDept is not Guid hd)
                return new HashSet<Guid>();
            return (await _db.Users.Where(u => u.IsActive && u.DepartmentId == hd).Select(u => u.Id).ToListAsync(ct)).ToHashSet();
        }

        var scope = await _scope.ResolveAsync(ct);
        if (scope.SeesAll)
            return (await _db.Users.Where(u => u.IsActive).Select(u => u.Id).ToListAsync(ct)).ToHashSet();
        return scope.UserIds.ToHashSet();
    }

    private async Task<HashSet<Guid>> SensitiveUserIdsAsync(CancellationToken ct) =>
        (await (from ur in _db.UserRoles
                join r in _db.Roles on ur.RoleId equals r.Id
                where SensitiveAccountRoles.Contains(r.Name!)
                select ur.UserId).Distinct().ToListAsync(ct)).ToHashSet();
}
