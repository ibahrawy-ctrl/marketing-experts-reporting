using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.EmployeeServices;
using Reporting.Domain.Entities.EmployeeServices;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// أرصدة الإجازات والأذونات (V1.1). الرصيد مشتقّ من حركات Ledger (لا قيمة مخزّنة). يفرض الصلاحية خادميًّا:
/// الموظّف يرى رصيده فقط؛ الإدارة/HR (BalanceManagers) ترى/تُدير الجميع. لا حذف لحركة؛ التصحيح بإضافة حركة معاكسة.
/// التعديل اليدوي يستلزم سببًا إلزاميًّا ويُسجَّل في Audit بلا بيانات حساسة.
/// </summary>
public class BalanceService : IBalanceService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _audit;

    public BalanceService(AppDbContext db, ICurrentUser currentUser, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    private static readonly LeaveRequestStatus[] PendingStatuses =
    {
        LeaveRequestStatus.Submitted, LeaveRequestStatus.TeamLeaderApproved,
        LeaveRequestStatus.ManagerApproved, LeaveRequestStatus.ReturnedForEdit
    };

    public async Task<Result<MyBalancesDto>> GetMyBalancesAsync(int? year, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<MyBalancesDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var y = year ?? DateTime.UtcNow.Year;
        var entries = await _db.EmployeeBalanceLedger.AsNoTracking()
            .Where(e => e.EmployeeId == uid && e.Year == y)
            .ToListAsync(ct);

        var annual = Summarize(entries, BalanceType.AnnualLeave);
        var perm = Summarize(entries, BalanceType.Permission);

        var pending = await _db.LeaveRequests.AsNoTracking()
            .CountAsync(r => r.RequesterUserId == uid && PendingStatuses.Contains(r.Status), ct);

        var jobRoleId = await _db.Users.Where(u => u.Id == uid).Select(u => u.JobRoleId).FirstOrDefaultAsync(ct);
        var policy = await ResolvePolicyAsync(y, jobRoleId, ct);

        // عند وجود حدّ شهري للأذونات: نحسب المستخدَم والمتبقّي ضمن الشهر التقويمي الحالي (UTC) من السنة y.
        // العدّ من leave_requests (StartDate ضمن الشهر، الحالة HrApproved) ليطابق حارس الاعتماد النهائي.
        int? usedThisMonth = null;
        int? remainingThisMonth = null;
        if (policy?.PermissionMonthlyLimit is decimal monthlyLimit)
        {
            var month = DateTime.UtcNow.Month;
            var monthStart = new DateOnly(y, month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var used = await _db.LeaveRequests.AsNoTracking().CountAsync(r =>
                r.RequesterUserId == uid
                && r.Type == LeaveRequestType.Permission
                && r.Status == LeaveRequestStatus.HrApproved
                && r.StartDate >= monthStart && r.StartDate <= monthEnd, ct);
            usedThisMonth = used;
            remainingThisMonth = Math.Max(0, (int)monthlyLimit - used);
        }

        return Result<MyBalancesDto>.Success(new MyBalancesDto(
            y, annual, perm, pending,
            policy?.PermissionUnit ?? PermissionUnit.Count,
            policy?.PermissionMonthlyLimit,
            policy?.PermissionAnnualLimit,
            policy?.AllowNegativeBalance ?? true,
            policy is not null,
            usedThisMonth,
            remainingThisMonth));
    }

    public async Task<Result<IReadOnlyList<EmployeeBalanceRowDto>>> ListEmployeesAsync(
        string? q, Guid? departmentId, Guid? teamId, int? year, CancellationToken ct = default)
    {
        var y = year ?? DateTime.UtcNow.Year;

        var usersQuery = _db.Users.AsNoTracking().Where(u => u.IsActive);
        if (departmentId is Guid dep) usersQuery = usersQuery.Where(u => u.DepartmentId == dep);
        if (teamId is Guid team) usersQuery = usersQuery.Where(u => u.TeamId == team);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            usersQuery = usersQuery.Where(u => u.FullName.Contains(term) || (u.Email != null && u.Email.Contains(term)));
        }

        var users = await usersQuery
            .Select(u => new { u.Id, u.FullName, u.JobRoleId, u.DepartmentId, u.TeamId })
            .ToListAsync(ct);
        var ids = users.Select(u => u.Id).ToList();

        var ledger = await _db.EmployeeBalanceLedger.AsNoTracking()
            .Where(e => ids.Contains(e.EmployeeId) && e.Year == y)
            .ToListAsync(ct);
        var byEmployee = ledger.GroupBy(e => e.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());

        var jobRoleNames = await _db.JobRoles.AsNoTracking().ToDictionaryAsync(j => j.Id, j => j.NameAr, ct);
        var depNames = await _db.Departments.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.NameAr, ct);
        var teamNames = await _db.Teams.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.NameAr, ct);

        var rows = users.Select(u =>
        {
            var entries = byEmployee.GetValueOrDefault(u.Id, new List<EmployeeBalanceLedger>());
            var annual = Summarize(entries, BalanceType.AnnualLeave);
            var perm = Summarize(entries, BalanceType.Permission);
            return new EmployeeBalanceRowDto(
                u.Id, u.FullName,
                u.JobRoleId is Guid jr ? jobRoleNames.GetValueOrDefault(jr) : null,
                u.DepartmentId, u.DepartmentId is Guid d ? depNames.GetValueOrDefault(d) : null,
                u.TeamId, u.TeamId is Guid t ? teamNames.GetValueOrDefault(t) : null,
                y, annual.Remaining, perm.Remaining, annual.IsNegative, perm.IsNegative);
        }).OrderBy(r => r.EmployeeName).ToList();

        return Result<IReadOnlyList<EmployeeBalanceRowDto>>.Success(rows);
    }

    public async Task<Result<EmployeeLedgerDto>> GetEmployeeLedgerAsync(Guid userId, int? year, CancellationToken ct = default)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result<EmployeeLedgerDto>.Failure("الموظّف غير موجود.", "user.not_found");
        return Result<EmployeeLedgerDto>.Success(await BuildLedgerAsync(userId, user.FullName, year ?? DateTime.UtcNow.Year, ct));
    }

    public async Task<Result<EmployeeLedgerDto>> SetOpeningBalanceAsync(Guid userId, OpeningBalanceRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid actor)
            return Result<EmployeeLedgerDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (request.Amount <= 0)
            return Result<EmployeeLedgerDto>.Failure("المقدار يجب أن يكون موجبًا.", "balance.amount_invalid");

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result<EmployeeLedgerDto>.Failure("الموظّف غير موجود.", "user.not_found");

        _db.EmployeeBalanceLedger.Add(new EmployeeBalanceLedger
        {
            EmployeeId = userId,
            BalanceType = request.BalanceType,
            Amount = request.Amount,
            Direction = BalanceDirection.Credit,
            Source = BalanceSource.OpeningBalance,
            Year = request.Year,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedBy = actor
        });
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actor, "balance.opening_set", nameof(EmployeeBalanceLedger), userId,
            JsonSerializer.Serialize(new { userId, request.BalanceType, request.Amount, request.Year }), ct: ct);

        return Result<EmployeeLedgerDto>.Success(await BuildLedgerAsync(userId, user.FullName, request.Year, ct));
    }

    public async Task<Result<EmployeeLedgerDto>> AdjustAsync(Guid userId, BalanceAdjustmentRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid actor)
            return Result<EmployeeLedgerDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result<EmployeeLedgerDto>.Failure("سبب التعديل اليدوي مطلوب.", "balance.reason_required");
        if (request.Amount <= 0)
            return Result<EmployeeLedgerDto>.Failure("المقدار يجب أن يكون موجبًا.", "balance.amount_invalid");

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result<EmployeeLedgerDto>.Failure("الموظّف غير موجود.", "user.not_found");

        _db.EmployeeBalanceLedger.Add(new EmployeeBalanceLedger
        {
            EmployeeId = userId,
            BalanceType = request.BalanceType,
            Amount = request.Amount,
            Direction = request.Direction,
            Source = BalanceSource.ManualAdjustment,
            Year = request.Year,
            Notes = request.Reason.Trim(),
            CreatedBy = actor
        });
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actor, "balance.adjusted", nameof(EmployeeBalanceLedger), userId,
            JsonSerializer.Serialize(new { userId, request.BalanceType, request.Direction, request.Amount, request.Year }), ct: ct);

        return Result<EmployeeLedgerDto>.Success(await BuildLedgerAsync(userId, user.FullName, request.Year, ct));
    }

    // ===== مساعدات =====

    private async Task<BalancePolicy?> ResolvePolicyAsync(int year, Guid? jobRoleId, CancellationToken ct)
    {
        var candidates = await _db.BalancePolicies.AsNoTracking()
            .Where(p => p.Year == year && (p.JobRoleId == null || p.JobRoleId == jobRoleId))
            .ToListAsync(ct);
        // الأخصّ (لمسمّى وظيفي) يطغى على العام.
        return candidates.FirstOrDefault(p => p.JobRoleId == jobRoleId) ?? candidates.FirstOrDefault(p => p.JobRoleId == null);
    }

    private static BalanceSummaryDto Summarize(IReadOnlyList<EmployeeBalanceLedger> entries, BalanceType type)
    {
        var credited = entries.Where(e => e.BalanceType == type && e.Direction == BalanceDirection.Credit).Sum(e => e.Amount);
        var debited = entries.Where(e => e.BalanceType == type && e.Direction == BalanceDirection.Debit).Sum(e => e.Amount);
        var remaining = credited - debited;
        return new BalanceSummaryDto(type, credited, debited, remaining, remaining < 0);
    }

    private async Task<EmployeeLedgerDto> BuildLedgerAsync(Guid userId, string userName, int year, CancellationToken ct)
    {
        var entries = await _db.EmployeeBalanceLedger.AsNoTracking()
            .Where(e => e.EmployeeId == userId && e.Year == year)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToListAsync(ct);

        var actorIds = entries.Select(e => e.CreatedBy).Distinct().ToList();
        var names = actorIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users.Where(u => actorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return new EmployeeLedgerDto(
            userId, userName, year,
            Summarize(entries, BalanceType.AnnualLeave),
            Summarize(entries, BalanceType.Permission),
            entries.Select(e => new BalanceLedgerEntryDto(
                e.Id, e.BalanceType, e.Amount, e.Direction, e.Source, e.RelatedRequestId, e.Year,
                e.Notes, e.CreatedBy, names.GetValueOrDefault(e.CreatedBy), e.CreatedAtUtc)).ToList());
    }
}
