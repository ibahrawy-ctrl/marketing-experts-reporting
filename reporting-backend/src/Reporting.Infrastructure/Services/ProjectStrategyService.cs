using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.Projects360;
using Reporting.Domain.Entities.Projects360;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// استراتيجيّة المشروع (CPW-R3 · W4 · §3 · D-04).
///
/// <para>
/// **استراتيجيّة نشطة واحدة لكلّ مشروع** مفروضة بفهرس فريد **جزئيّ** على القاعدة
/// (<c>ProjectId WHERE IsActive</c>). هذه الخدمة لا تُنشئ صفًّا ثانيًا نشطًا أبدًا: الحفظ
/// يُحدِّث الصفّ النشط أو يُنشئ أوّله. **لا إصدارات في W4** — والقيد الجزئيّ يبقيها ممكنة لاحقًا
/// بلا إسقاط قيد.
/// </para>
///
/// <para>
/// **لا Hard Coding لأنواع الخدمات**: لا ذكر لـSEO/Ads/Social في هذا الملفّ ولا في الدومين.
/// الحقول المشروطة كلّها رموز كتالوج (<c>strategy_field</c>) وأقسامها رموز كتالوج
/// (<c>strategy_section</c>)، فيُضاف نوع خدمة جديد ببيانات لا بكود.
/// </para>
/// </summary>
public class ProjectStrategyService : IProjectStrategyService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IProject360Authorization _auth;
    private readonly IAuditService _audit;

    public ProjectStrategyService(AppDbContext db, ICurrentUser currentUser, IProject360Authorization auth, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _auth = auth;
        _audit = audit;
    }

    public async Task<Result<ProjectStrategyDto?>> GetAsync(Guid projectId, CancellationToken ct = default)
    {
        var scope = await _auth.LoadVisibleProjectAsync(projectId, ct);
        if (!scope.Succeeded) return Result<ProjectStrategyDto?>.Failure(scope.Error!, scope.ErrorCode);

        var entity = await _db.ProjectStrategies.AsNoTracking()
            .Include(s => s.Attributes)
            .FirstOrDefaultAsync(s => s.ProjectId == projectId && s.IsActive, ct);

        return Result<ProjectStrategyDto?>.Success(entity is null ? null : await MapAsync(entity, ct));
    }

    public async Task<Result<ProjectStrategyDto>> UpsertAsync(Guid projectId, UpsertProjectStrategyRequest request, CancellationToken ct = default)
    {
        var scope = await _auth.LoadVisibleProjectAsync(projectId, ct);
        if (!scope.Succeeded) return Result<ProjectStrategyDto>.Failure(scope.Error!, scope.ErrorCode);
        if (!await _auth.CanManageProject360Async(scope.Value!, ct))
            return Result<ProjectStrategyDto>.Failure("لا تملك صلاحية إدارة استراتيجيّة هذا المشروع.", "auth.forbidden");
        if (_currentUser.UserId is not Guid uid)
            return Result<ProjectStrategyDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var attributes = request.Attributes ?? Array.Empty<UpsertProjectStrategyAttributeRequest>();
        var validation = await ValidateAttributesAsync(attributes, scope.Value!.ServiceType, ct);
        if (validation is not null) return Result<ProjectStrategyDto>.Failure(validation.Value.Message, validation.Value.Code);

        var entity = await _db.ProjectStrategies
            .Include(s => s.Attributes)
            .FirstOrDefaultAsync(s => s.ProjectId == projectId && s.IsActive, ct);

        var created = entity is null;
        if (entity is null)
        {
            entity = new ProjectStrategy { ProjectId = projectId, IsActive = true };
            // BaseEntity يضبط Id في المُنشئ ⟹ التتبّع التلقائيّ قد يختار Modified بدل Added (درس CPWR2-DEF-01).
            _db.ProjectStrategies.Add(entity);
        }

        entity.Vision = Project360Guards.Trim(request.Vision);
        entity.StrategySummary = Project360Guards.Trim(request.StrategySummary);
        entity.TargetAudience = Project360Guards.Trim(request.TargetAudience);
        entity.CustomerPersona = Project360Guards.Trim(request.CustomerPersona);
        entity.Positioning = Project360Guards.Trim(request.Positioning);
        entity.ValueProposition = Project360Guards.Trim(request.ValueProposition);
        entity.Competitors = Project360Guards.Trim(request.Competitors);
        entity.ToneOfVoice = Project360Guards.Trim(request.ToneOfVoice);
        entity.Messaging = Project360Guards.Trim(request.Messaging);
        entity.MarketingApproach = Project360Guards.Trim(request.MarketingApproach);
        entity.SuccessFactors = Project360Guards.Trim(request.SuccessFactors);
        if (!created) entity.UpdatedAtUtc = DateTime.UtcNow;

        SyncAttributes(entity, attributes);

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, created ? "project_strategy.created" : "project_strategy.updated", nameof(ProjectStrategy), entity.Id, ct: ct);

        return Result<ProjectStrategyDto>.Success(await MapAsync(entity, ct));
    }

    public async Task<Result<ProjectStrategySchemaDto>> GetSchemaAsync(Guid projectId, CancellationToken ct = default)
    {
        var scope = await _auth.LoadVisibleProjectAsync(projectId, ct);
        if (!scope.Succeeded) return Result<ProjectStrategySchemaDto>.Failure(scope.Error!, scope.ErrorCode);

        // استعلام واحد للمجالين معًا (لا استعلام لكلّ مجال، ولا استعلام داخل حلقة).
        var catalog = await _db.ExecutionTaxonomyValues.AsNoTracking()
            .Where(v => v.IsActive &&
                        (v.Domain == Project360CatalogDomains.StrategySection ||
                         v.Domain == Project360CatalogDomains.StrategyField))
            .OrderBy(v => v.SortOrder).ThenBy(v => v.NameAr)
            .Select(v => new { v.Domain, v.Code, v.NameAr, v.SortOrder })
            .ToListAsync(ct);

        // DEC-W4-02 — الترشيح **بالبيانات**: بادئة رمز الحقل هي قسمه، والقسم إمّا عامّ
        // (يظهر دائمًا) أو محجوز لخدمة (يظهر بمطابقة ServiceType). صفر تفريع لكلّ خدمة.
        var serviceType = scope.Value!.ServiceType;

        var applicableSections = catalog
            .Where(v => v.Domain == Project360CatalogDomains.StrategySection &&
                        ProjectStrategyApplicability.SectionAppliesTo(v.Code, serviceType))
            .ToList();

        var sectionNames = applicableSections.ToDictionary(v => v.Code, v => v.NameAr, StringComparer.Ordinal);

        var sections = applicableSections
            .Select(v => new ProjectStrategySectionDto(v.Code, v.NameAr, v.SortOrder))
            .ToList();

        var dynamicFields = catalog
            .Where(v => v.Domain == Project360CatalogDomains.StrategyField &&
                        ProjectStrategyApplicability.AppliesTo(v.Code, serviceType))
            .Select(v =>
            {
                var section = ProjectStrategyApplicability.SectionOf(v.Code);
                return new ProjectStrategySchemaFieldDto(
                    v.Code, v.NameAr, false,
                    section,
                    section is null ? null : sectionNames.GetValueOrDefault(section),
                    v.SortOrder);
            })
            .ToList();

        var coreFields = ProjectStrategyCoreFields.Ordered
            .Select((f, i) => new ProjectStrategySchemaFieldDto(f.Code, f.NameAr, true, null, null, i))
            .ToList();

        return Result<ProjectStrategySchemaDto>.Success(
            new ProjectStrategySchemaDto(coreFields, dynamicFields, sections));
    }

    // ===== helpers =====

    /// <summary>
    /// تحقّق خادميّ لرموز الحقول والأقسام مقابل الكتالوج + رفض التكرار داخل الطلب نفسه.
    /// **استعلامان ثابتان** مهما بلغ عدد السمات (لا استعلام لكلّ سمة).
    /// </summary>
    private async Task<(string Message, string Code)?> ValidateAttributesAsync(
        IReadOnlyList<UpsertProjectStrategyAttributeRequest> attributes, ServiceType serviceType, CancellationToken ct)
    {
        if (attributes.Count == 0) return null;

        var fieldCodes = new List<string>(attributes.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in attributes)
        {
            var code = Project360Guards.Trim(a.FieldCode);
            if (code is null) return ("رمز الحقل الاستراتيجيّ مطلوب.", Project360ErrorCodes.StrategyFieldCodeInvalid);
            if (!seen.Add(code)) return ($"رمز الحقل «{code}» مكرَّر في الطلب.", Project360ErrorCodes.StrategyDuplicateField);
            if (Project360Guards.ValidateSortOrder(a.SortOrder) is { } sortError) return sortError;
            fieldCodes.Add(code);
        }

        var knownFields = await Project360Guards.ResolveActiveCodeNamesAsync(
            _db, Project360CatalogDomains.StrategyField, fieldCodes, ct);
        foreach (var code in fieldCodes)
        {
            if (!knownFields.ContainsKey(code))
                return ($"رمز الحقل «{code}» غير معروف أو غير نشط في كتالوج التصنيفات.", Project360ErrorCodes.StrategyFieldCodeInvalid);

            // DEC-W4-02 — رمز صحيح لكن خارج نطاق خدمة المشروع: خطأ **مختلف** عن الرمز المجهول،
            // كي تميّز الواجهة بين «لا وجود له» و«لا ينطبق هنا» بلا تخمين.
            if (!ProjectStrategyApplicability.AppliesTo(code, serviceType))
                return ($"رمز الحقل «{code}» لا ينطبق على نوع خدمة هذا المشروع.", Project360ErrorCodes.StrategyFieldNotApplicable);
        }

        var sectionCodes = attributes
            .Select(a => Project360Guards.Trim(a.SectionCode))
            .Where(c => c is not null).Select(c => c!)
            .Distinct(StringComparer.Ordinal).ToList();
        if (sectionCodes.Count > 0)
        {
            var knownSections = await Project360Guards.ResolveActiveCodeNamesAsync(
                _db, Project360CatalogDomains.StrategySection, sectionCodes, ct);
            foreach (var code in sectionCodes)
                if (!knownSections.ContainsKey(code))
                    return ($"رمز القسم «{code}» غير معروف أو غير نشط في كتالوج التصنيفات.", Project360ErrorCodes.StrategySectionCodeInvalid);
        }

        return null;
    }

    /// <summary>
    /// مزامنة تفاضليّة للسمات: تحديث الموجود، إضافة الجديد، حذف ما لم يُرسَل.
    /// الإضافة عبر <c>DbSet.Add</c> صراحةً — لا عبر مجموعة التنقّل — تفاديًا لاختيار EF حالة
    /// <c>Modified</c> لكيان جديد يحمل <c>Id</c> مضبوطًا مسبقًا (درس CPWR2-DEF-01).
    /// </summary>
    private void SyncAttributes(ProjectStrategy strategy, IReadOnlyList<UpsertProjectStrategyAttributeRequest> requested)
    {
        var incoming = requested.ToDictionary(a => a.FieldCode.Trim(), a => a, StringComparer.OrdinalIgnoreCase);
        var existing = strategy.Attributes.ToList();

        foreach (var current in existing)
        {
            if (incoming.TryGetValue(current.FieldCode, out var update))
            {
                current.SectionCode = Project360Guards.Trim(update.SectionCode);
                current.ValueText = Project360Guards.Trim(update.ValueText);
                current.SortOrder = update.SortOrder;
                current.UpdatedAtUtc = DateTime.UtcNow;
                incoming.Remove(current.FieldCode);
            }
            else
            {
                _db.ProjectStrategyAttributes.Remove(current);
            }
        }

        foreach (var added in incoming.Values)
        {
            _db.ProjectStrategyAttributes.Add(new ProjectStrategyAttribute
            {
                ProjectStrategyId = strategy.Id,
                FieldCode = added.FieldCode.Trim(),
                SectionCode = Project360Guards.Trim(added.SectionCode),
                ValueText = Project360Guards.Trim(added.ValueText),
                SortOrder = added.SortOrder,
            });
        }
    }

    private async Task<ProjectStrategyDto> MapAsync(ProjectStrategy s, CancellationToken ct)
    {
        var rows = await _db.ProjectStrategyAttributes.AsNoTracking()
            .Where(a => a.ProjectStrategyId == s.Id)
            .OrderBy(a => a.SortOrder).ThenBy(a => a.FieldCode)
            .ToListAsync(ct);

        var fieldNames = await Project360Guards.ResolveActiveCodeNamesAsync(
            _db, Project360CatalogDomains.StrategyField, rows.Select(a => a.FieldCode).Distinct().ToList(), ct);
        var sectionNames = await Project360Guards.ResolveActiveCodeNamesAsync(
            _db, Project360CatalogDomains.StrategySection,
            rows.Where(a => a.SectionCode is not null).Select(a => a.SectionCode!).Distinct().ToList(), ct);

        var attributes = rows.Select(a => new ProjectStrategyAttributeDto(
            a.FieldCode,
            fieldNames.GetValueOrDefault(a.FieldCode),
            a.SectionCode,
            a.SectionCode is string sc ? sectionNames.GetValueOrDefault(sc) : null,
            a.ValueText,
            a.SortOrder)).ToList();

        return new ProjectStrategyDto(
            s.Id, s.ProjectId, s.Vision, s.StrategySummary, s.TargetAudience, s.CustomerPersona,
            s.Positioning, s.ValueProposition, s.Competitors, s.ToneOfVoice, s.Messaging,
            s.MarketingApproach, s.SuccessFactors, attributes, s.CreatedAtUtc, s.UpdatedAtUtc);
    }
}
