using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Common;
using Reporting.Application.Directory;
using Reporting.Domain.Entities.Org;
using Reporting.Infrastructure.Identity;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

public class DirectoryService : IDirectoryService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IScopeResolver _scope;

    public DirectoryService(AppDbContext db, UserManager<ApplicationUser> users, IScopeResolver scope)
    {
        _db = db;
        _users = users;
        _scope = scope;
    }

    public async Task<IReadOnlyList<DirectoryUserDto>> ListUsersAsync(bool includeInactive, CancellationToken ct = default)
    {
        var scope = await _scope.ResolveAsync(ct);
        var usersQuery = _db.Users.AsNoTracking();
        if (!includeInactive) usersQuery = usersQuery.Where(u => u.IsActive);
        if (!scope.SeesAll)
        {
            var ids = scope.UserIds;
            usersQuery = usersQuery.Where(u => ids.Contains(u.Id));
        }
        var users = await usersQuery.OrderBy(u => u.FullName).ToListAsync(ct);

        // أدوار كل مستخدم عبر جداول Identity.
        var userRoles = await (
            from ur in _db.UserRoles
            join r in _db.Roles on ur.RoleId equals r.Id
            select new { ur.UserId, RoleName = r.Name! }).ToListAsync(ct);
        var rolesByUser = userRoles
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.RoleName).ToList());

        return users.Select(u => new DirectoryUserDto(
            u.Id,
            u.FullName,
            u.Email ?? string.Empty,
            u.IsActive,
            rolesByUser.GetValueOrDefault(u.Id) ?? new List<string>(),
            u.DepartmentId,
            u.TeamId,
            u.ManagerId)).ToList();
    }

    public async Task<IReadOnlyList<DepartmentDto>> ListDepartmentsAsync(CancellationToken ct = default)
    {
        var scope = await _scope.ResolveAsync(ct);
        var q = _db.Departments.AsNoTracking().AsQueryable();
        if (!scope.SeesAll)
        {
            var ids = scope.UserIds;
            // الإدارات التي يديرها مستخدم داخل النطاق، أو ينتمي إليها مستخدم داخل النطاق.
            var visibleDeptIds = await _db.Users
                .Where(u => ids.Contains(u.Id) && u.DepartmentId != null)
                .Select(u => u.DepartmentId!.Value).Distinct().ToListAsync(ct);
            q = q.Where(d => (d.ManagerId != null && ids.Contains(d.ManagerId.Value)) || visibleDeptIds.Contains(d.Id));
        }
        return await q.OrderBy(d => d.NameAr)
            .Select(d => new DepartmentDto(d.Id, d.NameAr, d.NameEn, d.Code, d.ManagerId, d.IsActive))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TeamDto>> ListTeamsAsync(CancellationToken ct = default)
    {
        var scope = await _scope.ResolveAsync(ct);
        var q = _db.Teams.AsNoTracking().AsQueryable();
        if (!scope.SeesAll)
        {
            var ids = scope.UserIds;
            // الفرق التي يقودها مستخدم داخل النطاق، أو ينتمي إليها مستخدم داخل النطاق.
            var visibleTeamIds = await _db.Users
                .Where(u => ids.Contains(u.Id) && u.TeamId != null)
                .Select(u => u.TeamId!.Value).Distinct().ToListAsync(ct);
            q = q.Where(t => (t.TeamLeaderId != null && ids.Contains(t.TeamLeaderId.Value)) || visibleTeamIds.Contains(t.Id));
        }
        return await q.OrderBy(t => t.NameAr)
            .Select(t => new TeamDto(t.Id, t.NameAr, t.NameEn, t.DepartmentId, t.TeamLeaderId, t.IsActive))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<JobRoleDto>> ListJobRolesAsync(CancellationToken ct = default)
        => await _db.JobRoles.AsNoTracking()
            .OrderBy(j => j.NameAr)
            .Select(j => new JobRoleDto(j.Id, j.NameAr, j.NameEn, j.Code, j.DepartmentId, j.IsActive))
            .ToListAsync(ct);

    public IReadOnlyList<RoleAccessDto> GetRoleMatrix()
        => Roles.All.Select(role =>
        {
            var single = new[] { role };
            var scopeType = RoleAccess.ScopeTypeFor(role);
            var perms = RoleAccess.PermissionsFor(single);
            return new RoleAccessDto(
                role,
                Roles.DisplayAr(role),
                scopeType,
                RoleAccess.ScopeDescriptionAr(scopeType),
                perms,
                perms.Select(RoleAccess.PermissionLabelAr).ToList());
        }).ToList();

    public async Task<Result> UpdateUserRolesAsync(Guid userId, IReadOnlyList<string> roles, Guid actingUserId, CancellationToken ct = default)
    {
        var desired = (roles ?? new List<string>())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct()
            .ToList();

        if (desired.Count == 0)
            return Result.Failure("يجب تعيين دور واحد على الأقل.", "user.roles.empty");

        var invalid = desired.Where(r => !Roles.All.Contains(r)).ToList();
        if (invalid.Count > 0)
            return Result.Failure($"أدوار غير معروفة: {string.Join(", ", invalid)}", "user.roles.invalid");

        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result.Failure("المستخدم غير موجود.", "user.not_found");

        var current = (await _users.GetRolesAsync(user)).ToList();

        // حاجز 1: لا يستطيع الأدمن إزالة دور الأدمن عن نفسه (منع قفل ذاتي).
        if (userId == actingUserId && current.Contains(Roles.Admin) && !desired.Contains(Roles.Admin))
            return Result.Failure("لا يمكنك إزالة دور مدير النظام عن نفسك.", "user.roles.self_lockout.conflict");

        // حاجز 2: لا يمكن إزالة آخر أدمن في النظام.
        if (current.Contains(Roles.Admin) && !desired.Contains(Roles.Admin))
        {
            var admins = await _users.GetUsersInRoleAsync(Roles.Admin);
            if (admins.Count <= 1)
                return Result.Failure("لا يمكن إزالة آخر مدير نظام في المنظومة.", "user.roles.last_admin.conflict");
        }

        var toAdd = desired.Except(current).ToList();
        var toRemove = current.Except(desired).ToList();

        if (toRemove.Count > 0)
        {
            var rr = await _users.RemoveFromRolesAsync(user, toRemove);
            if (!rr.Succeeded)
                return Result.Failure(string.Join("; ", rr.Errors.Select(e => e.Description)), "user.roles.update_failed.conflict");
        }
        if (toAdd.Count > 0)
        {
            var ar = await _users.AddToRolesAsync(user, toAdd);
            if (!ar.Succeeded)
                return Result.Failure(string.Join("; ", ar.Errors.Select(e => e.Description)), "user.roles.update_failed.conflict");
        }

        return Result.Success();
    }

    public async Task<Result<DirectoryUserDto>> CreateUserAsync(CreateUserRequest req, CancellationToken ct = default)
    {
        var email = (req.Email ?? string.Empty).Trim();
        var fullName = (req.FullName ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(email))
            return Result<DirectoryUserDto>.Failure("البريد الإلكتروني مطلوب.", "user.email.required");
        if (string.IsNullOrWhiteSpace(fullName))
            return Result<DirectoryUserDto>.Failure("الاسم الكامل مطلوب.", "user.name.required");
        if (string.IsNullOrWhiteSpace(req.Password))
            return Result<DirectoryUserDto>.Failure("كلمة المرور مطلوبة.", "user.password.required");

        var desired = (req.Roles ?? new List<string>())
            .Where(r => !string.IsNullOrWhiteSpace(r)).Distinct().ToList();
        if (desired.Count == 0)
            return Result<DirectoryUserDto>.Failure("يجب تعيين دور واحد على الأقل.", "user.roles.empty");
        var invalid = desired.Where(r => !Roles.All.Contains(r)).ToList();
        if (invalid.Count > 0)
            return Result<DirectoryUserDto>.Failure($"أدوار غير معروفة: {string.Join(", ", invalid)}", "user.roles.invalid");

        if (await _users.FindByEmailAsync(email) is not null)
            return Result<DirectoryUserDto>.Failure("هذا البريد الإلكتروني مستخدم بالفعل.", "user.email.duplicate.conflict");

        var orgErr = await ValidateOrgAsync(req.TeamId, req.DepartmentId, req.ManagerId, ct);
        if (orgErr is not null)
            return Result<DirectoryUserDto>.Failure(orgErr.Value.Error, orgErr.Value.Code);

        var deptId = await ResolveDepartmentAsync(req.TeamId, req.DepartmentId, ct);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            IsActive = true,
            DepartmentId = deptId,
            TeamId = req.TeamId,
            ManagerId = req.ManagerId,
        };

        var create = await _users.CreateAsync(user, req.Password);
        if (!create.Succeeded)
            return Result<DirectoryUserDto>.Failure(string.Join("; ", create.Errors.Select(e => e.Description)), "user.create_failed.conflict");

        var addRoles = await _users.AddToRolesAsync(user, desired);
        if (!addRoles.Succeeded)
        {
            await _users.DeleteAsync(user); // تراجع: لا نترك مستخدمًا بلا أدوار
            return Result<DirectoryUserDto>.Failure(string.Join("; ", addRoles.Errors.Select(e => e.Description)), "user.create_failed.conflict");
        }

        return Result<DirectoryUserDto>.Success(new DirectoryUserDto(
            user.Id, user.FullName, user.Email ?? string.Empty, user.IsActive,
            desired, user.DepartmentId, user.TeamId, user.ManagerId));
    }

    public async Task<Result<DirectoryUserDto>> UpdateUserAsync(Guid userId, UpdateUserRequest req, Guid actingUserId, CancellationToken ct = default)
    {
        var fullName = (req.FullName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(fullName))
            return Result<DirectoryUserDto>.Failure("الاسم الكامل مطلوب.", "user.name.required");

        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result<DirectoryUserDto>.Failure("المستخدم غير موجود.", "user.not_found");

        // حاجز: لا يستطيع الأدمن تعطيل نفسه (منع قفل ذاتي).
        if (userId == actingUserId && !req.IsActive)
            return Result<DirectoryUserDto>.Failure("لا يمكنك تعطيل حسابك بنفسك.", "user.self_deactivate.conflict");

        // منع تعطيل آخر أدمن نشط.
        if (!req.IsActive && user.IsActive && await _users.IsInRoleAsync(user, Roles.Admin))
        {
            var admins = await _users.GetUsersInRoleAsync(Roles.Admin);
            if (admins.Count(a => a.IsActive) <= 1)
                return Result<DirectoryUserDto>.Failure("لا يمكن تعطيل آخر مدير نظام نشط.", "user.last_admin.conflict");
        }

        if (req.ManagerId == userId)
            return Result<DirectoryUserDto>.Failure("لا يمكن أن يكون المستخدم مديرًا لنفسه.", "user.manager.self.conflict");

        var orgErr = await ValidateOrgAsync(req.TeamId, req.DepartmentId, req.ManagerId, ct);
        if (orgErr is not null)
            return Result<DirectoryUserDto>.Failure(orgErr.Value.Error, orgErr.Value.Code);

        user.FullName = fullName;
        user.IsActive = req.IsActive;
        user.TeamId = req.TeamId;
        user.DepartmentId = await ResolveDepartmentAsync(req.TeamId, req.DepartmentId, ct);
        user.ManagerId = req.ManagerId;

        var upd = await _users.UpdateAsync(user);
        if (!upd.Succeeded)
            return Result<DirectoryUserDto>.Failure(string.Join("; ", upd.Errors.Select(e => e.Description)), "user.update_failed.conflict");

        var roles = (await _users.GetRolesAsync(user)).ToList();
        return Result<DirectoryUserDto>.Success(new DirectoryUserDto(
            user.Id, user.FullName, user.Email ?? string.Empty, user.IsActive,
            roles, user.DepartmentId, user.TeamId, user.ManagerId));
    }

    public async Task<Result> DeleteUserAsync(Guid userId, Guid actingUserId, CancellationToken ct = default)
    {
        if (userId == actingUserId)
            return Result.Failure("لا يمكنك حذف حسابك بنفسك.", "user.delete_self.conflict");

        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result.Failure("المستخدم غير موجود.", "user.not_found");

        // منع حذف آخر أدمن في النظام.
        if (await _users.IsInRoleAsync(user, Roles.Admin))
        {
            var admins = await _users.GetUsersInRoleAsync(Roles.Admin);
            if (admins.Count <= 1)
                return Result.Failure("لا يمكن حذف آخر مدير نظام في المنظومة.", "user.last_admin.conflict");
        }

        // تنظيف المراجع: رموز التجديد + قيادة الفرق/الإدارات + علاقة الإدارة.
        await _db.RefreshTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);
        await _db.Teams.Where(t => t.TeamLeaderId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.TeamLeaderId, (Guid?)null), ct);
        await _db.Departments.Where(d => d.ManagerId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.ManagerId, (Guid?)null), ct);
        await _db.Users.Where(u => u.ManagerId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.ManagerId, (Guid?)null), ct);

        var del = await _users.DeleteAsync(user);
        if (!del.Succeeded)
            return Result.Failure(string.Join("; ", del.Errors.Select(e => e.Description)), "user.delete_failed.conflict");

        return Result.Success();
    }

    public async Task<Result> AddTeamMemberAsync(Guid teamId, Guid userId, CancellationToken ct = default)
    {
        var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team is null)
            return Result.Failure("الفريق غير موجود.", "team.not_found");

        // البند 2: إدارة الفرق متاحة للأدوار الإدارية ضمن نطاقها فقط (ScopeResolver خادميًّا).
        var scopeErr = await EnsureTeamInScopeAsync(teamId, ct);
        if (scopeErr is not null) return scopeErr;

        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result.Failure("المستخدم غير موجود.", "user.not_found");

        // لا يمكن ضمّ مستخدم خارج نطاق المُدير (منع توسيع النطاق عبر الإضافة).
        var scope = await _scope.ResolveAsync(ct);
        if (!scope.SeesAll && !scope.UserIds.Contains(userId))
            return Result.Failure("لا يمكنك إضافة مستخدم خارج نطاق صلاحيتك.", "auth.forbidden");

        user.TeamId = teamId;
        user.DepartmentId = team.DepartmentId;
        var upd = await _users.UpdateAsync(user);
        if (!upd.Succeeded)
            return Result.Failure(string.Join("; ", upd.Errors.Select(e => e.Description)), "team.member.add_failed.conflict");

        return Result.Success();
    }

    public async Task<Result> RemoveTeamMemberAsync(Guid teamId, Guid userId, CancellationToken ct = default)
    {
        // البند 2: لا يجوز إلا للأدوار الإدارية ضمن نطاق الفريق.
        var scopeErr = await EnsureTeamInScopeAsync(teamId, ct);
        if (scopeErr is not null) return scopeErr;

        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result.Failure("المستخدم غير موجود.", "user.not_found");
        if (user.TeamId != teamId)
            return Result.Failure("المستخدم ليس عضوًا في هذا الفريق.", "team.member.not_in_team.conflict");

        user.TeamId = null;
        var upd = await _users.UpdateAsync(user);
        if (!upd.Succeeded)
            return Result.Failure(string.Join("; ", upd.Errors.Select(e => e.Description)), "team.member.remove_failed.conflict");

        return Result.Success();
    }

    /// <summary>
    /// البند 2 — حارس نطاق إدارة الفرق: يسمح للأدوار التي ترى كل المنظومة (Admin/CEO/GM)،
    /// وإلا يشترط أن يكون الفريق ضمن نطاق المُدير (يقوده مستخدم داخل النطاق أو يضمّ عضوًا داخل النطاق).
    /// خارج النطاق ⇒ auth.forbidden (403).
    /// </summary>
    private async Task<Result?> EnsureTeamInScopeAsync(Guid teamId, CancellationToken ct)
    {
        var scope = await _scope.ResolveAsync(ct);
        if (scope.SeesAll) return null;

        var ids = scope.UserIds;
        var leaderInScope = await _db.Teams
            .AnyAsync(t => t.Id == teamId && t.TeamLeaderId != null && ids.Contains(t.TeamLeaderId.Value), ct);
        var memberInScope = await _db.Users
            .AnyAsync(u => u.TeamId == teamId && ids.Contains(u.Id), ct);

        return leaderInScope || memberInScope
            ? null
            : Result.Failure("هذا الفريق خارج نطاق صلاحيتك.", "auth.forbidden");
    }

    public async Task<Result<TeamDto>> CreateTeamAsync(CreateTeamRequest req, CancellationToken ct = default)
    {
        var nameAr = (req.NameAr ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(nameAr))
            return Result<TeamDto>.Failure("اسم الفريق مطلوب.", "team.name.required");

        if (!await _db.Departments.AnyAsync(d => d.Id == req.DepartmentId, ct))
            return Result<TeamDto>.Failure("الإدارة المحدّدة غير موجودة.", "department.not_found");

        if (req.TeamLeaderId is Guid lid && !await _db.Users.AnyAsync(u => u.Id == lid, ct))
            return Result<TeamDto>.Failure("قائد الفريق المحدّد غير موجود.", "team.leader.not_found");

        var team = new Team
        {
            NameAr = nameAr,
            NameEn = string.IsNullOrWhiteSpace(req.NameEn) ? null : req.NameEn!.Trim(),
            DepartmentId = req.DepartmentId,
            TeamLeaderId = req.TeamLeaderId,
            IsActive = true,
        };
        _db.Teams.Add(team);
        await _db.SaveChangesAsync(ct);

        return Result<TeamDto>.Success(new TeamDto(team.Id, team.NameAr, team.NameEn, team.DepartmentId, team.TeamLeaderId, team.IsActive));
    }

    public async Task<Result<TeamDto>> UpdateTeamAsync(Guid teamId, UpdateTeamRequest req, CancellationToken ct = default)
    {
        var nameAr = (req.NameAr ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(nameAr))
            return Result<TeamDto>.Failure("اسم الفريق مطلوب.", "team.name.required");

        var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team is null)
            return Result<TeamDto>.Failure("الفريق غير موجود.", "team.not_found");

        // البند 2: تعديل بيانات الفريق متاح للأدوار الإدارية ضمن نطاقها فقط (ScopeResolver خادميًّا).
        var scopeErr = await EnsureTeamInScopeAsync(teamId, ct);
        if (scopeErr is not null) return Result<TeamDto>.Failure(scopeErr.Error!, scopeErr.ErrorCode!);

        if (!await _db.Departments.AnyAsync(d => d.Id == req.DepartmentId, ct))
            return Result<TeamDto>.Failure("الإدارة المحدّدة غير موجودة.", "department.not_found");

        if (req.TeamLeaderId is Guid lid && !await _db.Users.AnyAsync(u => u.Id == lid, ct))
            return Result<TeamDto>.Failure("قائد الفريق المحدّد غير موجود.", "team.leader.not_found");

        team.NameAr = nameAr;
        team.NameEn = string.IsNullOrWhiteSpace(req.NameEn) ? null : req.NameEn!.Trim();
        team.DepartmentId = req.DepartmentId;
        team.TeamLeaderId = req.TeamLeaderId;
        team.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);

        return Result<TeamDto>.Success(new TeamDto(team.Id, team.NameAr, team.NameEn, team.DepartmentId, team.TeamLeaderId, team.IsActive));
    }

    public async Task<Result> DeleteTeamAsync(Guid teamId, CancellationToken ct = default)
    {
        var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team is null)
            return Result.Failure("الفريق غير موجود.", "team.not_found");

        // تنظيف المراجع: تفريغ ربط الأعضاء بالفريق قبل الحذف.
        await _db.Users.Where(u => u.TeamId == teamId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.TeamId, (Guid?)null), ct);

        _db.Teams.Remove(team);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<DepartmentDto>> CreateDepartmentAsync(CreateDepartmentRequest req, CancellationToken ct = default)
    {
        var nameAr = (req.NameAr ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(nameAr))
            return Result<DepartmentDto>.Failure("اسم الإدارة مطلوب.", "department.name.required");

        if (req.ManagerId is Guid mid && !await _db.Users.AnyAsync(u => u.Id == mid, ct))
            return Result<DepartmentDto>.Failure("المدير المحدّد غير موجود.", "user.manager.not_found");

        var dept = new Department
        {
            NameAr = nameAr,
            NameEn = string.IsNullOrWhiteSpace(req.NameEn) ? null : req.NameEn!.Trim(),
            Code = string.IsNullOrWhiteSpace(req.Code) ? null : req.Code!.Trim(),
            ManagerId = req.ManagerId,
            IsActive = true,
        };
        _db.Departments.Add(dept);
        await _db.SaveChangesAsync(ct);

        return Result<DepartmentDto>.Success(new DepartmentDto(dept.Id, dept.NameAr, dept.NameEn, dept.Code, dept.ManagerId, dept.IsActive));
    }

    public async Task<Result<DepartmentDto>> UpdateDepartmentAsync(Guid departmentId, UpdateDepartmentRequest req, CancellationToken ct = default)
    {
        var nameAr = (req.NameAr ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(nameAr))
            return Result<DepartmentDto>.Failure("اسم الإدارة مطلوب.", "department.name.required");

        var dept = await _db.Departments.FirstOrDefaultAsync(d => d.Id == departmentId, ct);
        if (dept is null)
            return Result<DepartmentDto>.Failure("الإدارة غير موجودة.", "department.not_found");

        if (req.ManagerId is Guid mid && !await _db.Users.AnyAsync(u => u.Id == mid, ct))
            return Result<DepartmentDto>.Failure("المدير المحدّد غير موجود.", "user.manager.not_found");

        dept.NameAr = nameAr;
        dept.NameEn = string.IsNullOrWhiteSpace(req.NameEn) ? null : req.NameEn!.Trim();
        dept.Code = string.IsNullOrWhiteSpace(req.Code) ? null : req.Code!.Trim();
        dept.ManagerId = req.ManagerId;
        dept.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);

        return Result<DepartmentDto>.Success(new DepartmentDto(dept.Id, dept.NameAr, dept.NameEn, dept.Code, dept.ManagerId, dept.IsActive));
    }

    public async Task<Result> DeleteDepartmentAsync(Guid departmentId, CancellationToken ct = default)
    {
        var dept = await _db.Departments.FirstOrDefaultAsync(d => d.Id == departmentId, ct);
        if (dept is null)
            return Result.Failure("الإدارة غير موجودة.", "department.not_found");

        // منع حذف إدارة بها فرق (يجب نقل/حذف الفرق أولًا للحفاظ على التكامل).
        if (await _db.Teams.AnyAsync(t => t.DepartmentId == departmentId, ct))
            return Result.Failure("لا يمكن حذف إدارة بها فرق. انقل الفرق أو احذفها أولًا.", "department.has_teams.conflict");

        // تنظيف المراجع: تفريغ ربط المستخدمين بالإدارة قبل الحذف.
        await _db.Users.Where(u => u.DepartmentId == departmentId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.DepartmentId, (Guid?)null), ct);

        _db.Departments.Remove(dept);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    // يتحقق من وجود الفريق/الإدارة/المدير المُشار إليها (إن وُجدت).
    private async Task<(string Error, string Code)?> ValidateOrgAsync(Guid? teamId, Guid? departmentId, Guid? managerId, CancellationToken ct)
    {
        if (teamId is Guid tid && !await _db.Teams.AnyAsync(t => t.Id == tid, ct))
            return ("الفريق المحدّد غير موجود.", "team.not_found");
        if (departmentId is Guid did && !await _db.Departments.AnyAsync(d => d.Id == did, ct))
            return ("الإدارة المحدّدة غير موجودة.", "department.not_found");
        if (managerId is Guid mid && !await _db.Users.AnyAsync(u => u.Id == mid, ct))
            return ("المدير المحدّد غير موجود.", "user.manager.not_found");
        return null;
    }

    // إذا أُسند فريق دون إدارة، تُشتق الإدارة من إدارة الفريق.
    private async Task<Guid?> ResolveDepartmentAsync(Guid? teamId, Guid? departmentId, CancellationToken ct)
    {
        if (departmentId is not null) return departmentId;
        if (teamId is Guid tid)
            return await _db.Teams.Where(t => t.Id == tid).Select(t => (Guid?)t.DepartmentId).FirstOrDefaultAsync(ct);
        return null;
    }
}
