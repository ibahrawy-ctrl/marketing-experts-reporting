using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.Projects360;
using Reporting.Domain.Entities.Projects360;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// تنفيذ **جسر التنفيذ الهجين** (P360-WF-R2 §10).
///
/// <para>
/// **الفكرة الحاكمة**: قبل هذه الطبقة كان تنفيذ الفريق يعيش في التقارير، والمخرَج التعاقديّ يعيش
/// في المشروع، ولا شيء بينهما إلّا أن يفتح إنسانٌ الشاشتين ويقارن بالاسم. الجسر يستبدل تلك
/// المقارنة اليدويّة بارتباطٍ **بالمعرّف**، ويُبقي القرار بشريًّا: الادّعاء من المنفِّذ، والحسم
/// من المسؤول عن المشروع.
/// </para>
///
/// <para>
/// **الكتابة تقع في مكان واحد**: عند القبول فقط، وداخل <c>SaveWithHealthAsync</c> — نفس المعاملة
/// التي تكتب المخرَج تعيد احتساب تقدّم المشروع وصحّته. لا مسار ثانٍ يكتب على المخرَج من هنا.
/// </para>
/// </summary>
public class ProjectExecutionBridgeService : IProjectExecutionBridgeService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IProject360Authorization _auth;
    private readonly IProjectHealthService _health;
    private readonly IAuditService _audit;

    public ProjectExecutionBridgeService(
        AppDbContext db,
        ICurrentUser currentUser,
        IProject360Authorization auth,
        IProjectHealthService health,
        IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _auth = auth;
        _health = health;
        _audit = audit;
    }

    public async Task<Result<IReadOnlyList<ProjectExecutionProposalDto>>> ListAsync(
        Guid projectId,
        ExecutionProposalStatus? status = null,
        Guid? deliverableId = null,
        CancellationToken ct = default)
    {
        var scope = await _auth.LoadVisibleProjectAsync(projectId, ct);
        if (!scope.Succeeded)
            return Result<IReadOnlyList<ProjectExecutionProposalDto>>.Failure(scope.Error!, scope.ErrorCode);

        var canReview = await _auth.CanUpdateProject360ProgressAsync(scope.Value!, ct);
        var dtos = await BuildAsync(projectId, status, deliverableId, proposalId: null, canReview, ct);
        return Result<IReadOnlyList<ProjectExecutionProposalDto>>.Success(dtos);
    }

    public async Task<Result<ProjectExecutionProposalDto>> CreateAsync(
        Guid projectId, CreateProjectExecutionProposalRequest request, CancellationToken ct = default)
    {
        // **الرؤية وحدها تكفي للرفع**: المنفِّذ يعرف ما أنجزه ولا يملك حقّ الكتابة على المخرَج،
        // وإلزامه بحارس الكتابة كان سيُفرِغ «المقترح» من معناه ويُبقي التنفيذ خارج المشروع.
        var scope = await _auth.LoadVisibleProjectAsync(projectId, ct);
        if (!scope.Succeeded) return Result<ProjectExecutionProposalDto>.Failure(scope.Error!, scope.ErrorCode);
        if (_currentUser.UserId is not Guid uid)
            return Result<ProjectExecutionProposalDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var invalid = Project360Guards.ValidatePercent(request.ProposedProgressPercent, "نسبة التنفيذ المقترحة");
        if (invalid is not null) return Result<ProjectExecutionProposalDto>.Failure(invalid.Value.Message, invalid.Value.Code);

        var deliverable = await _db.ProjectDeliverables.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == request.DeliverableId && d.ProjectId == projectId, ct);
        if (deliverable is null)
            return Result<ProjectExecutionProposalDto>.Failure(
                "المخرَج التعاقديّ غير موجود ضمن هذا المشروع.", Project360ErrorCodes.DeliverableNotFound);
        if (!deliverable.IsActive)
            return Result<ProjectExecutionProposalDto>.Failure(
                "لا يمكن رفع تنفيذ على مخرَج معطَّل.", Project360ErrorCodes.ProposalDeliverableInactive);

        // التسليم المُستشهَد به يجب أن يكون من **نفس المشروع** — وإلّا صار الجسر منفذًا لعبور
        // الأدلّة بين المشاريع، وهو نفس الخطر الذي يحرسه <c>ValidateLinksAsync</c> في المخرَجات.
        if (request.SubmissionId is Guid sid)
        {
            var ok = await _db.ReportSubmissions.AsNoTracking()
                .AnyAsync(s => s.Id == sid && s.ProjectId == projectId, ct);
            if (!ok)
                return Result<ProjectExecutionProposalDto>.Failure(
                    "التسليم المرتبط ليس من نفس المشروع.", Project360ErrorCodes.ProposalSubmissionMismatch);
        }

        var entity = new ProjectExecutionUpdateProposal
        {
            ProjectId = projectId,
            DeliverableId = deliverable.Id,
            SubmissionId = request.SubmissionId,
            ProposedById = uid,
            ProposedProgressPercent = request.ProposedProgressPercent,
            ProposedStatus = request.ProposedStatus,
            ExecutionNote = Project360Guards.Trim(request.ExecutionNote),
            Status = ExecutionProposalStatus.Pending,
        };
        _db.ProjectExecutionUpdateProposals.Add(entity);

        // **لا `SaveWithHealthAsync` هنا**: المقترح المعلَّق لا يمسّ رقمًا واحدًا من أرقام المشروع.
        // إعادة احتساب الصحّة عند الرفع كانت ستوحي بأنّ الادّعاء أثّر قبل أن يُقَرّ.
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "project_execution_proposal.created",
            nameof(ProjectExecutionUpdateProposal), entity.Id, ct: ct);

        return await ReloadAsync(projectId, entity.Id, scope.Value!, ct);
    }

    public async Task<Result<ProjectExecutionProposalDto>> ReviewAsync(
        Guid projectId, Guid proposalId, ReviewProjectExecutionProposalRequest request, CancellationToken ct = default)
    {
        var scope = await _auth.LoadVisibleProjectAsync(projectId, ct);
        if (!scope.Succeeded) return Result<ProjectExecutionProposalDto>.Failure(scope.Error!, scope.ErrorCode);
        if (!await _auth.CanUpdateProject360ProgressAsync(scope.Value!, ct))
            return Result<ProjectExecutionProposalDto>.Failure(
                "لا تملك صلاحية مراجعة تنفيذ هذا المشروع.", "auth.forbidden");
        if (_currentUser.UserId is not Guid uid)
            return Result<ProjectExecutionProposalDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var proposal = await _db.ProjectExecutionUpdateProposals
            .FirstOrDefaultAsync(p => p.Id == proposalId && p.ProjectId == projectId, ct);
        if (proposal is null)
            return Result<ProjectExecutionProposalDto>.Failure(
                "مقترح التنفيذ غير موجود ضمن هذا المشروع.", Project360ErrorCodes.ProposalNotFound);

        var reviewNote = Project360Guards.Trim(request.ReviewNote);
        if (!request.Accept && reviewNote is null)
            return Result<ProjectExecutionProposalDto>.Failure(
                "سبب الرفض مطلوب.", Project360ErrorCodes.ProposalRejectReasonRequired);

        var decided = request.Accept ? ExecutionProposalStatus.Accepted : ExecutionProposalStatus.Rejected;

        // **حارس التعادليّة**: مقترح محسوم لا يُطبَّق ثانية. تكرار **نفس** القرار يعيد النتيجة
        // نفسها نجاحًا (شبكة تعيد الإرسال، لا مستخدم يقرّر مرّتين)، أمّا قرار **مخالف** فانقلابٌ
        // في الحكم لا تكرار له، ويُردّ صراحةً بدل أن يُطبَّق بأثر مزدوج على المخرَج.
        if (proposal.Status != ExecutionProposalStatus.Pending)
        {
            return proposal.Status == decided
                ? await ReloadAsync(projectId, proposal.Id, scope.Value!, ct)
                : Result<ProjectExecutionProposalDto>.Failure(
                    "هذا المقترح حُسِم من قبل ولا يمكن عكس قراره.", Project360ErrorCodes.ProposalAlreadyReviewed);
        }

        proposal.Status = decided;
        proposal.ReviewedById = uid;
        proposal.ReviewedAtUtc = DateTime.UtcNow;
        proposal.ReviewNote = reviewNote;
        proposal.UpdatedAtUtc = DateTime.UtcNow;

        if (!request.Accept)
        {
            // الرفض لا يمسّ المخرَج ⟹ لا إعادة احتساب: لا مدخل من مدخلات الصحّة تغيّر.
            await _db.SaveChangesAsync(ct);
            await _audit.LogAsync(uid, "project_execution_proposal.rejected",
                nameof(ProjectExecutionUpdateProposal), proposal.Id, ct: ct);
            return await ReloadAsync(projectId, proposal.Id, scope.Value!, ct);
        }

        var deliverable = await _db.ProjectDeliverables
            .FirstOrDefaultAsync(d => d.Id == proposal.DeliverableId && d.ProjectId == projectId, ct);
        if (deliverable is null)
            return Result<ProjectExecutionProposalDto>.Failure(
                "المخرَج التعاقديّ غير موجود ضمن هذا المشروع.", Project360ErrorCodes.DeliverableNotFound);
        if (!deliverable.IsActive)
            return Result<ProjectExecutionProposalDto>.Failure(
                "لا يمكن تطبيق تنفيذ على مخرَج معطَّل.", Project360ErrorCodes.ProposalDeliverableInactive);

        // اللقطة تُؤخَذ **قبل** الكتابة: بها وحدها يصير الأثر قابلًا للمراجعة وللعكس اليدويّ.
        proposal.PreviousProgressPercent = deliverable.ProgressPercent;

        deliverable.ProgressPercent = proposal.ProposedProgressPercent;
        deliverable.Status = proposal.ProposedStatus;
        if (proposal.ProposedStatus == ProjectDeliverableStatus.Delivered)
            deliverable.DeliveredAtUtc ??= DateTime.UtcNow;
        deliverable.UpdatedAtUtc = DateTime.UtcNow;

        // **نقطة الوصل كلّها**: معاملة واحدة تحمل حسم المقترح وكتابة المخرَج وإعادة احتساب
        // تقدّم المشروع وصحّته. فشل أيّ جزء يُرجِع الثلاثة معًا فلا يبقى مقترح مقبول بلا أثر.
        await _health.SaveWithHealthAsync(projectId, ct);
        await _audit.LogAsync(uid, "project_execution_proposal.accepted",
            nameof(ProjectExecutionUpdateProposal), proposal.Id, ct: ct);

        return await ReloadAsync(projectId, proposal.Id, scope.Value!, ct);
    }

    // ===== helpers =====

    private async Task<Result<ProjectExecutionProposalDto>> ReloadAsync(
        Guid projectId, Guid proposalId, Project360Context scope, CancellationToken ct)
    {
        var canReview = await _auth.CanUpdateProject360ProgressAsync(scope, ct);
        var dtos = await BuildAsync(projectId, status: null, deliverableId: null, proposalId, canReview, ct);
        return dtos.Count == 0
            ? Result<ProjectExecutionProposalDto>.Failure(
                "مقترح التنفيذ غير موجود ضمن هذا المشروع.", Project360ErrorCodes.ProposalNotFound)
            : Result<ProjectExecutionProposalDto>.Success(dtos[0]);
    }

    /// <summary>
    /// **ثلاثة استعلامات ثابتة** مهما بلغ عدد المقترحات (§15): المقترحات مع المخرَج بضمّ واحد،
    /// ثمّ أسماء الأشخاص دفعةً واحدة.
    /// </summary>
    private async Task<List<ProjectExecutionProposalDto>> BuildAsync(
        Guid projectId,
        ExecutionProposalStatus? status,
        Guid? deliverableId,
        Guid? proposalId,
        bool canReview,
        CancellationToken ct)
    {
        var query = _db.ProjectExecutionUpdateProposals.AsNoTracking()
            .Where(p => p.ProjectId == projectId);

        if (proposalId is Guid pid) query = query.Where(p => p.Id == pid);
        if (status is ExecutionProposalStatus s) query = query.Where(p => p.Status == s);
        if (deliverableId is Guid did) query = query.Where(p => p.DeliverableId == did);

        var rows = await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new
            {
                Proposal = p,
                DeliverableName = p.Deliverable!.Name,
                DeliverableProgress = p.Deliverable!.ProgressPercent,
                DeliverableStatus = p.Deliverable!.Status,
            })
            .ToListAsync(ct);
        if (rows.Count == 0) return new List<ProjectExecutionProposalDto>();

        var personIds = rows
            .SelectMany(r => new[] { (Guid?)r.Proposal.ProposedById, r.Proposal.ReviewedById })
            .Where(x => x is not null).Select(x => x!.Value).Distinct().ToList();

        var names = await _db.Users.AsNoTracking()
            .Where(u => personIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return rows.Select(r => new ProjectExecutionProposalDto(
            r.Proposal.Id,
            r.Proposal.ProjectId,
            r.Proposal.DeliverableId,
            r.DeliverableName,
            r.DeliverableProgress,
            r.DeliverableStatus,
            r.Proposal.SubmissionId,
            r.Proposal.ProposedById,
            names.GetValueOrDefault(r.Proposal.ProposedById),
            r.Proposal.ProposedProgressPercent,
            r.Proposal.ProposedStatus,
            r.Proposal.ExecutionNote,
            r.Proposal.Status,
            r.Proposal.ReviewedById,
            r.Proposal.ReviewedById is Guid rid ? names.GetValueOrDefault(rid) : null,
            r.Proposal.ReviewedAtUtc,
            r.Proposal.ReviewNote,
            r.Proposal.PreviousProgressPercent,
            r.Proposal.CreatedAtUtc,
            // القدرة تُحسَب مرّة للمشروع لا لكلّ صفّ: الحارس مشروعيّ لا مقترحيّ.
            // **الحسم متاح للمعلَّق وحده** — إظهار زرّ على محسومٍ وعدٌ سيردّه الخادم.
            canReview && r.Proposal.Status == ExecutionProposalStatus.Pending)).ToList();
    }
}
