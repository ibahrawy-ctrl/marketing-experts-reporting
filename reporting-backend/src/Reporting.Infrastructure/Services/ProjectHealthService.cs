using Microsoft.EntityFrameworkCore;
using Reporting.Application.Common;
using Reporting.Application.Projects360;
using Reporting.Domain.Entities.Clients;
using Reporting.Domain.Enums;
using Reporting.Domain.Projects360;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// **الصحّة المخزَّنة للمشروع** (CPW-R3 · R2 §84 + §1074 · قرار المالك FINDING-W6-03).
///
/// <para>
/// **مصدر احتساب واحد لا غير**: كلّ رقم هنا يخرج من <see cref="ProjectHealthPolicy"/>. لا عتبة،
/// ولا وزن مكوّن، ولا معادلة تسوية في هذا الملفّ — وهو ما يمنع أن يعرض المشروع الواحد رقمين
/// متناقضين بين اللوحة والقائمة.
/// </para>
///
/// <para>
/// **ثلاثة تُكتب وواحد يُشتقّ**: تُكتب <c>HealthPercent</c> و<c>HealthStatus</c> و
/// <c>HealthComputedAtUtc</c> فقط. <c>Reasons</c> **لا تُخزَّن أبدًا** (قرار W1-A بند 1، غير منقوض)
/// بل تُشتقّ حتميًّا من نفس المكوّنات في كلّ قراءة.
/// </para>
///
/// <para>
/// **صفر هجرة**: الأعمدة الثلاثة أُنشئت في الهجرة <c>20260811142239_AddProject360Foundation</c>
/// (#33) وكانت تُقرأ ولا تُكتب. هذه الحزمة تُغلق الكتابة فقط — لا عمود ولا فهرس ولا نوع جديد.
/// </para>
///
/// <para>
/// **علامة «لم تُحتسَب بعد»**: <c>HealthComputedAtUtc = null</c> **وحدها**. لا تُقرأ
/// <c>HealthStatus</c>/<c>HealthPercent</c> على مشروع ختمه فارغ — لأنّ <c>ProjectHealthStatus</c>
/// لا يملك قيمة «غير مقيَّم»، وتوسيع التعداد كان سيستلزم تغيير بيانات ممنوعًا في هذه الحزمة.
/// </para>
///
/// <para>
/// **عدد استعلامات ثابت = 2** لكلّ احتساب مهما بلغ عدد الأهداف أو المؤشّرات: صفّ المشروع
/// (متعقَّب للكتابة) + صفوف المؤشّرات النشطة تحت أهداف نشطة مضمومة بوزن هدفها. **صفر N+1**.
/// </para>
/// </summary>
public class ProjectHealthService : IProjectHealthService
{
    private readonly AppDbContext _db;
    private readonly IProject360Authorization _auth;

    public ProjectHealthService(AppDbContext db, IProject360Authorization auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<Result<ProjectHealthDto>> RecomputeAsync(Guid projectId, CancellationToken ct = default)
    {
        // الترتيب: مصادقة ⟵ رؤية ⟵ وجود — كلّها داخل البوّابة الموحّدة، و«خارج النطاق» = «غير موجود».
        var scope = await _auth.LoadVisibleProjectAsync(projectId, ct);
        if (!scope.Succeeded) return Result<ProjectHealthDto>.Failure(scope.Error!, scope.ErrorCode);

        // إعادة الاحتساب كتابة على حالة المشروع ⟹ تتطلّب المستوى التشغيليّ (D-07)،
        // لا مجرّد الرؤية. القارئ المخوَّل بالرؤية وحدها يُرفَض بـ403 لا 404 (يرى المشروع أصلًا).
        if (!await _auth.CanUpdateProject360ProgressAsync(scope.Value!, ct))
            return Result<ProjectHealthDto>.Failure("لا تملك صلاحية إعادة احتساب صحّة هذا المشروع.", "auth.forbidden");

        var applied = await ApplyCoreAsync(projectId, ct);
        if (applied is null)
            return Result<ProjectHealthDto>.Failure("المشروع غير موجود.", Project360ErrorCodes.ProjectNotFound);

        await _db.SaveChangesAsync(ct);

        var (snapshot, project) = applied.Value;
        return Result<ProjectHealthDto>.Success(Map(snapshot, project.HealthComputedAtUtc));
    }

    public async Task SaveWithHealthAsync(Guid projectId, CancellationToken ct = default)
    {
        // معاملة واحدة تحكم الطفرة والصحّة معًا: فشل أيّهما ⟹ تراجع كامل، فلا تبقى طفرة بصحّة بائتة.
        // لا تُفتَح معاملة متداخلة إن كانت واحدة قائمة (مسار الاختبارات ومسارات مركّبة مستقبلًا).
        var owned = _db.Database.CurrentTransaction is null;
        var tx = owned ? await _db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            // (1) تثبيت الطفرة أوّلًا — استعلام التجميع يقرأ من القاعدة لا من متعقّب التغييرات.
            await _db.SaveChangesAsync(ct);
            // (2) احتساب الصحّة من الحالة **بعد** الطفرة ثمّ كتابتها.
            await ApplyCoreAsync(projectId, ct);
            await _db.SaveChangesAsync(ct);

            if (tx is not null) await tx.CommitAsync(ct);
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }
    }

    // ======================================================================
    // النواة — احتساب من الـPolicy وكتابة الأعمدة الثلاثة، بلا SaveChanges وبلا تخويل
    // ======================================================================

    /// <summary>
    /// يجمع مدخلات <see cref="ProjectHealthPolicy"/> الفعليّة ويكتب مخرجاتها الثلاثة على الكيان
    /// المتعقَّب. **لا يحفظ** — الحفظ مسؤوليّة المستدعي كي تبقى الطفرة والصحّة في وحدة عمل واحدة.
    /// يعيد <c>null</c> إن لم يوجد المشروع.
    /// </summary>
    private async Task<(ProjectHealthSnapshot Snapshot, Project Project)?> ApplyCoreAsync(Guid projectId, CancellationToken ct)
    {
        // (1) صفّ المشروع — متعقَّب لأنّه هدف الكتابة.
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return null;

        // (2) المؤشّرات النشطة تحت أهداف نشطة، مع وزن الهدف — استعلام واحد ثابت.
        // الأهداف بلا مؤشّرات محتسَبة تُستبعَد من البسط والمقام معًا بحكم ComputeWeightedScore،
        // فالضمّ من جهة المؤشّر يعطي **نفس** نتيجة مسار اللوحة بالضبط ولا يحتاج استعلامًا ثالثًا.
        var rows = await _db.ProjectKpis.AsNoTracking()
            .Where(k => k.ProjectId == projectId && k.IsActive && k.Objective!.IsActive)
            .Select(k => new
            {
                k.ObjectiveId,
                ObjectiveWeight = k.Objective!.Weight,
                k.Weight,
                k.CurrentValue,
                k.TargetValue,
                k.Direction,
            })
            .ToListAsync(ct);

        var objectiveScores = rows
            .GroupBy(r => new { r.ObjectiveId, r.ObjectiveWeight })
            .Select(g => new ProjectHealthPolicy.WeightedAchievement(
                g.Key.ObjectiveWeight,
                ProjectHealthPolicy.ComputeWeightedScore(
                    g.Select(k => new ProjectHealthPolicy.WeightedAchievement(
                        k.Weight,
                        ProjectHealthPolicy.ComputeAchievement(k.CurrentValue, k.TargetValue, k.Direction)))
                     .ToList()).Score))
            .ToList();

        // DEC-W4-03 — ترجيح على مستويين لا متوسّطًا مسطَّحًا.
        var kpiScore = ProjectHealthPolicy.RollUpObjectiveScores(objectiveScores);

        var now = DateTime.UtcNow;
        var scheduleScore = ProjectHealthPolicy.ComputeScheduleScore(
            project.StartDate, project.EndDate, DateOnly.FromDateTime(now), project.ProgressPercent, project.Status);

        var snapshot = ProjectHealthPolicy.ComputeHealth(
            kpiScore.Score, project.ProgressPercent, scheduleScore, now, kpiScore.AllWeightsZero);

        if (snapshot.IsEvaluated)
        {
            project.HealthPercent = snapshot.Score!.Value;
            project.HealthStatus = snapshot.Status;
            project.HealthComputedAtUtc = snapshot.LastEvaluatedAtUtc;
        }
        else
        {
            // «لم يُقيَّم» ≠ «صفر»: يُترك الختم فارغًا وهو العلامة الوحيدة، والعمودان يعودان
            // لقيمتهما الافتراضيّة كي لا يُقرأ رقم قديم على أنّه حكم قائم.
            project.HealthPercent = 0m;
            project.HealthStatus = ProjectHealthStatus.Green;
            project.HealthComputedAtUtc = null;
        }

        return (snapshot, project);
    }

    /// <summary>تحويل لقطة الـPolicy إلى العقد الرسميّ — **بلا أيّ إعادة احتساب**.</summary>
    internal static ProjectHealthDto Map(ProjectHealthSnapshot snapshot, DateTime? persistedAtUtc) =>
        new(snapshot.Score,
            snapshot.IsEvaluated ? snapshot.Status : null,
            snapshot.KpiScore,
            snapshot.ProgressPercent,
            snapshot.ScheduleScore,
            snapshot.Reasons.Select(r => r.Code).ToList(),
            snapshot.LastEvaluatedAtUtc,
            persistedAtUtc);
}
