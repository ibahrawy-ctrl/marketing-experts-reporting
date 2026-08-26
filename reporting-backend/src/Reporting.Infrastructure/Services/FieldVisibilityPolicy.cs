using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Application.Common;
using Reporting.Application.Security;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// تنفيذ طبقة الرؤية على مستوى الحقل (P2-SEC-001).
/// يحلّ العلاقة من <see cref="IScopeResolver"/> وشجرة <c>ManagerId</c>، ثمّ يفوّض القرار
/// إلى المصفوفة النقيّة <see cref="FieldVisibilityRules"/>، ويكتب أثرًا تدقيقيًّا للتصنيفات الحسّاسة.
/// </summary>
public class FieldVisibilityPolicy : IFieldVisibilityPolicy
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IScopeResolver _scope;
    private readonly IAuditService _audit;

    public FieldVisibilityPolicy(AppDbContext db, ICurrentUser currentUser, IScopeResolver scope, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _scope = scope;
        _audit = audit;
    }

    public async Task<FieldVisibilityContext> BuildContextAsync(
        Guid subjectUserId, string? purpose = null, CancellationToken ct = default)
    {
        var viewerId = _currentUser.UserId ?? Guid.Empty;
        var roles = _currentUser.Roles;
        var permissions = _currentUser.Permissions;

        if (viewerId == Guid.Empty || !_currentUser.IsAuthenticated)
            return new FieldVisibilityContext(viewerId, subjectUserId, roles, SubjectRelation.None, permissions, purpose);

        var relation = await ResolveRelationAsync(viewerId, subjectUserId, ct);
        return new FieldVisibilityContext(viewerId, subjectUserId, roles, relation, permissions, purpose);
    }

    /// <summary>
    /// يحسب العلاقة الفعليّة: نفسه ← مرؤوس مباشر ← داخل شجرة الإدارة ← رؤية شركة ← خارج النطاق.
    /// الموضوع غير الموجود يُعامَل <see cref="SubjectRelation.None"/> كي لا يفرّق المهاجم بين
    /// «غير موجود» و«خارج نطاقي».
    /// </summary>
    private async Task<SubjectRelation> ResolveRelationAsync(Guid viewerId, Guid subjectId, CancellationToken ct)
    {
        if (viewerId == subjectId) return SubjectRelation.Self;

        var subject = await _db.Users.AsNoTracking()
            .Where(u => u.Id == subjectId)
            .Select(u => new { u.Id, u.ManagerId })
            .FirstOrDefaultAsync(ct);
        if (subject is null) return SubjectRelation.None;

        var scope = await _scope.ResolveAsync(ct);

        // وظيفة الموارد البشريّة مؤسّسيّة بطبيعتها في مصفوفة §7، بينما ScopeResolver القائم
        // يُسقِط دور HR إلى نطاق "own" (RoleAccess.ScopeTypeFor). لا نُعدّل ScopeResolver كي لا
        // يتغيّر سلوك أيّ شاشة قائمة؛ نوسّع **العلاقة** هنا فقط، داخل طبقة المرحلة الثانية.
        // التوسيع لا يفتح شيئًا حسّاسًا: الدرجات الحسّاسة تبقى محكومة بإذن صريح لا بالدور.
        var hrOrganizationWide = _currentUser.IsInRole(Roles.Hr);

        if (!hrOrganizationWide && !scope.Contains(subjectId)) return SubjectRelation.None;

        if (subject.ManagerId == viewerId) return SubjectRelation.DirectTeam;
        return scope.SeesAll || hrOrganizationWide
            ? SubjectRelation.Company
            : SubjectRelation.Department;
    }

    public bool CanSee(FieldVisibilityContext ctx, FieldSensitivity sensitivity) =>
        FieldVisibilityRules.CanSee(ctx, sensitivity);

    public bool CanSeeSection(FieldVisibilityContext ctx, Employee360Section section) =>
        FieldVisibilityRules.CanSeeSection(ctx, section);

    public async Task<bool> CanSeeAsync(
        FieldVisibilityContext ctx, FieldSensitivity sensitivity, string fieldKey, CancellationToken ct = default)
    {
        var allowed = FieldVisibilityRules.CanSee(ctx, sensitivity);

        // أثر تدقيقيّ للوصول الفعليّ إلى الحقول المصنّفة حسّاسة — **بلا قيمة الحقل** إطلاقًا.
        if (allowed && FieldVisibilityRules.IsAuditable(sensitivity))
        {
            var payload = JsonSerializer.Serialize(new
            {
                subjectUserId = ctx.SubjectUserId,
                fieldKey,
                sensitivity = sensitivity.ToString(),
                purpose = ctx.Purpose
            });
            await _audit.LogAsync(ctx.ViewerUserId, "sensitive_field.read", "User", ctx.SubjectUserId, payload, ct: ct);
        }

        return allowed;
    }
}
