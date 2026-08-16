using Microsoft.EntityFrameworkCore;
using Reporting.Application.Leave;
using Reporting.Domain.Entities.EmployeeServices;
using Reporting.Domain.Entities.Leave;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// تنفيذ دورة حياة رصيد الإجازات/الأذونات (LEAVE-DEDUCTION-ON-TL-APPROVAL-R1).
///
/// عقد المعاملة: لا تُستدعى SaveChanges هنا إطلاقًا — تُدرَج الحركة في متتبّع التغيير فقط، ويملك
/// المعاملةَ مستدعيها فيحفظ تغيير الحالة والحركة معًا في SaveChanges واحد ⇒ ذرّيّة تامّة.
///
/// منع الازدواج بطبقتين: (1) فحص تطبيقيّ يشمل صفوف القاعدة **و** الصفوف المُدرَجة محلّيًّا ولم تُحفظ بعد
/// (Local) لصدّ الاستدعاء المزدوج داخل المعاملة الواحدة؛ (2) الفهرس الفريد الجزئيّ
/// (RelatedRequestId, Source) في القاعدة وهو الضمان النهائيّ ضدّ التزامن بين معاملتين.
/// </summary>
public class LeaveBalanceLifecycleService : ILeaveBalanceLifecycleService
{
    private static readonly BalanceSource[] DebitSources =
    {
        BalanceSource.ApprovedLeave, BalanceSource.ApprovedPermission
    };

    private readonly AppDbContext _db;

    public LeaveBalanceLifecycleService(AppDbContext db) => _db = db;

    public async Task<LeaveBalanceLifecycleOutcome> ApplyDebitOnTeamLeaderApprovalAsync(
        LeaveRequest entity, Guid actor, CancellationToken ct = default)
    {
        var (balanceType, source) = entity.Type == LeaveRequestType.Leave
            ? (BalanceType.AnnualLeave, BalanceSource.ApprovedLeave)
            : (BalanceType.Permission, BalanceSource.ApprovedPermission);

        if (await HasEntryAsync(entity.Id, source, ct))
            return LeaveBalanceLifecycleOutcome.AlreadyApplied;

        // إجازة = عدد الأيّام شاملًا الطرفين؛ إذن = وحدة واحدة (V1.1 تعتمد Count=1).
        var amount = entity.Type == LeaveRequestType.Leave
            ? (entity.EndDate.DayNumber - entity.StartDate.DayNumber) + 1
            : 1;

        _db.EmployeeBalanceLedger.Add(new EmployeeBalanceLedger
        {
            EmployeeId = entity.RequesterUserId,
            BalanceType = balanceType,
            Amount = amount,
            Direction = BalanceDirection.Debit,
            Source = source,
            RelatedRequestId = entity.Id,
            Year = entity.StartDate.Year,
            CreatedBy = actor
        });

        return LeaveBalanceLifecycleOutcome.Created;
    }

    public async Task<LeaveBalanceLifecycleOutcome> ReverseDebitAsync(
        LeaveRequest entity, Guid actor, string reason, CancellationToken ct = default)
    {
        if (await HasEntryAsync(entity.Id, BalanceSource.Reversal, ct))
            return LeaveBalanceLifecycleOutcome.AlreadyApplied;

        var original = await FindDebitAsync(entity.Id, ct);
        if (original is null)
            return LeaveBalanceLifecycleOutcome.NoDebitToReverse;

        _db.EmployeeBalanceLedger.Add(new EmployeeBalanceLedger
        {
            EmployeeId = original.EmployeeId,
            BalanceType = original.BalanceType,
            Amount = original.Amount,
            Direction = BalanceDirection.Credit,
            Source = BalanceSource.Reversal,
            RelatedRequestId = entity.Id,
            Year = original.Year,
            Notes = reason,
            CreatedBy = actor
        });

        return LeaveBalanceLifecycleOutcome.Created;
    }

    public async Task<LeaveBalanceLifecycleState> GetCurrentBalanceLifecycleStateAsync(
        Guid leaveRequestId, CancellationToken ct = default)
    {
        var rows = await _db.EmployeeBalanceLedger.AsNoTracking()
            .Where(e => e.RelatedRequestId == leaveRequestId)
            .ToListAsync(ct);

        var debit = rows.FirstOrDefault(e => e.Direction == BalanceDirection.Debit && DebitSources.Contains(e.Source));
        var reversal = rows.FirstOrDefault(e => e.Source == BalanceSource.Reversal);

        return new LeaveBalanceLifecycleState(
            HasDebit: debit is not null,
            HasReversal: reversal is not null,
            DebitAmount: debit?.Amount ?? 0m,
            ReversalAmount: reversal?.Amount ?? 0m,
            BalanceType: debit?.BalanceType ?? reversal?.BalanceType,
            Year: debit?.Year ?? reversal?.Year);
    }

    /// <summary>يفحص القاعدة والصفوف المُدرَجة محلّيًّا (غير المحفوظة) معًا — منع ازدواج داخل المعاملة الواحدة.</summary>
    private async Task<bool> HasEntryAsync(Guid requestId, BalanceSource source, CancellationToken ct)
    {
        var pending = _db.EmployeeBalanceLedger.Local
            .Any(e => e.RelatedRequestId == requestId && e.Source == source);
        if (pending) return true;

        return await _db.EmployeeBalanceLedger
            .AnyAsync(e => e.RelatedRequestId == requestId && e.Source == source, ct);
    }

    /// <summary>يجد الخصم الأصليّ للطلب من القاعدة أو من الإدراج المحلّيّ (خصم ثمّ عكس في المعاملة نفسها).</summary>
    private async Task<EmployeeBalanceLedger?> FindDebitAsync(Guid requestId, CancellationToken ct)
    {
        var local = _db.EmployeeBalanceLedger.Local.FirstOrDefault(e =>
            e.RelatedRequestId == requestId
            && e.Direction == BalanceDirection.Debit
            && DebitSources.Contains(e.Source));
        if (local is not null) return local;

        return await _db.EmployeeBalanceLedger
            .Where(e => e.RelatedRequestId == requestId
                && e.Direction == BalanceDirection.Debit
                && (e.Source == BalanceSource.ApprovedLeave || e.Source == BalanceSource.ApprovedPermission))
            .FirstOrDefaultAsync(ct);
    }
}
