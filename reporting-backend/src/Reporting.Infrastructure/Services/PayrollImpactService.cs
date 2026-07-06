using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.Payroll;
using Reporting.Domain.Entities.Leave;
using Reporting.Domain.Entities.Payroll;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// «عرض التأثير على الرواتب» (FIN-L1). يقرأ على مستوى الشركة (بلا ScopeResolver) طلبات الإجازة/الاستئذان
/// المؤثّرة على الراتب (لقطة الرصيد من حارس الرصيد/ESS-L2) ويربطها بحالة مراجعة مالية اختيارية. المراجعة
/// إعلامية بحتة: لا تعدّل LeaveRequest ولا حالته ولا تُجري خصمًا آليًّا. الطلب المؤثّر بلا صفّ مراجعة = Pending.
/// الحماية عبر سياستَي PayrollImpactRead (قراءة) / PayrollImpactManage (تحديث المراجعة) عند نقطة النهاية،
/// مع فحص دفاعي ثانٍ في الخدمة لتحديث المراجعة.
/// </summary>
public class PayrollImpactService : IPayrollImpactService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _audit;

    public PayrollImpactService(AppDbContext db, ICurrentUser currentUser, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    private bool CanManage => _currentUser.IsInAnyRole(Roles.PayrollImpactManagers);

    public async Task<Result<PayrollImpactListDto>> ListAsync(PayrollImpactFilter filter, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null)
            return Result<PayrollImpactListDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        // طلبات مؤثّرة على الراتب + إدارة/فريق الموظّف من جدول المستخدمين (بلا ScopeResolver — على مستوى الشركة).
        var query =
            from lr in _db.LeaveRequests.AsNoTracking()
            join u in _db.Users.AsNoTracking() on lr.RequesterUserId equals u.Id
            where lr.IsPotentialUnpaidLeave
                  || (lr.UncoveredLeaveDays != null && lr.UncoveredLeaveDays > 0)
                  || lr.PermissionShortfallResolution == PermissionShortfallResolution.CompensateAfterHours
                  || lr.PermissionShortfallResolution == PermissionShortfallResolution.AdminOrPayrollReview
            select new { Lr = lr, u.DepartmentId, u.TeamId };

        // فلتر حالة الاعتماد: حالة صريحة ⟶ تُطبَّق؛ وإلا كل الحالات إن AllApprovalStatuses؛ وإلا HrApproved فقط.
        if (filter.ApprovalStatus is LeaveRequestStatus st)
            query = query.Where(x => x.Lr.Status == st);
        else if (!filter.AllApprovalStatuses)
            query = query.Where(x => x.Lr.Status == LeaveRequestStatus.HrApproved);

        if (filter.Year is int y) query = query.Where(x => x.Lr.StartDate.Year == y);
        if (filter.Month is int m) query = query.Where(x => x.Lr.StartDate.Month == m);
        if (filter.EmployeeUserId is Guid eu) query = query.Where(x => x.Lr.RequesterUserId == eu);
        if (filter.DepartmentId is Guid d) query = query.Where(x => x.DepartmentId == d);
        if (filter.TeamId is Guid t) query = query.Where(x => x.TeamId == t);
        if (filter.Type is LeaveRequestType lt) query = query.Where(x => x.Lr.Type == lt);

        var rows = await query.OrderByDescending(x => x.Lr.StartDate).ThenByDescending(x => x.Lr.CreatedAtUtc).ToListAsync(ct);

        var leaveIds = rows.Select(r => r.Lr.Id).ToList();
        var reviews = await _db.PayrollImpactReviews.AsNoTracking()
            .Where(r => leaveIds.Contains(r.LeaveRequestId))
            .ToDictionaryAsync(r => r.LeaveRequestId, r => r, ct);

        var deptNames = await DepartmentNamesAsync(rows.Select(r => r.DepartmentId), ct);
        var teamNames = await TeamNamesAsync(rows.Select(r => r.TeamId), ct);
        var userNames = await UserNamesAsync(
            rows.Select(r => (Guid?)r.Lr.RequesterUserId)
                .Concat(reviews.Values.Select(v => v.ReviewedByUserId)), ct);

        var items = rows.Select(r =>
        {
            reviews.TryGetValue(r.Lr.Id, out var review);
            return MapItem(r.Lr, r.DepartmentId, r.TeamId, review, deptNames, teamNames, userNames);
        }).ToList();

        // فلاتر مشتقّة (محسوبة بعد الجلب لأنها لا تُخزَّن): نوع التأثير + حالة المراجعة المالية.
        if (filter.ImpactType is PayrollImpactType it) items = items.Where(i => i.ImpactType == it).ToList();
        if (filter.ReviewStatus is PayrollImpactReviewStatus rs) items = items.Where(i => i.ReviewStatus == rs).ToList();

        var summary = new PayrollImpactSummaryDto(
            TotalImpacted: items.Count,
            TotalUncoveredLeaveDays: items.Where(i => i.Type == LeaveRequestType.Leave).Sum(i => i.UncoveredLeaveDays ?? 0m),
            TotalImpactedPermissions: items.Count(i => i.Type == LeaveRequestType.Permission),
            AfterHoursCompensationRequests: items.Count(i => i.PermissionShortfallResolution == PermissionShortfallResolution.CompensateAfterHours),
            NeedsFinanceReviewCount: items.Count(i => i.ReviewStatus is PayrollImpactReviewStatus.Pending or PayrollImpactReviewStatus.NeedsReview));

        return Result<PayrollImpactListDto>.Success(new PayrollImpactListDto(summary, items));
    }

    public async Task<Result<PayrollImpactDetailDto>> GetByIdAsync(Guid leaveRequestId, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null)
            return Result<PayrollImpactDetailDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var detail = await BuildDetailAsync(leaveRequestId, ct);
        return detail is null
            ? Result<PayrollImpactDetailDto>.Failure("الطلب المؤثّر على الراتب غير موجود.", "payroll_impact.not_found")
            : Result<PayrollImpactDetailDto>.Success(detail);
    }

    public async Task<Result<PayrollImpactDetailDto>> ReviewAsync(Guid leaveRequestId, PayrollImpactReviewRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<PayrollImpactDetailDto>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (!CanManage)
            return Result<PayrollImpactDetailDto>.Failure("لا تملك صلاحية تحديث المراجعة المالية.", "auth.forbidden");

        // يجب أن يكون الطلب مؤثّرًا على الراتب فعلًا (لا نُنشئ مراجعةً لطلب غير مؤثّر).
        var lr = await _db.LeaveRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == leaveRequestId, ct);
        if (lr is null || !IsImpacted(lr))
            return Result<PayrollImpactDetailDto>.Failure("الطلب المؤثّر على الراتب غير موجود.", "payroll_impact.not_found");

        // إنشاء كسول أو تحديث صفّ المراجعة (لا يمسّ LeaveRequest إطلاقًا).
        var review = await _db.PayrollImpactReviews.FirstOrDefaultAsync(r => r.LeaveRequestId == leaveRequestId, ct);
        if (review is null)
        {
            review = new PayrollImpactReview { LeaveRequestId = leaveRequestId };
            _db.PayrollImpactReviews.Add(review);
        }
        review.Status = request.Status;
        review.FinanceNote = string.IsNullOrWhiteSpace(request.FinanceNote) ? null : request.FinanceNote.Trim();
        review.ReviewedByUserId = uid;
        review.ReviewedAtUtc = DateTime.UtcNow;
        review.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "payroll_impact.review.updated", nameof(PayrollImpactReview), review.Id,
            JsonSerializer.Serialize(new { leaveRequestId, status = request.Status.ToString() }), ct: ct);

        return Result<PayrollImpactDetailDto>.Success((await BuildDetailAsync(leaveRequestId, ct))!);
    }

    // ===== مساعدات =====

    private static bool IsImpacted(LeaveRequest lr) =>
        lr.IsPotentialUnpaidLeave
        || (lr.UncoveredLeaveDays is decimal u && u > 0)
        || lr.PermissionShortfallResolution is PermissionShortfallResolution.CompensateAfterHours
            or PermissionShortfallResolution.AdminOrPayrollReview;

    private static PayrollImpactType ClassifyImpact(LeaveRequest lr) =>
        lr.Type == LeaveRequestType.Permission
            ? (lr.PermissionShortfallResolution == PermissionShortfallResolution.CompensateAfterHours
                ? PayrollImpactType.PermissionAfterHoursCompensation
                : PayrollImpactType.PermissionAdminOrPayrollReview)
            : PayrollImpactType.UnpaidLeave;

    private async Task<PayrollImpactDetailDto?> BuildDetailAsync(Guid leaveRequestId, CancellationToken ct)
    {
        var row = await (
            from lr in _db.LeaveRequests.AsNoTracking()
            join u in _db.Users.AsNoTracking() on lr.RequesterUserId equals u.Id
            where lr.Id == leaveRequestId
            select new { Lr = lr, u.DepartmentId, u.TeamId }).FirstOrDefaultAsync(ct);
        if (row is null || !IsImpacted(row.Lr)) return null;

        var review = await _db.PayrollImpactReviews.AsNoTracking().FirstOrDefaultAsync(r => r.LeaveRequestId == leaveRequestId, ct);
        var deptNames = await DepartmentNamesAsync(new[] { row.DepartmentId }, ct);
        var teamNames = await TeamNamesAsync(new[] { row.TeamId }, ct);
        var ids = new List<Guid?> { row.Lr.RequesterUserId, review?.ReviewedByUserId };
        var userNames = await UserNamesAsync(ids, ct);

        var item = MapItem(row.Lr, row.DepartmentId, row.TeamId, review, deptNames, teamNames, userNames);
        return new PayrollImpactDetailDto(item, row.Lr.Reason, row.Lr.Notes, CanManage);
    }

    private static PayrollImpactListItemDto MapItem(
        LeaveRequest lr, Guid? departmentId, Guid? teamId, PayrollImpactReview? review,
        Dictionary<Guid, string> deptNames, Dictionary<Guid, string> teamNames, Dictionary<Guid, string> userNames) =>
        new(
            lr.Id,
            lr.RequesterUserId,
            userNames.GetValueOrDefault(lr.RequesterUserId, string.Empty),
            departmentId,
            departmentId is Guid d ? deptNames.GetValueOrDefault(d) : null,
            teamId,
            teamId is Guid t ? teamNames.GetValueOrDefault(t) : null,
            lr.Type,
            ClassifyImpact(lr),
            lr.StartDate,
            lr.EndDate,
            lr.StartTime,
            lr.EndTime,
            lr.Status,
            lr.BalanceAtRequest,
            lr.RequestedLeaveDays,
            lr.UncoveredLeaveDays,
            lr.IsPotentialUnpaidLeave,
            lr.EmployeeAcknowledgedUnpaidDeduction,
            lr.EmployeeAcknowledgedAtUtc,
            lr.PermissionShortfallResolution,
            review?.Status ?? PayrollImpactReviewStatus.Pending,
            review?.FinanceNote,
            review?.ReviewedByUserId,
            review?.ReviewedByUserId is Guid rb ? userNames.GetValueOrDefault(rb) : null,
            review?.ReviewedAtUtc,
            lr.CreatedAtUtc);

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var distinct = ids.Where(i => i is Guid g && g != Guid.Empty).Select(i => i!.Value).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Users.Where(u => distinct.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    private async Task<Dictionary<Guid, string>> DepartmentNamesAsync(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var distinct = ids.Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Departments.Where(d => distinct.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.NameAr, ct);
    }

    private async Task<Dictionary<Guid, string>> TeamNamesAsync(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var distinct = ids.Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Teams.Where(t => distinct.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.NameAr, ct);
    }
}
