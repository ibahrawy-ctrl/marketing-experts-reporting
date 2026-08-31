using System.Runtime.ExceptionServices;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.Directory;
using Reporting.Domain.Entities.Org;
using Reporting.Application.Security;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Identity;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

public class DirectoryService : IDirectoryService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IScopeResolver _scope;
    private readonly IAuditService _audit;

    public DirectoryService(AppDbContext db, UserManager<ApplicationUser> users, IScopeResolver scope, IAuditService audit)
    {
        _db = db;
        _users = users;
        _scope = scope;
        _audit = audit;
    }

    // ===== DEF-P123-001/002 — تفرّد وحدات الدليل التنظيميّ =====
    //
    // نطاق التفرّد (مطابق للعقد): اسم الإدارة **على مستوى الشركة**، واسم الفريق **داخل إدارته**
    // فقط — فيجوز وجود «فريق التسويق» في إدارتين مختلفتين. رمز الإدارة يبقى فريدًا كما كان.
    //
    // التطبيع = `Trim()` وحده، وهو ما يفعله النموذج القائم فعلًا. لا تُضاف مطابقة غير حسّاسة
    // لحالة الأحرف: العقد الحاليّ لا ينصّ عليها، والمقارنة هنا يجب أن تُطابق قيد قاعدة البيانات
    // حرفيًّا وإلّا انفصل التحقّق التطبيقيّ عن الضمانة النهائيّة.

    private const string DepartmentNameConflictCode = "department.name.conflict";
    private const string DepartmentNameConflictAr = "توجد إدارة أخرى بهذا الاسم. اختر اسمًا مختلفًا.";

    private const string DepartmentCodeConflictCode = "department.code.conflict";
    private const string DepartmentCodeConflictAr = "توجد إدارة أخرى بهذا الرمز. اختر رمزًا مختلفًا.";

    private const string TeamNameConflictCode = "team.name.conflict";
    private const string TeamNameConflictAr = "يوجد فريق آخر بهذا الاسم داخل الإدارة نفسها. اختر اسمًا مختلفًا.";

    private Task<bool> DepartmentNameTakenAsync(string nameAr, Guid? excludeId, CancellationToken ct) =>
        _db.Departments.AnyAsync(d => d.NameAr == nameAr && (excludeId == null || d.Id != excludeId), ct);

    private Task<bool> DepartmentCodeTakenAsync(string code, Guid? excludeId, CancellationToken ct) =>
        _db.Departments.AnyAsync(d => d.Code == code && (excludeId == null || d.Id != excludeId), ct);

    private Task<bool> TeamNameTakenAsync(Guid departmentId, string nameAr, Guid? excludeId, CancellationToken ct) =>
        _db.Teams.AnyAsync(t => t.DepartmentId == departmentId && t.NameAr == nameAr
                                && (excludeId == null || t.Id != excludeId), ct);

    /// <summary>
    /// حفظ يترجم **قيود التفرّد المعروفة وحدها** إلى تعارض 409 بدل 500.
    ///
    /// <para>
    /// هذه شبكة أمان ضدّ التسابق (طلبان متزامنان يجتازان الفحص المسبق معًا)، لا بديل عنه.
    /// **لا يُبتلَع أيّ خطأ آخر:** ما لا يُطابق فهرسًا معروفًا يُعاد رميه كما هو ليسلك مسار
    /// الخطأ الحقيقيّ (500) — فتحويل كلّ <c>DbUpdateException</c> إلى 409 يُخفي أعطالًا فعليّة.
    /// </para>
    /// <para>لا يُكشف اسم القيد الداخليّ ولا نصّ SQL للعميل؛ الرسالة عربيّة والرمز دلاليّ.</para>
    /// </summary>
    private async Task<Result?> SaveTranslatingDirectoryConflictsAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return null;
        }
        catch (DbUpdateException ex) when (UniqueViolationIndex(ex) is { } index)
        {
            return index switch
            {
                IxDepartmentsName => Result.Failure(DepartmentNameConflictAr, DepartmentNameConflictCode),
                IxDepartmentsCode => Result.Failure(DepartmentCodeConflictAr, DepartmentCodeConflictCode),
                IxTeamsDepartmentName => Result.Failure(TeamNameConflictAr, TeamNameConflictCode),
                _ => throw ExceptionDispatchInfo.Capture(ex).SourceException
            };
        }
    }

    internal const string IxDepartmentsName = "IX_departments_NameAr";
    internal const string IxDepartmentsCode = "IX_departments_Code";
    internal const string IxTeamsDepartmentName = "IX_teams_DepartmentId_NameAr";

    /// <summary>اسم الفهرس المنتهَك عند 23505 فقط، وإلّا <c>null</c>.</summary>
    private static string? UniqueViolationIndex(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
            ? pg.ConstraintName
            : null;

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
            u.ManagerId,
            u.JobRoleId)).ToList();
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

    public async Task<IReadOnlyList<JobRoleDto>> ListJobRolesAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        var q = _db.JobRoles.AsNoTracking().AsQueryable();
        if (activeOnly) q = q.Where(j => j.IsActive);
        return await q.OrderBy(j => j.NameAr)
            .Select(j => new JobRoleDto(j.Id, j.NameAr, j.NameEn, j.Code, j.DepartmentId, j.IsActive))
            .ToListAsync(ct);
    }

    // ===== دليل الموارد البشرية المخصّص (قراءة فقط لحزمة A) =====
    // على مستوى الشركة عمدًا (بلا استدعاء _scope) — الحماية عبر سياسة HrDirectoryRead عند نقطة النهاية.
    // منفصل تمامًا عن ListUsersAsync/ListDepartmentsAsync/ListTeamsAsync العامة (لم تتغيّر سلوكًا).

    public async Task<IReadOnlyList<HrDirectoryUserDto>> ListHrDirectoryUsersAsync(bool includeInactive, bool actingIsAdmin, CancellationToken ct = default)
        => await BuildHrDirectoryUsersAsync(includeInactive ? null : true, actingIsAdmin, ct);

    public async Task<IReadOnlyList<HrDirectoryUserDto>> ListHrDirectoryManagersAsync(bool actingIsAdmin, CancellationToken ct = default)
        // المديرون المتاحون = المستخدمون النشطون فقط (استبعاد الذات ومنع الدائرية يُفرضان في الخدمة عند الحفظ).
        => await BuildHrDirectoryUsersAsync(true, actingIsAdmin, ct);

    private async Task<IReadOnlyList<HrDirectoryUserDto>> BuildHrDirectoryUsersAsync(bool? activeOnly, bool actingIsAdmin, CancellationToken ct)
    {
        var usersQuery = _db.Users.AsNoTracking();
        if (activeOnly == true) usersQuery = usersQuery.Where(u => u.IsActive);
        var users = await usersQuery.OrderBy(u => u.FullName).ToListAsync(ct);

        // أدوار كل مستخدم عبر جداول Identity (لتحديد الحسابات الحسّاسة فقط — لا تُعاد الأدوار في الـDTO).
        var userRoles = await (
            from ur in _db.UserRoles
            join r in _db.Roles on ur.RoleId equals r.Id
            select new { ur.UserId, RoleName = r.Name! }).ToListAsync(ct);
        var rolesByUser = userRoles
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.RoleName).ToHashSet());

        return users.Select(u =>
        {
            var roles = rolesByUser.GetValueOrDefault(u.Id) ?? new HashSet<string>();
            var isSensitive = roles.Any(r => SensitiveAccountRoles.Contains(r));
            // CanEdit يعتمد على دور المنفّذ لا على حساسية الهدف فقط:
            // الأدمن يستطيع تعديل الاسم/التنظيم لأيّ حساب (بما فيه الحسّاس) من هذا السطح؛ غير الأدمن (HR/CeoSupport/GM/CEO) ممنوع من الحسّاس.
            var canEdit = actingIsAdmin || !isSensitive;
            return new HrDirectoryUserDto(
                u.Id, u.FullName, u.Email ?? string.Empty, u.IsActive,
                u.DepartmentId, u.TeamId, u.ManagerId, u.JobRoleId,
                isSensitive, canEdit, u.HireDate, u.ExitDate);
        }).ToList();
    }

    public async Task<IReadOnlyList<DepartmentDto>> ListHrDirectoryDepartmentsAsync(CancellationToken ct = default)
        => await _db.Departments.AsNoTracking().OrderBy(d => d.NameAr)
            .Select(d => new DepartmentDto(d.Id, d.NameAr, d.NameEn, d.Code, d.ManagerId, d.IsActive))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TeamDto>> ListHrDirectoryTeamsAsync(CancellationToken ct = default)
        => await _db.Teams.AsNoTracking().OrderBy(t => t.NameAr)
            .Select(t => new TeamDto(t.Id, t.NameAr, t.NameEn, t.DepartmentId, t.TeamLeaderId, t.IsActive))
            .ToListAsync(ct);

    // عدد القوالب المرتبطة بمسمّى = القوالب المربوطة مباشرةً (JobRoleId) + الإسنادات الصريحة النشطة (scope=JobRole, Include).
    private async Task<Dictionary<Guid, int>> TemplateCountsByJobRoleAsync(CancellationToken ct)
    {
        var directPairs = await _db.ReportTemplates.AsNoTracking()
            .Where(t => t.JobRoleId != null)
            .Select(t => new { JobRoleId = t.JobRoleId!.Value, TemplateId = t.Id })
            .ToListAsync(ct);
        var assignPairs = await _db.ReportTemplateAssignments.AsNoTracking()
            .Where(a => a.IsActive && a.ScopeType == TemplateAssignmentScope.JobRole && a.Kind == TemplateAssignmentKind.Include)
            .Select(a => new { JobRoleId = a.ScopeId, TemplateId = a.ReportTemplateId })
            .ToListAsync(ct);
        return directPairs.Concat(assignPairs)
            .GroupBy(x => x.JobRoleId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.TemplateId).Distinct().Count());
    }

    private async Task<Dictionary<Guid, int>> EmployeeCountsByJobRoleAsync(CancellationToken ct)
        => await _db.Users.AsNoTracking()
            .Where(u => u.JobRoleId != null)
            .GroupBy(u => u.JobRoleId!.Value)
            .Select(g => new { JobRoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.JobRoleId, x => x.Count, ct);

    public async Task<IReadOnlyList<JobRoleDetailDto>> ListJobRolesWithCountsAsync(CancellationToken ct = default)
    {
        var roles = await _db.JobRoles.AsNoTracking().OrderBy(j => j.NameAr).ToListAsync(ct);
        var depts = await _db.Departments.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.NameAr, ct);
        var empCounts = await EmployeeCountsByJobRoleAsync(ct);
        var tplCounts = await TemplateCountsByJobRoleAsync(ct);

        return roles.Select(j => new JobRoleDetailDto(
            j.Id, j.NameAr, j.NameEn, j.Code, j.DepartmentId,
            j.DepartmentId is { } did ? depts.GetValueOrDefault(did) : null,
            j.IsActive,
            empCounts.GetValueOrDefault(j.Id),
            tplCounts.GetValueOrDefault(j.Id))).ToList();
    }

    private async Task<JobRoleDetailDto> BuildJobRoleDetailAsync(JobRole j, CancellationToken ct)
    {
        var deptName = j.DepartmentId is { } did
            ? await _db.Departments.AsNoTracking().Where(d => d.Id == did).Select(d => d.NameAr).FirstOrDefaultAsync(ct)
            : null;
        var empCount = await _db.Users.AsNoTracking().CountAsync(u => u.JobRoleId == j.Id, ct);
        var tplCount = (await TemplateCountsByJobRoleAsync(ct)).GetValueOrDefault(j.Id);
        return new JobRoleDetailDto(j.Id, j.NameAr, j.NameEn, j.Code, j.DepartmentId, deptName, j.IsActive, empCount, tplCount);
    }

    public async Task<Result<JobRoleDetailDto>> CreateJobRoleAsync(CreateJobRoleRequest req, Guid actingUserId, CancellationToken ct = default)
    {
        var nameAr = req.NameAr?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(nameAr))
            return Result<JobRoleDetailDto>.Failure("الاسم العربي للمسمّى الوظيفي مطلوب.", "jobrole.name_required");

        var dup = await _db.JobRoles.AsNoTracking().AnyAsync(j => j.NameAr.ToLower() == nameAr.ToLower(), ct);
        if (dup)
            return Result<JobRoleDetailDto>.Failure("يوجد مسمّى وظيفي بنفس الاسم العربي.", "jobrole.name_duplicate.conflict");

        var code = string.IsNullOrWhiteSpace(req.Code) ? null : req.Code!.Trim();
        if (code is not null)
        {
            var codeDup = await _db.JobRoles.AsNoTracking().AnyAsync(j => j.Code != null && j.Code.ToLower() == code.ToLower(), ct);
            if (codeDup)
                return Result<JobRoleDetailDto>.Failure("يوجد مسمّى وظيفي بنفس الرمز.", "jobrole.code_duplicate.conflict");
        }

        if (req.DepartmentId is { } depId)
        {
            var depExists = await _db.Departments.AsNoTracking().AnyAsync(d => d.Id == depId, ct);
            if (!depExists)
                return Result<JobRoleDetailDto>.Failure("الإدارة المحدّدة غير موجودة.", "department.not_found");
        }

        var role = new JobRole
        {
            NameAr = nameAr,
            NameEn = string.IsNullOrWhiteSpace(req.NameEn) ? null : req.NameEn!.Trim(),
            Code = code,
            DepartmentId = req.DepartmentId,
            IsActive = true,
        };
        _db.JobRoles.Add(role);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(actingUserId, "jobrole.created", "JobRole", role.Id,
            JsonSerializer.Serialize(new { role.NameAr, role.NameEn, role.Code, role.DepartmentId }), null, ct);
        return Result<JobRoleDetailDto>.Success(await BuildJobRoleDetailAsync(role, ct));
    }

    public async Task<Result<JobRoleDetailDto>> UpdateJobRoleAsync(Guid jobRoleId, UpdateJobRoleRequest req, Guid actingUserId, CancellationToken ct = default)
    {
        var role = await _db.JobRoles.FirstOrDefaultAsync(j => j.Id == jobRoleId, ct);
        if (role is null)
            return Result<JobRoleDetailDto>.Failure("المسمّى الوظيفي غير موجود.", "jobrole.not_found");

        var nameAr = req.NameAr?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(nameAr))
            return Result<JobRoleDetailDto>.Failure("الاسم العربي للمسمّى الوظيفي مطلوب.", "jobrole.name_required");

        var dup = await _db.JobRoles.AsNoTracking()
            .AnyAsync(j => j.Id != jobRoleId && j.NameAr.ToLower() == nameAr.ToLower(), ct);
        if (dup)
            return Result<JobRoleDetailDto>.Failure("يوجد مسمّى وظيفي بنفس الاسم العربي.", "jobrole.name_duplicate.conflict");

        var code = string.IsNullOrWhiteSpace(req.Code) ? null : req.Code!.Trim();
        if (code is not null)
        {
            var codeDup = await _db.JobRoles.AsNoTracking()
                .AnyAsync(j => j.Id != jobRoleId && j.Code != null && j.Code.ToLower() == code.ToLower(), ct);
            if (codeDup)
                return Result<JobRoleDetailDto>.Failure("يوجد مسمّى وظيفي بنفس الرمز.", "jobrole.code_duplicate.conflict");
        }

        if (req.DepartmentId is { } depId)
        {
            var depExists = await _db.Departments.AsNoTracking().AnyAsync(d => d.Id == depId, ct);
            if (!depExists)
                return Result<JobRoleDetailDto>.Failure("الإدارة المحدّدة غير موجودة.", "department.not_found");
        }

        var old = new { role.NameAr, role.NameEn, role.Code, role.DepartmentId };
        role.NameAr = nameAr;
        role.NameEn = string.IsNullOrWhiteSpace(req.NameEn) ? null : req.NameEn!.Trim();
        role.Code = code;
        role.DepartmentId = req.DepartmentId;
        role.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(actingUserId, "jobrole.updated", "JobRole", role.Id,
            JsonSerializer.Serialize(new { old, @new = new { role.NameAr, role.NameEn, role.Code, role.DepartmentId } }), null, ct);
        return Result<JobRoleDetailDto>.Success(await BuildJobRoleDetailAsync(role, ct));
    }

    // الأرشفة/إعادة التفعيل فقط (لا حذف صلب). الأرشفة لا تفكّ ربط الموظفين القائمين — تُخفي المسمّى من الاختيارات الجديدة (تصفية في الواجهة عبر activeOnly).
    public async Task<Result<JobRoleDetailDto>> SetJobRoleActiveAsync(Guid jobRoleId, bool isActive, Guid actingUserId, CancellationToken ct = default)
    {
        var role = await _db.JobRoles.FirstOrDefaultAsync(j => j.Id == jobRoleId, ct);
        if (role is null)
            return Result<JobRoleDetailDto>.Failure("المسمّى الوظيفي غير موجود.", "jobrole.not_found");

        if (role.IsActive == isActive)
            return Result<JobRoleDetailDto>.Failure(
                isActive ? "المسمّى الوظيفي نشط بالفعل." : "المسمّى الوظيفي مؤرشف بالفعل.",
                "jobrole.state_unchanged.conflict");

        role.IsActive = isActive;
        role.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(actingUserId, isActive ? "jobrole.reactivated" : "jobrole.archived", "JobRole", role.Id,
            JsonSerializer.Serialize(new { role.NameAr }), null, ct);
        return Result<JobRoleDetailDto>.Success(await BuildJobRoleDetailAsync(role, ct));
    }

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
                perms.Select(RoleAccess.PermissionLabelAr).ToList(),
                RoleCapabilities.ForRole(role));
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

    // ===== مفاتيح الصلاحيّات الدقيقة (perm) — المسار المنتج للمنح والإلغاء =====
    // AppPermissions.cs:6-9 يفرض: لا يكتسب أيّ دور هذه المفاتيح ضمنًا (ولا Admin)، والتعيين قرار
    // نشر صريح. لذلك تُخزَّن كمطالبات Identity على المستخدم بعينه، ولا يوجد ولن يوجد ربط دور↔مفتاح.
    // سلطة المنح = نفس سلطة توزيع الأدوار (Policies.UserManagement) لأنّها التغيير الأمنيّ النظير.

    private async Task<string[]> ReadPermissionClaimsAsync(ApplicationUser user) =>
        (await _users.GetClaimsAsync(user))
            .Where(c => c.Type == AppPermissions.ClaimType)
            .Select(c => c.Value)
            .Distinct()
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToArray();

    public async Task<Result<UserPermissionsDto>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result<UserPermissionsDto>.Failure("المستخدم غير موجود.", "user.not_found");

        return Result<UserPermissionsDto>.Success(
            new UserPermissionsDto(userId, await ReadPermissionClaimsAsync(user)));
    }

    public async Task<Result<UserPermissionsDto>> SetUserPermissionsAsync(
        Guid userId, IReadOnlyList<string> permissions, Guid actingUserId, CancellationToken ct = default)
    {
        var desired = (permissions ?? Array.Empty<string>())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // مفتاح غير معروف يُرفض بدل أن يُخزَّن صامتًا: مطالبة لا تطابق أيّ سياسة تعطي وهم صلاحيّة.
        var unknown = desired.Where(p => !AppPermissions.All.Contains(p)).ToList();
        if (unknown.Count > 0)
            return Result<UserPermissionsDto>.Failure(
                $"مفاتيح صلاحيّات غير معروفة: {string.Join("، ", unknown)}", "user.permissions.invalid");

        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result<UserPermissionsDto>.Failure("المستخدم غير موجود.", "user.not_found");

        var current = await ReadPermissionClaimsAsync(user);
        var toAdd = desired.Except(current, StringComparer.Ordinal).ToList();
        var toRemove = current.Except(desired, StringComparer.Ordinal).ToList();

        // Idempotent: تكرار النداء بنفس المجموعة لا يكتب شيئًا ولا يولّد سجلّ تدقيق زائفًا.
        if (toAdd.Count == 0 && toRemove.Count == 0)
            return Result<UserPermissionsDto>.Success(new UserPermissionsDto(userId, current));

        foreach (var p in toRemove)
        {
            var r = await _users.RemoveClaimAsync(user, new Claim(AppPermissions.ClaimType, p));
            if (!r.Succeeded)
                return Result<UserPermissionsDto>.Failure(
                    string.Join("; ", r.Errors.Select(e => e.Description)), "user.permissions.update_failed.conflict");
        }
        foreach (var p in toAdd)
        {
            var r = await _users.AddClaimAsync(user, new Claim(AppPermissions.ClaimType, p));
            if (!r.Succeeded)
                return Result<UserPermissionsDto>.Failure(
                    string.Join("; ", r.Errors.Select(e => e.Description)), "user.permissions.update_failed.conflict");
        }

        var after = await ReadPermissionClaimsAsync(user);
        await _audit.LogAsync(actingUserId, "user.permissions.changed", "User", userId,
            JsonSerializer.Serialize(new { before = current, after, granted = toAdd, revoked = toRemove }), null, ct);

        return Result<UserPermissionsDto>.Success(new UserPermissionsDto(userId, after));
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

        var email = (req.Email ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(email))
            return Result<DirectoryUserDto>.Failure("البريد الإلكتروني مطلوب.", "user.email.required");

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

        // تغيير البريد (هوية الدخول): يُمنع التكرار. البريد = UserName أيضًا.
        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var existing = await _users.FindByEmailAsync(email);
            if (existing is not null && existing.Id != user.Id)
                return Result<DirectoryUserDto>.Failure("هذا البريد الإلكتروني مستخدم بالفعل.", "user.email.duplicate.conflict");

            var setEmail = await _users.SetEmailAsync(user, email);
            if (!setEmail.Succeeded)
                return Result<DirectoryUserDto>.Failure(string.Join("; ", setEmail.Errors.Select(e => e.Description)), "user.email.invalid.conflict");
            var setName = await _users.SetUserNameAsync(user, email);
            if (!setName.Succeeded)
                return Result<DirectoryUserDto>.Failure(string.Join("; ", setName.Errors.Select(e => e.Description)), "user.email.invalid.conflict");
        }

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
            roles, user.DepartmentId, user.TeamId, user.ManagerId, user.JobRoleId));
    }

    // تعديل المسمّى الوظيفي للموظف فقط — لا يمسّ أي حقل آخر. يسجّل Audit بالقيمة القديمة/الجديدة.
    public async Task<Result<DirectoryUserDto>> UpdateUserJobRoleAsync(Guid userId, UpdateUserJobRoleRequest req, Guid actingUserId, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result<DirectoryUserDto>.Failure("المستخدم غير موجود.", "user.not_found");

        // التحقق من وجود المسمّى الوظيفي المطلوب (null = إزالة المسمّى).
        if (req.JobRoleId is Guid newJobRoleId)
        {
            var exists = await _db.JobRoles.AsNoTracking().AnyAsync(j => j.Id == newJobRoleId, ct);
            if (!exists)
                return Result<DirectoryUserDto>.Failure("المسمّى الوظيفي غير موجود.", "jobrole.not_found");
        }

        var oldJobRoleId = user.JobRoleId;
        if (oldJobRoleId == req.JobRoleId)
            return Result<DirectoryUserDto>.Failure("المسمّى الوظيفي الحالي مطابق للمطلوب — لا تغيير.", "jobrole.unchanged.conflict");

        user.JobRoleId = req.JobRoleId;
        var upd = await _users.UpdateAsync(user);
        if (!upd.Succeeded)
            return Result<DirectoryUserDto>.Failure(string.Join("; ", upd.Errors.Select(e => e.Description)), "user.update_failed.conflict");

        // Audit: الموظف + المسمّى القديم/الجديد + المنفّذ + الوقت (يضبطه AuditService) + ملاحظة اختيارية.
        var notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes!.Trim();
        await _audit.LogAsync(actingUserId, "user.jobrole.changed", "User", userId,
            JsonSerializer.Serialize(new { targetEmail = user.Email, oldJobRoleId, newJobRoleId = req.JobRoleId, notes }), null, ct);

        var roles = (await _users.GetRolesAsync(user)).ToList();
        return Result<DirectoryUserDto>.Success(new DirectoryUserDto(
            user.Id, user.FullName, user.Email ?? string.Empty, user.IsActive,
            roles, user.DepartmentId, user.TeamId, user.ManagerId, user.JobRoleId));
    }

    // تعديل البيانات الأساسية غير الحسّاسة للموظف (الاسم الكامل فقط) — لا يمسّ أيّ حقل آخر. يسجّل Audit.
    public async Task<Result<DirectoryUserDto>> UpdateUserBasicAsync(Guid userId, UpdateUserBasicRequest req, Guid actingUserId, bool actingIsAdmin, CancellationToken ct = default)
    {
        var fullName = (req.FullName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(fullName))
            return Result<DirectoryUserDto>.Failure("الاسم الكامل مطلوب.", "user.name.required");

        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result<DirectoryUserDto>.Failure("المستخدم غير موجود.", "user.not_found");

        // حاجز الحساب الحسّاس يُطبَّق على غير الأدمن فقط: HR/CeoSupport ممنوعون من تعديل Admin/CEO/GM/CeoSupport.
        // الأدمن مسموح له بتعديل الاسم لأيّ حساب (الأدوار/التعطيل/كلمة المرور تبقى عبر إدارة المستخدمين).
        if (!actingIsAdmin && await IsSensitiveAccountAsync(user))
            return Result<DirectoryUserDto>.Failure("لا يمكن تعديل بيانات حساب إداري/تنفيذي حسّاس من هذا السطح.", "auth.forbidden");

        var oldName = user.FullName;
        if (string.Equals(oldName, fullName, StringComparison.Ordinal))
            return Result<DirectoryUserDto>.Failure("الاسم الحالي مطابق للمطلوب — لا تغيير.", "user.basic.unchanged.conflict");

        user.FullName = fullName;
        var upd = await _users.UpdateAsync(user);
        if (!upd.Succeeded)
            return Result<DirectoryUserDto>.Failure(string.Join("; ", upd.Errors.Select(e => e.Description)), "user.update_failed.conflict");

        var notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes!.Trim();
        await _audit.LogAsync(actingUserId, "user.basic.updated", "User", userId,
            JsonSerializer.Serialize(new { targetEmail = user.Email, oldName, newName = fullName, notes }), null, ct);

        var roles = (await _users.GetRolesAsync(user)).ToList();
        return Result<DirectoryUserDto>.Success(new DirectoryUserDto(
            user.Id, user.FullName, user.Email ?? string.Empty, user.IsActive,
            roles, user.DepartmentId, user.TeamId, user.ManagerId, user.JobRoleId));
    }

    // DEF-R5-002 — نافذة خدمة الموظّف (الالتحاق/انتهاء الخدمة) على سطح إدارة الموظّف نفسه، بلا شاشة مستقلّة.
    // مصدر بيانات مُعلَن ومحكوم بصلاحيّة وتدقيق بدل رقم يُخصَم بلا سند. لا يُعيد كتابة تقييم واحد.
    public async Task<Result<DirectoryUserDto>> UpdateUserEmploymentWindowAsync(
        Guid userId, UpdateUserEmploymentWindowRequest req, Guid actingUserId, bool actingIsAdmin, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result<DirectoryUserDto>.Failure("المستخدم غير موجود.", "user.not_found");

        // الحاجز نفسه المطبَّق على بقيّة أسطح إدارة الموظّف: HR/CeoSupport ممنوعون من الحسابات الحسّاسة.
        if (!actingIsAdmin && await IsSensitiveAccountAsync(user))
            return Result<DirectoryUserDto>.Failure("لا يمكن تعديل بيانات حساب إداري/تنفيذي حسّاس من هذا السطح.", "auth.forbidden");

        // خروجٌ بلا التحاق نافذةٌ بلا بداية — ترفض صراحةً بدل أن تُستكمل بتخمين (CreatedAtUtc أو غيره).
        if (req.ExitDate is not null && req.HireDate is null)
            return Result<DirectoryUserDto>.Failure(
                "لا يمكن تسجيل تاريخ انتهاء الخدمة بلا تاريخ التحاق.", "user.employment.hire_required");
        if (req.HireDate is DateOnly h && req.ExitDate is DateOnly x && x < h)
            return Result<DirectoryUserDto>.Failure(
                "تاريخ انتهاء الخدمة لا يسبق تاريخ الالتحاق.", "user.employment.range_invalid");

        var oldHire = user.HireDate;
        var oldExit = user.ExitDate;
        if (oldHire == req.HireDate && oldExit == req.ExitDate)
            return Result<DirectoryUserDto>.Failure(
                "نافذة الخدمة الحالية مطابقة للمطلوب — لا تغيير.", "user.employment.unchanged.conflict");

        user.HireDate = req.HireDate;
        user.ExitDate = req.ExitDate;
        var upd = await _users.UpdateAsync(user);
        if (!upd.Succeeded)
            return Result<DirectoryUserDto>.Failure(string.Join("; ", upd.Errors.Select(e => e.Description)), "user.update_failed.conflict");

        var notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes!.Trim();
        await _audit.LogAsync(actingUserId, "user.employment_window.updated", "User", userId,
            JsonSerializer.Serialize(new
            {
                targetEmail = user.Email,
                oldHireDate = oldHire, newHireDate = req.HireDate,
                oldExitDate = oldExit, newExitDate = req.ExitDate,
                notes
            }), null, ct);

        var roles = (await _users.GetRolesAsync(user)).ToList();
        return Result<DirectoryUserDto>.Success(new DirectoryUserDto(
            user.Id, user.FullName, user.Email ?? string.Empty, user.IsActive,
            roles, user.DepartmentId, user.TeamId, user.ManagerId, user.JobRoleId));
    }

    // تعديل الانتماء التنظيمي للموظف فقط (الإدارة/الفريق/المدير) مع قيود أمان صارمة. لا يمسّ الاسم/البريد/الأدوار/التفعيل.
    public async Task<Result<DirectoryUserDto>> UpdateUserOrgAssignmentAsync(Guid userId, UpdateUserOrgAssignmentRequest req, Guid actingUserId, bool actingIsAdmin, CancellationToken ct = default)
    {
        // (1) لا تغيير ذاتي عبر هذا السطح.
        if (userId == actingUserId)
            return Result<DirectoryUserDto>.Failure("لا يمكنك تعديل انتمائك التنظيمي من هذا السطح.", "user.org.self.conflict");

        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result<DirectoryUserDto>.Failure("المستخدم غير موجود.", "user.not_found");

        // (2) حاجز الحساب الحسّاس يُطبَّق على غير الأدمن فقط: HR/CeoSupport/GM/CEO ممنوعون من نقل Admin/CEO/GM/CeoSupport.
        // الأدمن مسموح له بالنقل التنظيمي لأيّ حساب (الأدوار/التعطيل/كلمة المرور تبقى عبر إدارة المستخدمين).
        if (!actingIsAdmin && await IsSensitiveAccountAsync(user))
            return Result<DirectoryUserDto>.Failure("لا يمكن تغيير الانتماء التنظيمي لحساب إداري/تنفيذي حسّاس من هذا السطح.", "auth.forbidden");

        // (3) المدير لا يكون المستخدم نفسه.
        if (req.ManagerId == userId)
            return Result<DirectoryUserDto>.Failure("لا يمكن أن يكون المستخدم مديرًا لنفسه.", "user.manager.self.conflict");

        // (4) وجود الفريق/الإدارة/المدير.
        var orgErr = await ValidateOrgAsync(req.TeamId, req.DepartmentId, req.ManagerId, ct);
        if (orgErr is not null)
            return Result<DirectoryUserDto>.Failure(orgErr.Value.Error, orgErr.Value.Code);

        // (5) المدير المعيَّن يجب أن يكون نشطًا.
        if (req.ManagerId is Guid mgrId)
        {
            var managerActive = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == mgrId && u.IsActive, ct);
            if (!managerActive)
                return Result<DirectoryUserDto>.Failure("لا يمكن تعيين مدير غير نشط.", "user.manager.inactive.conflict");

            // (6) منع علاقة مدير دائرية: لا يجوز أن يصل تسلسل مديري المدير المعيَّن إلى المستخدم نفسه.
            if (await WouldCreateManagerCycleAsync(userId, mgrId, ct))
                return Result<DirectoryUserDto>.Failure("لا يمكن إنشاء علاقة مدير دائرية.", "user.manager.cycle.conflict");
        }

        var oldDepartmentId = user.DepartmentId;
        var oldTeamId = user.TeamId;
        var oldManagerId = user.ManagerId;

        var newDepartmentId = await ResolveDepartmentAsync(req.TeamId, req.DepartmentId, ct);
        if (oldDepartmentId == newDepartmentId && oldTeamId == req.TeamId && oldManagerId == req.ManagerId)
            return Result<DirectoryUserDto>.Failure("الانتماء التنظيمي الحالي مطابق للمطلوب — لا تغيير.", "user.org.unchanged.conflict");

        user.DepartmentId = newDepartmentId;
        user.TeamId = req.TeamId;
        user.ManagerId = req.ManagerId;

        var upd = await _users.UpdateAsync(user);
        if (!upd.Succeeded)
            return Result<DirectoryUserDto>.Failure(string.Join("; ", upd.Errors.Select(e => e.Description)), "user.update_failed.conflict");

        // Audit: المستخدم المتأثّر + الإدارة/الفريق/المدير (القديم والجديد) + المنفّذ + الوقت (AuditService) + الملاحظة.
        var notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes!.Trim();
        await _audit.LogAsync(actingUserId, "user.org.changed", "User", userId,
            JsonSerializer.Serialize(new
            {
                targetEmail = user.Email,
                oldDepartmentId, newDepartmentId,
                oldTeamId, newTeamId = req.TeamId,
                oldManagerId, newManagerId = req.ManagerId,
                notes
            }), null, ct);

        var roles = (await _users.GetRolesAsync(user)).ToList();
        return Result<DirectoryUserDto>.Success(new DirectoryUserDto(
            user.Id, user.FullName, user.Email ?? string.Empty, user.IsActive,
            roles, user.DepartmentId, user.TeamId, user.ManagerId, user.JobRoleId));
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

    public async Task<Result> ResetUserPasswordAsync(Guid userId, string newPassword, Guid actingUserId, bool actorIsAdmin, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
            return Result.Failure("كلمة المرور الجديدة مطلوبة.", "user.password.required");

        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result.Failure("المستخدم غير موجود.", "user.not_found");

        var targetIsAdmin = await _users.IsInRoleAsync(user, Roles.Admin);

        // حساب مدير النظام لا يُعاد تعيين كلمة مروره إلا بواسطة مدير نظام (لا CeoSupport).
        if (targetIsAdmin && !actorIsAdmin)
            return Result.Failure("لا يمكن إعادة تعيين كلمة مرور حساب مدير نظام إلا بواسطة مدير نظام.", "auth.forbidden");

        // منع إعادة تعيين كلمة مرور آخر مدير نظام نشط (تجنّب فقدان الوصول).
        if (targetIsAdmin)
        {
            var admins = await _users.GetUsersInRoleAsync(Roles.Admin);
            if (admins.Count(a => a.IsActive) <= 1)
                return Result.Failure("لا يمكن إعادة تعيين كلمة مرور آخر مدير نظام نشط.", "user.last_admin.conflict");
        }

        // إعادة التعيين الذرّية عبر Identity: توليد رمز ثم تطبيقه (يتحقق من قوة كلمة المرور بلا فجوة بلا كلمة مرور).
        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var reset = await _users.ResetPasswordAsync(user, token, newPassword);
        if (!reset.Succeeded)
            return Result.Failure("كلمة المرور الجديدة لا تستوفي الشروط (8 أحرف على الأقل، وتشمل حرفًا كبيرًا وصغيرًا ورقمًا).", "auth.password_invalid");

        // إبطال كل رموز التجديد النشطة كي تُغلق الجلسات القائمة.
        var activeTokens = await _db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var t in activeTokens) t.RevokedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // تسجيل العملية في Audit دون أي إشارة لكلمة المرور.
        await _audit.LogAsync(actingUserId, "user.password.reset", "User", userId,
            JsonSerializer.Serialize(new { targetEmail = user.Email, targetIsAdmin }), null, ct);

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

    // ===== عضويات الفريق الإضافية (MULTI-TEAM-MEMBERSHIP-MVP-R1) =====
    // منفصلة تمامًا عن AddTeamMemberAsync: لا تغيّر TeamId/DepartmentId/ManagerId/JobRoleId على المستخدم،
    // ولا تدخل ScopeResolver ولا تؤثّر على التقارير/الـKPI/المشاريع. الوصول للأدمن فقط (سياسة الكنترولر).

    public async Task<Result<TeamMembershipsDto>> ListTeamMembershipsAsync(Guid teamId, CancellationToken ct = default)
    {
        var team = await _db.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team is null)
            return Result<TeamMembershipsDto>.Failure("الفريق غير موجود.", "team.not_found");

        // الأعضاء الأساسيون: من TeamId == teamId.
        var primary = await _db.Users.AsNoTracking()
            .Where(u => u.TeamId == teamId)
            .OrderBy(u => u.FullName)
            .Select(u => new TeamMemberDto(
                u.Id, u.FullName, u.Email ?? string.Empty, u.IsActive, u.DepartmentId, u.JobRoleId,
                true, null, true, null, null, null))
            .ToListAsync(ct);

        // الأعضاء الإضافيون: من جدول العضويات (نشطة فقط) — يُستبعَد من صار أساسيًّا في نفس الفريق.
        var memberships = await _db.UserTeamMemberships.AsNoTracking()
            .Where(m => m.TeamId == teamId && m.IsActive)
            .ToListAsync(ct);
        var addUserIds = memberships.Select(m => m.UserId).ToList();
        var addUsers = await _db.Users.AsNoTracking()
            .Where(u => addUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);
        var additional = memberships
            .Where(m => addUsers.ContainsKey(m.UserId) && addUsers[m.UserId].TeamId != teamId)
            .Select(m =>
            {
                var u = addUsers[m.UserId];
                return new TeamMemberDto(
                    u.Id, u.FullName, u.Email ?? string.Empty, u.IsActive, u.DepartmentId, u.JobRoleId,
                    false, m.Id, m.IsActive, m.StartDateUtc, m.EndDateUtc, m.Notes);
            })
            .OrderBy(m => m.FullName)
            .ToList();

        return Result<TeamMembershipsDto>.Success(new TeamMembershipsDto(
            team.Id, team.NameAr, team.DepartmentId, primary, additional));
    }

    public async Task<Result<TeamMemberDto>> AddAdditionalTeamMemberAsync(Guid teamId, AddAdditionalMemberRequest req, Guid actingUserId, CancellationToken ct = default)
    {
        var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team is null)
            return Result<TeamMemberDto>.Failure("الفريق غير موجود.", "team.not_found");

        var user = await _users.FindByIdAsync(req.UserId.ToString());
        if (user is null)
            return Result<TeamMemberDto>.Failure("المستخدم غير موجود.", "user.not_found");

        // لا تُضاف الحسابات الحسّاسة (Admin/CEO/GM/CeoSupport) كعضوية إضافية في MVP.
        if (await IsSensitiveAccountAsync(user))
            return Result<TeamMemberDto>.Failure("لا يمكن إضافة حساب إداري/تنفيذي حسّاس كعضو إضافي.", "team.additional_member.sensitive.conflict");

        // إن كان المستخدم عضوًا أساسيًّا في نفس الفريق ⇒ لا تُنشأ عضوية إضافية.
        if (user.TeamId == teamId)
            return Result<TeamMemberDto>.Failure("المستخدم عضو أساسي في هذا الفريق بالفعل.", "team.additional_member.already_primary.conflict");

        var existing = await _db.UserTeamMemberships
            .FirstOrDefaultAsync(m => m.UserId == req.UserId && m.TeamId == teamId, ct);
        if (existing is not null && existing.IsActive)
            return Result<TeamMemberDto>.Failure("للمستخدم عضوية إضافية نشطة في هذا الفريق بالفعل.", "team.additional_member.duplicate.conflict");

        if (existing is not null)
        {
            // إعادة تفعيل عضوية غير نشطة قائمة بدل إنشاء صفّ مكرّر.
            existing.IsActive = true;
            existing.StartDateUtc = req.StartDateUtc ?? DateTime.UtcNow;
            existing.EndDateUtc = req.EndDateUtc;
            existing.Notes = req.Notes;
            existing.CreatedByUserId = actingUserId;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            existing = new UserTeamMembership
            {
                UserId = req.UserId,
                TeamId = teamId,
                IsActive = true,
                MembershipType = "Secondary",
                StartDateUtc = req.StartDateUtc ?? DateTime.UtcNow,
                EndDateUtc = req.EndDateUtc,
                CreatedByUserId = actingUserId,
                Notes = req.Notes,
            };
            _db.UserTeamMemberships.Add(existing);
        }
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(actingUserId, "team.additional_member.added", "Team", teamId,
            JsonSerializer.Serialize(new { teamName = team.NameAr, userId = req.UserId, membershipId = existing.Id }), null, ct);

        return Result<TeamMemberDto>.Success(new TeamMemberDto(
            user.Id, user.FullName, user.Email ?? string.Empty, user.IsActive, user.DepartmentId, user.JobRoleId,
            false, existing.Id, existing.IsActive, existing.StartDateUtc, existing.EndDateUtc, existing.Notes));
    }

    public async Task<Result> RemoveAdditionalTeamMemberAsync(Guid teamId, Guid userId, Guid actingUserId, CancellationToken ct = default)
    {
        var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team is null)
            return Result.Failure("الفريق غير موجود.", "team.not_found");

        var membership = await _db.UserTeamMemberships
            .FirstOrDefaultAsync(m => m.UserId == userId && m.TeamId == teamId && m.IsActive, ct);
        if (membership is null)
            return Result.Failure("لا توجد عضوية إضافية نشطة لهذا المستخدم في هذا الفريق.", "team.additional_member.not_found");

        // إلغاء التفعيل (حذف ناعم) يحفظ السجل التاريخي ويسمح بإعادة التفعيل لاحقًا.
        membership.IsActive = false;
        membership.EndDateUtc = DateTime.UtcNow;
        membership.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(actingUserId, "team.additional_member.removed", "Team", teamId,
            JsonSerializer.Serialize(new { teamName = team.NameAr, userId, membershipId = membership.Id }), null, ct);

        return Result.Success();
    }

    public async Task<Result<UserTeamMembershipsDto>> ListUserTeamMembershipsAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return Result<UserTeamMembershipsDto>.Failure("المستخدم غير موجود.", "user.not_found");

        var primaryTeamName = user.TeamId is Guid ptid
            ? await _db.Teams.AsNoTracking().Where(t => t.Id == ptid).Select(t => t.NameAr).FirstOrDefaultAsync(ct)
            : null;

        var deptNames = await _db.Departments.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.NameAr, ct);

        var memberships = await _db.UserTeamMemberships.AsNoTracking()
            .Where(m => m.UserId == userId && m.IsActive)
            .ToListAsync(ct);
        var teamIds = memberships.Select(m => m.TeamId).ToList();
        var teams = await _db.Teams.AsNoTracking()
            .Where(t => teamIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, ct);

        var additional = memberships
            .Where(m => teams.ContainsKey(m.TeamId))
            .Select(m =>
            {
                var t = teams[m.TeamId];
                return new UserTeamMembershipDto(
                    m.Id, t.Id, t.NameAr, t.DepartmentId, deptNames.GetValueOrDefault(t.DepartmentId),
                    m.IsActive, m.MembershipType, m.StartDateUtc, m.EndDateUtc, m.Notes);
            })
            .OrderBy(m => m.TeamNameAr)
            .ToList();

        return Result<UserTeamMembershipsDto>.Success(new UserTeamMembershipsDto(
            user.Id, user.FullName, user.TeamId, primaryTeamName, additional));
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

        // DEF-P123-001 — تفرّد اسم الفريق **داخل إدارته** فقط: الاسم نفسه مسموح في إدارة أخرى.
        if (await TeamNameTakenAsync(req.DepartmentId, nameAr, null, ct))
            return Result<TeamDto>.Failure(TeamNameConflictAr, TeamNameConflictCode);

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

        var saved = await SaveTranslatingDirectoryConflictsAsync(ct);
        if (saved is not null) return Result<TeamDto>.Failure(saved.Error!, saved.ErrorCode!);

        return Result<TeamDto>.Success(new TeamDto(team.Id, team.NameAr, team.NameEn, team.DepartmentId, team.TeamLeaderId, team.IsActive));
    }

    public async Task<Result<TeamDto>> UpdateTeamAsync(Guid teamId, UpdateTeamRequest req, Guid actingUserId, CancellationToken ct = default)
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

        // DEF-P123-001 — التفرّد يُقاس على الإدارة **الهدف** (تُغطّي النقل وإعادة التسمية معًا).
        if (await TeamNameTakenAsync(req.DepartmentId, nameAr, teamId, ct))
            return Result<TeamDto>.Failure(TeamNameConflictAr, TeamNameConflictCode);

        if (req.TeamLeaderId is Guid lid && !await _db.Users.AnyAsync(u => u.Id == lid, ct))
            return Result<TeamDto>.Failure("قائد الفريق المحدّد غير موجود.", "team.leader.not_found");

        // 1.1 — أمان نقل الفريق: التقاط الإدارة القديمة قبل التطبيق لاكتشاف النقل ومزامنة أعضائه.
        var oldDepartmentId = team.DepartmentId;
        var isDepartmentChange = oldDepartmentId != req.DepartmentId;

        team.NameAr = nameAr;
        team.NameEn = string.IsNullOrWhiteSpace(req.NameEn) ? null : req.NameEn!.Trim();
        team.DepartmentId = req.DepartmentId;
        team.TeamLeaderId = req.TeamLeaderId;
        team.IsActive = req.IsActive;

        // 1.1 — مزامنة DepartmentId لأعضاء الفريق الحاليين فقط (TeamId == team.Id) إلى الإدارة الجديدة.
        // لا يمسّ المسمّى/المدير/الفريق، ولا أيّ مستخدم خارج الفريق. يمنع تكوّن عدم تطابق الإدارة.
        var syncedMembers = 0;
        if (isDepartmentChange && req.SyncMemberDepartments)
        {
            syncedMembers = await _db.Users
                .Where(u => u.TeamId == teamId && u.DepartmentId != req.DepartmentId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.DepartmentId, (Guid?)req.DepartmentId), ct);
        }

        await _db.SaveChangesAsync(ct);

        if (isDepartmentChange)
        {
            await _audit.LogAsync(actingUserId, "team.moved", "Team", team.Id,
                JsonSerializer.Serialize(new
                {
                    teamName = team.NameAr,
                    oldDepartmentId,
                    newDepartmentId = req.DepartmentId,
                    syncMemberDepartments = req.SyncMemberDepartments,
                    syncedMembersCount = syncedMembers
                }), null, ct);
        }
        else
        {
            await _audit.LogAsync(actingUserId, "team.updated", "Team", team.Id,
                JsonSerializer.Serialize(new { teamName = team.NameAr }), null, ct);
        }

        return Result<TeamDto>.Success(new TeamDto(team.Id, team.NameAr, team.NameEn, team.DepartmentId, team.TeamLeaderId, team.IsActive));
    }

    public async Task<Result> DeleteTeamAsync(Guid teamId, Guid actingUserId, CancellationToken ct = default)
    {
        var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team is null)
            return Result.Failure("الفريق غير موجود.", "team.not_found");

        // 1.2 — حارس الحذف: يُمنع الحذف إن كان للفريق أعضاء أو مشاريع يملكها (OwnerTeamId).
        // الأرشفة (IsActive=false عبر تعديل الفريق) هي البديل الموصى به للفريق المستخدَم.
        var memberCount = await _db.Users.CountAsync(u => u.TeamId == teamId, ct);
        var ownedProjectsCount = await _db.Projects.CountAsync(p => p.OwnerTeamId == teamId, ct);
        // MULTI-TEAM-MEMBERSHIP-MVP-R1: يُمنع الحذف أيضًا إن كان للفريق عضويات إضافية نشطة.
        var additionalMemberCount = await _db.UserTeamMemberships.CountAsync(m => m.TeamId == teamId && m.IsActive, ct);
        if (memberCount > 0 || ownedProjectsCount > 0 || additionalMemberCount > 0)
        {
            return Result.Failure(
                $"لا يمكن حذف الفريق لارتباطه ببيانات قائمة ({memberCount} عضوًا أساسيًّا، {additionalMemberCount} عضوًا إضافيًّا، {ownedProjectsCount} مشروعًا). " +
                "أزل الأعضاء الأساسيين والإضافيين وانقل/أعِد إسناد المشاريع أولًا، أو أرشِف الفريق (إلغاء التفعيل) بدلًا من الحذف.",
                "team.delete_forbidden.conflict");
        }

        _db.Teams.Remove(team);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(actingUserId, "team.deleted", "Team", teamId,
            JsonSerializer.Serialize(new { teamName = team.NameAr, departmentId = team.DepartmentId }), null, ct);

        return Result.Success();
    }

    public async Task<IReadOnlyList<TeamSummaryDto>> ListTeamSummariesAsync(CancellationToken ct = default)
    {
        var scope = await _scope.ResolveAsync(ct);
        var teamsQuery = _db.Teams.AsNoTracking().AsQueryable();
        if (!scope.SeesAll)
        {
            var ids = scope.UserIds;
            var visibleTeamIds = await _db.Users
                .Where(u => ids.Contains(u.Id) && u.TeamId != null)
                .Select(u => u.TeamId!.Value).Distinct().ToListAsync(ct);
            teamsQuery = teamsQuery.Where(t => (t.TeamLeaderId != null && ids.Contains(t.TeamLeaderId.Value)) || visibleTeamIds.Contains(t.Id));
        }
        var teams = await teamsQuery.OrderBy(t => t.NameAr).ToListAsync(ct);
        var teamIds = teams.Select(t => t.Id).ToList();

        var deptNames = await _db.Departments.AsNoTracking()
            .ToDictionaryAsync(d => d.Id, d => d.NameAr, ct);
        var leaderIds = teams.Where(t => t.TeamLeaderId != null).Select(t => t.TeamLeaderId!.Value).Distinct().ToList();
        var leaderNames = await _db.Users.AsNoTracking()
            .Where(u => leaderIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        // أعضاء كل فريق (TeamId) مع إداراتهم لحساب العدد وعدم التطابق.
        var members = await _db.Users.AsNoTracking()
            .Where(u => u.TeamId != null && teamIds.Contains(u.TeamId!.Value))
            .Select(u => new { TeamId = u.TeamId!.Value, u.DepartmentId })
            .ToListAsync(ct);
        var membersByTeam = members.GroupBy(m => m.TeamId).ToDictionary(g => g.Key, g => g.ToList());

        var projects = await _db.Projects.AsNoTracking()
            .Where(p => p.OwnerTeamId != null && teamIds.Contains(p.OwnerTeamId!.Value))
            .Select(p => new { TeamId = p.OwnerTeamId!.Value, p.Status })
            .ToListAsync(ct);
        var projectsByTeam = projects.GroupBy(p => p.TeamId).ToDictionary(g => g.Key, g => g.ToList());

        // عدد العضويات الإضافية النشطة لكل فريق (MULTI-TEAM-MEMBERSHIP-MVP-R1) — منفصل عن العدّ الأساسي،
        // لا يُدمَج في MemberCount ولا يدخل عدّادات الإدارة (DepartmentId يظل مبنيًّا على الفريق الأساسي فقط).
        var additionalCounts = (await _db.UserTeamMemberships.AsNoTracking()
            .Where(m => m.IsActive && teamIds.Contains(m.TeamId))
            .Select(m => m.TeamId)
            .ToListAsync(ct))
            .GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());

        return teams.Select(t =>
        {
            var tm = membersByTeam.GetValueOrDefault(t.Id) ?? new();
            var pr = projectsByTeam.GetValueOrDefault(t.Id) ?? new();
            return new TeamSummaryDto(
                t.Id, t.NameAr, t.NameEn, t.DepartmentId,
                deptNames.GetValueOrDefault(t.DepartmentId),
                t.TeamLeaderId,
                t.TeamLeaderId is Guid lid ? leaderNames.GetValueOrDefault(lid) : null,
                t.IsActive,
                tm.Count,
                pr.Count,
                pr.Count(p => p.Status == ProjectStatus.Active),
                tm.Count(m => m.DepartmentId != t.DepartmentId),
                tm.Count,
                additionalCounts.GetValueOrDefault(t.Id));
        }).ToList();
    }

    public async Task<IReadOnlyList<DepartmentSummaryDto>> ListDepartmentSummariesAsync(CancellationToken ct = default)
    {
        var teamSummaries = await ListTeamSummariesAsync(ct);

        var scope = await _scope.ResolveAsync(ct);
        var deptQuery = _db.Departments.AsNoTracking().AsQueryable();
        if (!scope.SeesAll)
        {
            var ids = scope.UserIds;
            var visibleDeptIds = await _db.Users
                .Where(u => ids.Contains(u.Id) && u.DepartmentId != null)
                .Select(u => u.DepartmentId!.Value).Distinct().ToListAsync(ct);
            deptQuery = deptQuery.Where(d => (d.ManagerId != null && ids.Contains(d.ManagerId.Value)) || visibleDeptIds.Contains(d.Id));
        }
        var depts = await deptQuery.OrderBy(d => d.NameAr).ToListAsync(ct);

        var managerIds = depts.Where(d => d.ManagerId != null).Select(d => d.ManagerId!.Value).Distinct().ToList();
        var managerNames = await _db.Users.AsNoTracking()
            .Where(u => managerIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var teamsByDept = teamSummaries.GroupBy(t => t.DepartmentId).ToDictionary(g => g.Key, g => g.ToList());

        return depts.Select(d =>
        {
            var teams = teamsByDept.GetValueOrDefault(d.Id) ?? new();
            return new DepartmentSummaryDto(
                d.Id, d.NameAr, d.NameEn, d.Code, d.ManagerId,
                d.ManagerId is Guid mid ? managerNames.GetValueOrDefault(mid) : null,
                d.ManagerId != null,
                d.IsActive,
                teams.Count,
                teams.Sum(t => t.MemberCount),
                teams.Sum(t => t.ProjectsCount),
                teams);
        }).ToList();
    }

    public async Task<Result<TeamMoveImpactDto>> GetTeamMoveImpactAsync(Guid teamId, Guid targetDepartmentId, CancellationToken ct = default)
    {
        var team = await _db.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team is null)
            return Result<TeamMoveImpactDto>.Failure("الفريق غير موجود.", "team.not_found");

        var scopeErr = await EnsureTeamInScopeAsync(teamId, ct);
        if (scopeErr is not null) return Result<TeamMoveImpactDto>.Failure(scopeErr.Error!, scopeErr.ErrorCode!);

        var targetDept = await _db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == targetDepartmentId, ct);
        if (targetDept is null)
            return Result<TeamMoveImpactDto>.Failure("الإدارة المستهدفة غير موجودة.", "department.not_found");

        var currentDept = await _db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == team.DepartmentId, ct);
        var leaderName = team.TeamLeaderId is Guid lid
            ? await _db.Users.AsNoTracking().Where(u => u.Id == lid).Select(u => u.FullName).FirstOrDefaultAsync(ct)
            : null;

        var memberDeptIds = await _db.Users.AsNoTracking()
            .Where(u => u.TeamId == teamId)
            .Select(u => u.DepartmentId)
            .ToListAsync(ct);
        var memberCount = memberDeptIds.Count;
        var isChange = team.DepartmentId != targetDepartmentId;
        // عدم التطابق يُقاس مقابل الإدارة المستهدفة (بعد النقل): مَن لن تتطابق إدارته إن لم تُزامَن.
        var mismatchCount = memberDeptIds.Count(d => d != targetDepartmentId);

        var memberIds = await _db.Users.AsNoTracking()
            .Where(u => u.TeamId == teamId).Select(u => u.Id).ToListAsync(ct);
        var projectsCount = await _db.Projects.CountAsync(p => p.OwnerTeamId == teamId, ct);
        var activeProjectsCount = await _db.Projects.CountAsync(p => p.OwnerTeamId == teamId && p.Status == ProjectStatus.Active, ct);
        var submissionsCount = await _db.ReportSubmissions.CountAsync(s => memberIds.Contains(s.SubmitterId), ct);

        var willSync = isChange && memberCount > 0;
        var warnings = new List<string>();
        if (!isChange)
            warnings.Add("الإدارة المستهدفة هي نفسها الحالية — لن يحدث نقل.");
        if (team.TeamLeaderId is null)
            warnings.Add("لا يوجد قائد فريق محدّد لهذا الفريق.");
        if (isChange && memberCount > 0)
            warnings.Add($"سيُحدَّث انتماء {memberCount} عضوًا إلى الإدارة الجديدة عند المزامنة.");
        if (isChange && mismatchCount > 0)
            warnings.Add($"{mismatchCount} عضوًا انتماؤهم الحالي يختلف عن الإدارة المستهدفة.");
        if (activeProjectsCount > 0)
            warnings.Add($"الفريق يملك {activeProjectsCount} مشروعًا نشطًا (تبقى ملكيتها للفريق بلا تغيير).");

        return Result<TeamMoveImpactDto>.Success(new TeamMoveImpactDto(
            team.Id, team.NameAr,
            team.DepartmentId, currentDept?.NameAr,
            targetDepartmentId, targetDept.NameAr,
            isChange,
            team.TeamLeaderId, leaderName,
            memberCount,
            projectsCount, activeProjectsCount,
            submissionsCount,
            mismatchCount,
            willSync,
            warnings));
    }

    public async Task<Result<DepartmentDto>> CreateDepartmentAsync(CreateDepartmentRequest req, CancellationToken ct = default)
    {
        var nameAr = (req.NameAr ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(nameAr))
            return Result<DepartmentDto>.Failure("اسم الإدارة مطلوب.", "department.name.required");

        var code = string.IsNullOrWhiteSpace(req.Code) ? null : req.Code!.Trim();

        // DEF-P123-001/002 — تحقّق تطبيقيّ مسبق يعطي رسالة مفهومة. القيد في قاعدة البيانات
        // هو الضمانة النهائيّة ضدّ التسابق، وانتهاكه يُترجَم إلى نفس الرمز الدلاليّ أدناه.
        if (await DepartmentNameTakenAsync(nameAr, null, ct))
            return Result<DepartmentDto>.Failure(DepartmentNameConflictAr, DepartmentNameConflictCode);

        if (code is not null && await DepartmentCodeTakenAsync(code, null, ct))
            return Result<DepartmentDto>.Failure(DepartmentCodeConflictAr, DepartmentCodeConflictCode);

        if (req.ManagerId is Guid mid && !await _db.Users.AnyAsync(u => u.Id == mid, ct))
            return Result<DepartmentDto>.Failure("المدير المحدّد غير موجود.", "user.manager.not_found");

        var dept = new Department
        {
            NameAr = nameAr,
            NameEn = string.IsNullOrWhiteSpace(req.NameEn) ? null : req.NameEn!.Trim(),
            Code = code,
            ManagerId = req.ManagerId,
            IsActive = true,
        };
        _db.Departments.Add(dept);

        var saved = await SaveTranslatingDirectoryConflictsAsync(ct);
        if (saved is not null) return Result<DepartmentDto>.Failure(saved.Error!, saved.ErrorCode!);

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

        var code = string.IsNullOrWhiteSpace(req.Code) ? null : req.Code!.Trim();

        // DEF-P123-001/002 — التعديل يخضع لنفس التفرّد، مع استثناء الصفّ نفسه من الفحص.
        if (await DepartmentNameTakenAsync(nameAr, departmentId, ct))
            return Result<DepartmentDto>.Failure(DepartmentNameConflictAr, DepartmentNameConflictCode);

        if (code is not null && await DepartmentCodeTakenAsync(code, departmentId, ct))
            return Result<DepartmentDto>.Failure(DepartmentCodeConflictAr, DepartmentCodeConflictCode);

        if (req.ManagerId is Guid mid && !await _db.Users.AnyAsync(u => u.Id == mid, ct))
            return Result<DepartmentDto>.Failure("المدير المحدّد غير موجود.", "user.manager.not_found");

        dept.NameAr = nameAr;
        dept.NameEn = string.IsNullOrWhiteSpace(req.NameEn) ? null : req.NameEn!.Trim();
        dept.Code = code;
        dept.ManagerId = req.ManagerId;
        dept.IsActive = req.IsActive;

        var saved = await SaveTranslatingDirectoryConflictsAsync(ct);
        if (saved is not null) return Result<DepartmentDto>.Failure(saved.Error!, saved.ErrorCode!);

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
    // الأدوار الحسّاسة التي لا يجوز تعديل حساباتها عبر سطح HR الجديد (تُدار حصرًا عبر إدارة الأدمن).
    private static readonly string[] SensitiveAccountRoles =
    {
        Roles.Admin, Roles.Ceo, Roles.GeneralManager, Roles.CeoSupport
    };

    private async Task<bool> IsSensitiveAccountAsync(ApplicationUser user)
    {
        var roles = await _users.GetRolesAsync(user);
        return roles.Any(r => SensitiveAccountRoles.Contains(r));
    }

    // هل يُنشئ تعيينُ المدير proposedManagerId للمستخدم userId علاقةً دائرية؟ (تتبّع سلسلة مديري المدير المعيَّن).
    private async Task<bool> WouldCreateManagerCycleAsync(Guid userId, Guid proposedManagerId, CancellationToken ct)
    {
        var visited = new HashSet<Guid>();
        Guid? cursor = proposedManagerId;
        while (cursor is Guid current)
        {
            if (current == userId) return true;   // السلسلة تعود إلى المستخدم نفسه ⇒ دائرة.
            if (!visited.Add(current)) return true; // دائرة قائمة في البيانات ⇒ توقّف بأمان.
            cursor = await _db.Users.AsNoTracking()
                .Where(u => u.Id == current)
                .Select(u => u.ManagerId)
                .FirstOrDefaultAsync(ct);
        }
        return false;
    }

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
