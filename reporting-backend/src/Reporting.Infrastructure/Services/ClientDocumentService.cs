using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Reporting.Application.Audit;
using Reporting.Application.Clients;
using Reporting.Application.Common;
using Reporting.Application.Documents;
using Reporting.Domain.Entities.Clients;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// خدمة مستندات العميل (CPW-R1B2). كلّ قرارات الأمن خادميّة:
/// النطاق (IClientProjectAccess)، وقائمة السماح والبصمة السحريّة (DocumentContentPolicy)،
/// وحدّ الحجم والحصّة (FileStorageOptions)، وحالة الفحص الصادقة (IDocumentScanner — C-01).
/// <para>
/// ثوابت: الحذف Tombstone فقط؛ <c>StorageKey</c> لا يخرج في أيّ DTO أو تدقيق؛
/// المستند خارج العميل أو خارج النطاق ⇒ 404 لا 403 (منع الاستكشاف).
/// </para>
/// </summary>
public class ClientDocumentService : IClientDocumentService
{
    private const string ResourceKind = "clients";

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClientProjectAccess _access;
    private readonly IAuditService _audit;
    private readonly IFileStorage _storage;
    private readonly IDocumentScanner _scanner;
    private readonly IDocumentAccessEvaluator _evaluator;
    private readonly FileStorageOptions _options;

    public ClientDocumentService(
        AppDbContext db,
        ICurrentUser currentUser,
        IClientProjectAccess access,
        IAuditService audit,
        IFileStorage storage,
        IDocumentScanner scanner,
        IDocumentAccessEvaluator evaluator,
        IOptions<FileStorageOptions> options)
    {
        _db = db;
        _currentUser = currentUser;
        _access = access;
        _audit = audit;
        _storage = storage;
        _scanner = scanner;
        _evaluator = evaluator;
        _options = options.Value;
    }

    // ===== قراءة =====

    public async Task<Result<IReadOnlyList<ClientDocumentDto>>> ListAsync(Guid clientId, ClientDocumentFilter filter, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null)
            return Result<IReadOnlyList<ClientDocumentDto>>.Failure("غير مصرّح.", "auth.unauthenticated");

        var guard = await AuthorizeReadAsync(clientId, ct);
        if (guard is not null) return Result<IReadOnlyList<ClientDocumentDto>>.Failure(guard.Value.message, guard.Value.code);

        // صلاحيّة العميل أوّلًا (أعلاه) ثمّ سياسة المستند — الفلترة داخل الاستعلام لا بعده.
        var context = await _evaluator.BuildContextAsync(clientId, ct);
        var canManage = await CanManageVisibilityAsync(clientId, ct);

        var q = _db.ClientDocuments.AsNoTracking()
            .Include(d => d.CurrentVersion)
            .Where(d => d.ClientId == clientId && !d.IsDeleted)
            .Where(_evaluator.VisibleFilter(context));

        // قائمتا الأدوار/المستخدمين لا تُحمَّلان إلّا لمن يملك صلاحيّة إدارة المستندات (منع تسرّب §12).
        if (canManage)
            q = q.Include(d => d.AllowedRoles).Include(d => d.AllowedUsers);

        if (!filter.IncludeArchived) q = q.Where(d => !d.IsArchived);
        if (!string.IsNullOrWhiteSpace(filter.CategoryCode)) q = q.Where(d => d.CategoryCode == filter.CategoryCode);
        if (!string.IsNullOrWhiteSpace(filter.ConfidentialityCode)) q = q.Where(d => d.ConfidentialityCode == filter.ConfidentialityCode);
        if (filter.LifecycleStatus is DocumentLifecycleStatus status) q = q.Where(d => d.LifecycleStatus == status);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            q = q.Where(d => EF.Functions.ILike(d.Title, $"%{term}%")
                          || (d.Tags != null && EF.Functions.ILike(d.Tags, $"%{term}%")));
        }

        var rows = await q.OrderByDescending(d => d.CreatedAtUtc).ToListAsync(ct);
        var names = await ResolveNamesAsync(rows.Select(r => r.UploadedByUserId), ct);
        return Result<IReadOnlyList<ClientDocumentDto>>.Success(rows.Select(r => Map(r, names, canManage)).ToList());
    }

    public async Task<Result<ClientDocumentDetailDto>> GetAsync(Guid clientId, Guid documentId, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null)
            return Result<ClientDocumentDetailDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var guard = await AuthorizeReadAsync(clientId, ct);
        if (guard is not null) return Result<ClientDocumentDetailDto>.Failure(guard.Value.message, guard.Value.code);

        var doc = await _db.ClientDocuments.AsNoTracking()
            .Include(d => d.CurrentVersion)
            .Include(d => d.AllowedRoles)
            .Include(d => d.AllowedUsers)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.ClientId == clientId && !d.IsDeleted, ct);
        if (doc is null) return Result<ClientDocumentDetailDto>.Failure("المستند غير موجود.", "client_document.not_found");

        // سياسة المستند بعد صلاحيّة العميل — المنع يعني «غير موجود» قبل تحميل أيّ نسخة أو بيانات وصفيّة.
        var context = await _evaluator.BuildContextAsync(clientId, ct);
        if (!_evaluator.Evaluate(doc, context).CanViewMetadata)
            return Result<ClientDocumentDetailDto>.Failure("المستند غير موجود.", "client_document.not_found");

        var canManage = await CanManageVisibilityAsync(clientId, ct);

        var versions = await _db.ClientDocumentVersions.AsNoTracking()
            .Where(v => v.ClientDocumentId == documentId)
            .OrderByDescending(v => v.VersionNo)
            .ToListAsync(ct);

        var ids = versions.Select(v => v.UploadedByUserId).Append(doc.UploadedByUserId);
        var names = await ResolveNamesAsync(ids, ct);

        return Result<ClientDocumentDetailDto>.Success(new ClientDocumentDetailDto(
            Map(doc, names, canManage),
            versions.Select(v => MapVersion(v, names)).ToList()));
    }

    public async Task<Result<ClientStorageUsageDto>> GetStorageUsageAsync(Guid clientId, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null)
            return Result<ClientStorageUsageDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var guard = await AuthorizeReadAsync(clientId, ct);
        if (guard is not null) return Result<ClientStorageUsageDto>.Failure(guard.Value.message, guard.Value.code);

        var used = await UsedBytesAsync(clientId, ct);
        var documentCount = await _db.ClientDocuments.CountAsync(d => d.ClientId == clientId && !d.IsDeleted, ct);
        var versionCount = await _db.ClientDocumentVersions
            .CountAsync(v => _db.ClientDocuments.Any(d => d.Id == v.ClientDocumentId && d.ClientId == clientId && !d.IsDeleted), ct);

        return Result<ClientStorageUsageDto>.Success(new ClientStorageUsageDto(
            clientId,
            used,
            _options.ResourceStorageQuotaBytes,
            Math.Max(0, _options.ResourceStorageQuotaBytes - used),
            documentCount,
            versionCount,
            _options.MaxUploadSizeBytes,
            DocumentContentPolicy.AllowedExtensions(_options),
            _scanner.EngineName,
            _scanner.IsConfigured));
    }

    // ===== كتابة =====

    public async Task<Result<ClientDocumentDetailDto>> CreateAsync(
        Guid clientId, CreateClientDocumentRequest request, DocumentUploadContent upload, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<ClientDocumentDetailDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var metadata = ValidateMetadata(request.Title, request.CategoryCode, request.ConfidentialityCode,
            request.Description, request.Tags, request.ChangeNote);
        if (metadata is not null) return Result<ClientDocumentDetailDto>.Failure(metadata.Value.message, metadata.Value.code);

        var auth = await AuthorizeWriteAsync(clientId, uid, ct);
        if (auth is not null) return Result<ClientDocumentDetailDto>.Failure(auth.Value.message, auth.Value.code);

        var document = new ClientDocument
        {
            ClientId = clientId,
            Title = request.Title.Trim(),
            Description = Trim(request.Description),
            CategoryCode = request.CategoryCode.Trim(),
            Tags = Trim(request.Tags),
            ConfidentialityCode = Trim(request.ConfidentialityCode),
            LifecycleStatus = DocumentLifecycleStatus.Current,
            UploadedByUserId = uid,
            VersionCount = 0
        };

        // السياسة الافتراضيّة للتصنيف تُطبَّق عند الإنشاء فقط، وتُتجاوَز باختيار صريح من المستخدم.
        var visibility = await ApplyVisibilityAsync(
            document,
            request.VisibilityType ?? DocumentCodeConstants.DefaultVisibilityFor(request.CategoryCode),
            request.AllowedRoles, request.AllowedUserIds, uid, ct);
        if (visibility is not null)
            return Result<ClientDocumentDetailDto>.Failure(visibility.Value.message, visibility.Value.code);

        var stored = await StoreVersionAsync(clientId, document, 1, upload, request.ChangeNote, uid, ct);
        if (!stored.Succeeded)
            return Result<ClientDocumentDetailDto>.Failure(stored.Error!, stored.ErrorCode);

        var version = stored.Value!;
        document.VersionCount = 1;

        _db.ClientDocuments.Add(document);
        _db.ClientDocumentVersions.Add(version);

        try
        {
            // المستند والنسخة يشيران لبعضهما (CurrentVersionId ↔ ClientDocumentId)؛
            // لذا يُكتب الصفّان أوّلًا ثمّ يُضبط المؤشّر في حفظ ثانٍ داخل نفس المعاملة.
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            await _db.SaveChangesAsync(ct);

            document.CurrentVersionId = version.Id;
            await _db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);
        }
        catch
        {
            // فشل الحفظ لا يترك ملفًّا يتيمًا على القرص.
            await _storage.DeleteAsync(version.StorageKey, CancellationToken.None);
            throw;
        }

        await _audit.LogAsync(uid, "client_document.created", nameof(ClientDocument), document.Id,
            AuditPayload(document, version), ct: ct);

        return await BuildWrittenDetailAsync(clientId, document.Id, ct);
    }

    public async Task<Result<ClientDocumentDetailDto>> AddVersionAsync(
        Guid clientId, Guid documentId, AddDocumentVersionRequest request, DocumentUploadContent upload, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<ClientDocumentDetailDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        if (ClientFieldGuards.AnyContainsSecret(request.ChangeNote))
            return Result<ClientDocumentDetailDto>.Failure("لا يجوز تخزين أسرار في ملاحظة التغيير.", "client_document.secret_forbidden");

        var auth = await AuthorizeWriteAsync(clientId, uid, ct);
        if (auth is not null) return Result<ClientDocumentDetailDto>.Failure(auth.Value.message, auth.Value.code);

        var document = await _db.ClientDocuments
            .Include(d => d.AllowedRoles)
            .Include(d => d.AllowedUsers)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.ClientId == clientId && !d.IsDeleted, ct);
        if (document is null) return Result<ClientDocumentDetailDto>.Failure("المستند غير موجود.", "client_document.not_found");

        // من لا يرى المستند لا يعدّله — نفس المقيّم، ونفس الردّ «غير موجود».
        var context = await _evaluator.BuildContextAsync(clientId, ct);
        if (!_evaluator.Evaluate(document, context).CanViewMetadata)
            return Result<ClientDocumentDetailDto>.Failure("المستند غير موجود.", "client_document.not_found");

        if (document.IsArchived)
            return Result<ClientDocumentDetailDto>.Failure("لا يمكن إضافة نسخة إلى مستند مؤرشف.", "client_document.archived.conflict");

        var currentVersions = await _db.ClientDocumentVersions
            .Where(v => v.ClientDocumentId == documentId)
            .ToListAsync(ct);

        var nextNo = currentVersions.Count == 0 ? 1 : currentVersions.Max(v => v.VersionNo) + 1;

        var stored = await StoreVersionAsync(clientId, document, nextNo, upload, request.ChangeNote, uid, ct);
        if (!stored.Succeeded)
            return Result<ClientDocumentDetailDto>.Failure(stored.Error!, stored.ErrorCode);

        var version = stored.Value!;

        // النسخة السابقة تبقى محفوظة وتصير Superseded — لا حذف إطلاقًا.
        foreach (var previous in currentVersions.Where(v => v.IsCurrent))
        {
            previous.IsCurrent = false;
            previous.UpdatedAtUtc = DateTime.UtcNow;
        }

        _db.ClientDocumentVersions.Add(version);
        document.CurrentVersionId = version.Id;
        document.VersionCount = nextNo;
        document.LifecycleStatus = DocumentLifecycleStatus.Current;
        document.UpdatedAtUtc = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            await _storage.DeleteAsync(version.StorageKey, CancellationToken.None);
            throw;
        }

        await _audit.LogAsync(uid, "client_document.version_added", nameof(ClientDocument), document.Id,
            AuditPayload(document, version), ct: ct);

        return await BuildWrittenDetailAsync(clientId, document.Id, ct);
    }

    public async Task<Result<ClientDocumentDto>> UpdateAsync(
        Guid clientId, Guid documentId, UpdateClientDocumentRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<ClientDocumentDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var metadata = ValidateMetadata(request.Title, request.CategoryCode, request.ConfidentialityCode,
            request.Description, request.Tags, request.ApprovalStatusCode);
        if (metadata is not null) return Result<ClientDocumentDto>.Failure(metadata.Value.message, metadata.Value.code);

        var auth = await AuthorizeWriteAsync(clientId, uid, ct);
        if (auth is not null) return Result<ClientDocumentDto>.Failure(auth.Value.message, auth.Value.code);

        var document = await _db.ClientDocuments
            .Include(d => d.CurrentVersion)
            .Include(d => d.AllowedRoles)
            .Include(d => d.AllowedUsers)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.ClientId == clientId && !d.IsDeleted, ct);
        if (document is null) return Result<ClientDocumentDto>.Failure("المستند غير موجود.", "client_document.not_found");

        // من لا يرى المستند لا يعدّله — نفس المقيّم، ونفس الردّ «غير موجود».
        var context = await _evaluator.BuildContextAsync(clientId, ct);
        if (!_evaluator.Evaluate(document, context).CanViewMetadata)
            return Result<ClientDocumentDto>.Failure("المستند غير موجود.", "client_document.not_found");

        document.Title = request.Title.Trim();
        document.Description = Trim(request.Description);
        document.CategoryCode = request.CategoryCode.Trim();
        document.Tags = Trim(request.Tags);
        document.ConfidentialityCode = Trim(request.ConfidentialityCode);
        if (request.LifecycleStatus is DocumentLifecycleStatus lifecycle) document.LifecycleStatus = lifecycle;
        if (!string.IsNullOrWhiteSpace(request.ApprovalStatusCode)) document.ApprovalStatusCode = request.ApprovalStatusCode.Trim();

        // غياب VisibilityType يُبقي السياسة الحاليّة كما هي — لا تُطبَّق سياسة التصنيف الافتراضيّة عند التعديل.
        if (request.VisibilityType is DocumentVisibilityType requested)
        {
            var visibility = await ApplyVisibilityAsync(
                document, requested, request.AllowedRoles, request.AllowedUserIds, uid, ct);
            if (visibility is not null)
                return Result<ClientDocumentDto>.Failure(visibility.Value.message, visibility.Value.code);
        }

        document.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "client_document.updated", nameof(ClientDocument), document.Id, ct: ct);

        var names = await ResolveNamesAsync(new[] { document.UploadedByUserId }, ct);
        // المُعدِّل اجتاز AuthorizeWriteAsync ⇒ يملك إدارة سياسة الرؤية بالتعريف.
        return Result<ClientDocumentDto>.Success(Map(document, names, true));
    }

    public async Task<Result<ClientDocumentDto>> SetArchivedAsync(
        Guid clientId, Guid documentId, bool isArchived, ArchiveClientDocumentRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<ClientDocumentDto>.Failure("غير مصرّح.", "auth.unauthenticated");

        var auth = await AuthorizeWriteAsync(clientId, uid, ct);
        if (auth is not null) return Result<ClientDocumentDto>.Failure(auth.Value.message, auth.Value.code);

        var document = await _db.ClientDocuments
            .Include(d => d.CurrentVersion)
            .Include(d => d.AllowedRoles)
            .Include(d => d.AllowedUsers)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.ClientId == clientId && !d.IsDeleted, ct);
        if (document is null) return Result<ClientDocumentDto>.Failure("المستند غير موجود.", "client_document.not_found");

        // من لا يرى المستند لا يؤرشفه — نفس المقيّم، ونفس الردّ «غير موجود».
        var context = await _evaluator.BuildContextAsync(clientId, ct);
        if (!_evaluator.Evaluate(document, context).CanViewMetadata)
            return Result<ClientDocumentDto>.Failure("المستند غير موجود.", "client_document.not_found");

        if (document.IsArchived == isArchived)
            return Result<ClientDocumentDto>.Failure(
                isArchived ? "المستند مؤرشف بالفعل." : "المستند غير مؤرشف.",
                "client_document.state_unchanged.conflict");

        document.IsArchived = isArchived;
        document.ArchivedAtUtc = isArchived ? DateTime.UtcNow : null;
        document.ArchivedByUserId = isArchived ? uid : null;
        document.ArchiveReason = isArchived ? Trim(request.Reason) : null;
        document.LifecycleStatus = isArchived ? DocumentLifecycleStatus.Archived : DocumentLifecycleStatus.Current;
        document.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, isArchived ? "client_document.archived" : "client_document.unarchived",
            nameof(ClientDocument), document.Id, ct: ct);

        var names = await ResolveNamesAsync(new[] { document.UploadedByUserId }, ct);
        return Result<ClientDocumentDto>.Success(Map(document, names, true));
    }

    public async Task<Result<bool>> DeleteAsync(
        Guid clientId, Guid documentId, DeleteClientDocumentRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<bool>.Failure("غير مصرّح.", "auth.unauthenticated");
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result<bool>.Failure("سبب الحذف مطلوب.", "client_document.delete_reason_required");

        var auth = await AuthorizeWriteAsync(clientId, uid, ct);
        if (auth is not null) return Result<bool>.Failure(auth.Value.message, auth.Value.code);
        if (!_currentUser.IsInAnyRole(Roles.TeamManagement))
            return Result<bool>.Failure("حذف المستندات مقصور على الإدارة العليا.", "auth.forbidden");

        var document = await _db.ClientDocuments
            .Include(d => d.AllowedRoles)
            .Include(d => d.AllowedUsers)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.ClientId == clientId && !d.IsDeleted, ct);
        if (document is null) return Result<bool>.Failure("المستند غير موجود.", "client_document.not_found");

        // من لا يرى المستند لا يحذفه — نفس المقيّم، ونفس الردّ «غير موجود».
        var context = await _evaluator.BuildContextAsync(clientId, ct);
        if (!_evaluator.Evaluate(document, context).CanViewMetadata)
            return Result<bool>.Failure("المستند غير موجود.", "client_document.not_found");

        // Tombstone: الصفّ يبقى وكذلك كلّ النسخ والملفّات على القرص.
        document.IsDeleted = true;
        document.DeletedAtUtc = DateTime.UtcNow;
        document.DeletedByUserId = uid;
        document.DeleteReason = request.Reason.Trim();
        document.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(uid, "client_document.deleted", nameof(ClientDocument), document.Id, ct: ct);

        return Result<bool>.Success(true);
    }

    // ===== تنزيل =====

    public async Task<Result<DocumentDownload>> DownloadAsync(
        Guid clientId, Guid documentId, Guid? versionId, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not Guid uid)
            return Result<DocumentDownload>.Failure("غير مصرّح.", "auth.unauthenticated");

        var guard = await AuthorizeReadAsync(clientId, ct);
        if (guard is not null) return Result<DocumentDownload>.Failure(guard.Value.message, guard.Value.code);

        var document = await _db.ClientDocuments.AsNoTracking()
            .Include(d => d.AllowedRoles)
            .Include(d => d.AllowedUsers)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.ClientId == clientId && !d.IsDeleted, ct);
        if (document is null) return Result<DocumentDownload>.Failure("المستند غير موجود.", "client_document.not_found");

        // العرض == التنزيل في v1؛ المنع يعني «غير موجود» قبل الوصول إلى أيّ نسخة.
        var context = await _evaluator.BuildContextAsync(clientId, ct);
        if (!_evaluator.Evaluate(document, context).CanDownload)
            return Result<DocumentDownload>.Failure("المستند غير موجود.", "client_document.not_found");

        var version = versionId is Guid vid
            ? await _db.ClientDocumentVersions.AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == vid && v.ClientDocumentId == documentId, ct)
            : await _db.ClientDocumentVersions.AsNoTracking()
                .FirstOrDefaultAsync(v => v.ClientDocumentId == documentId && v.IsCurrent, ct);
        if (version is null) return Result<DocumentDownload>.Failure("نسخة المستند غير موجودة.", "client_document_version.not_found");

        // C-01: لا يُسمح بالتنزيل قبل فحص نظيف إلّا إن كان الاشتراط معطّلًا (لا محرّك فعليّ).
        if (_options.RequireCleanScanBeforeDownload && version.ScanStatus != DocumentScanStatus.Clean)
            return Result<DocumentDownload>.Failure("لم يجتز الملفّ فحص الأمان بعد.", "client_document.scan_not_clean.conflict");

        if (version.ScanStatus == DocumentScanStatus.Rejected)
            return Result<DocumentDownload>.Failure("الملفّ مرفوض من محرّك الفحص.", "client_document.scan_rejected.conflict");

        Stream content;
        try
        {
            content = await _storage.OpenReadAsync(version.StorageKey, ct);
        }
        catch (FileNotFoundException)
        {
            return Result<DocumentDownload>.Failure("الملفّ المخزَّن غير متاح.", "client_document.file_missing");
        }

        await _audit.LogAsync(uid, "client_document.downloaded", nameof(ClientDocumentVersion), version.Id, ct: ct);

        return Result<DocumentDownload>.Success(new DocumentDownload(
            content,
            version.ContentType,
            DocumentContentPolicy.SanitizeFileName(version.OriginalFileName),
            version.SizeBytes,
            // الامتداد المجهول أو القابل للتنفيذ في المتصفّح يُقدَّم كمرفق دائمًا.
            DocumentContentPolicy.ShouldForceAttachment(version.OriginalFileName)));
    }

    // ===== المساعدات =====

    /// <summary>
    /// يبني تفاصيل مستند كُتِب للتوّ من قِبل صاحب صلاحيّة الكتابة (إنشاء/إضافة نسخة).
    /// <para>
    /// لا يُعيد تطبيق سياسة الرؤية لأنّ الكاتب قد يختار سياسة تستثنيه هو نفسه
    /// (مثل <c>FinanceOnly</c> يرفعها مدير)، فإعادة التقييم كانت ستُرجِع 404 على عمليّة ناجحة.
    /// صلاحيّة الكتابة تحقّقت قبل الاستدعاء، والاستدعاء مقصور على المستند المكتوب في نفس الطلب.
    /// مسارات القراءة (<see cref="GetAsync"/>/<see cref="ListAsync"/>/التنزيل) تبقى محكومة بالسياسة كاملة.
    /// </para>
    /// </summary>
    private async Task<Result<ClientDocumentDetailDto>> BuildWrittenDetailAsync(
        Guid clientId, Guid documentId, CancellationToken ct)
    {
        var doc = await _db.ClientDocuments.AsNoTracking()
            .Include(d => d.CurrentVersion)
            .Include(d => d.AllowedRoles)
            .Include(d => d.AllowedUsers)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.ClientId == clientId && !d.IsDeleted, ct);
        if (doc is null) return Result<ClientDocumentDetailDto>.Failure("المستند غير موجود.", "client_document.not_found");

        var versions = await _db.ClientDocumentVersions.AsNoTracking()
            .Where(v => v.ClientDocumentId == documentId)
            .OrderByDescending(v => v.VersionNo)
            .ToListAsync(ct);

        var names = await ResolveNamesAsync(versions.Select(v => v.UploadedByUserId).Append(doc.UploadedByUserId), ct);

        return Result<ClientDocumentDetailDto>.Success(new ClientDocumentDetailDto(
            Map(doc, names, true),
            versions.Select(v => MapVersion(v, names)).ToList()));
    }

    /// <summary>
    /// يتحقّق من الملفّ ويخزّنه ويبني كيان النسخة (دون إضافته للسياق).
    /// عند أيّ رفض لا يُكتب شيء على القرص.
    /// </summary>
    private async Task<Result<ClientDocumentVersion>> StoreVersionAsync(
        Guid clientId, ClientDocument document, int versionNo,
        DocumentUploadContent upload, string? changeNote, Guid uid, CancellationToken ct)
    {
        if (upload.Length <= 0)
            return Result<ClientDocumentVersion>.Failure("الملفّ فارغ.", "document.file_required");
        if (upload.Length > _options.MaxUploadSizeBytes)
            return Result<ClientDocumentVersion>.Failure(
                $"حجم الملفّ يتجاوز الحدّ المسموح ({_options.MaxUploadSizeBytes / (1024 * 1024)} ميغابايت).",
                "document.file_too_large");

        var used = await UsedBytesAsync(clientId, ct);
        if (used + upload.Length > _options.ResourceStorageQuotaBytes)
            return Result<ClientDocumentVersion>.Failure("تجاوزت حصّة التخزين المخصّصة لهذا العميل.", "document.quota_exceeded");

        // قراءة الترويسة للتحقّق من البصمة السحريّة ثمّ إعادة التدفّق إلى بدايته.
        var header = new byte[512];
        var headerLength = await ReadHeaderAsync(upload.Content, header, ct);
        var validation = DocumentContentPolicy.Validate(
            upload.FileName, upload.DeclaredContentType, header.AsSpan(0, headerLength), _options);
        if (!validation.IsValid)
            return Result<ClientDocumentVersion>.Failure(validation.Error!, validation.ErrorCode);

        var scan = await _scanner.ScanAsync(upload.Content, upload.FileName, ct);
        if (scan.Status == DocumentScanStatus.Rejected)
            return Result<ClientDocumentVersion>.Failure("رفض محرّك الفحص هذا الملفّ.", "document.scan_rejected");
        if (upload.Content.CanSeek) upload.Content.Position = 0;

        var versionId = Guid.NewGuid();
        var storageKey = _storage.BuildStorageKey(ResourceKind, clientId, document.Id, versionId, validation.ResolvedExtension);
        var stored = await _storage.SaveAsync(storageKey, upload.Content, ct);

        return Result<ClientDocumentVersion>.Success(new ClientDocumentVersion
        {
            Id = versionId,
            ClientDocumentId = document.Id,
            VersionNo = versionNo,
            OriginalFileName = DocumentContentPolicy.SanitizeFileName(upload.FileName),
            StorageKey = stored.StorageKey,
            ContentType = validation.ResolvedContentType,
            SizeBytes = stored.SizeBytes,
            Sha256 = stored.Sha256,
            ScanStatus = scan.Status,
            ScanEngine = scan.Engine,
            ScannedAtUtc = scan.ScannedAtUtc,
            ScanDetail = scan.Detail,
            UploadedByUserId = uid,
            ChangeNote = Trim(changeNote),
            IsCurrent = true
        });
    }

    private static async Task<int> ReadHeaderAsync(Stream content, byte[] buffer, CancellationToken ct)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await content.ReadAsync(buffer.AsMemory(total, buffer.Length - total), ct);
            if (read == 0) break;
            total += read;
        }
        if (content.CanSeek) content.Position = 0;
        return total;
    }

    private Task<long> UsedBytesAsync(Guid clientId, CancellationToken ct)
        => _db.ClientDocumentVersions
            .Where(v => _db.ClientDocuments.Any(d => d.Id == v.ClientDocumentId && d.ClientId == clientId && !d.IsDeleted))
            .SumAsync(v => (long?)v.SizeBytes, ct)
            .ContinueWith(t => t.Result ?? 0L, ct, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);

    /// <summary>قراءة: العميل خارج النطاق يُعامَل كغير موجود لمنع الاستكشاف.</summary>
    private async Task<(string message, string code)?> AuthorizeReadAsync(Guid clientId, CancellationToken ct)
    {
        if (!await _db.Clients.AnyAsync(c => c.Id == clientId, ct))
            return ("العميل غير موجود.", "client.not_found");

        var client = await _db.Clients.AsNoTracking()
            .Select(c => new { c.Id, c.AccountManagerId })
            .FirstAsync(c => c.Id == clientId, ct);
        if (client.AccountManagerId == _currentUser.UserId) return null;

        // المالية والموارد البشريّة وظيفتان على مستوى الشركة: تعبران بوّابة العميل في مسار المستندات
        // حصرًا (CPW-R2). لا تكتسبان «صلاحيّة عميل» ⇒ سياسة المستند وحدها تحكم ما يظهر لهما،
        // فلا تريان ClientScoped، ولا يمنحهما ذلك أيّ وصول إلى بيانات العميل الأساسيّة.
        if (_currentUser.IsInAnyRole(DocumentVisibilityPolicy.Finance)
            || _currentUser.IsInAnyRole(DocumentVisibilityPolicy.HrManagement)) return null;

        var vis = await _access.ResolveAsync(ct);
        if (!vis.CanViewClient(clientId)) return ("العميل غير موجود.", "client.not_found");
        return null;
    }

    /// <summary>كتابة: مدير الحساب للعميل، أو دور إداريّ أساسيّ يرى العميل ضمن نطاقه.</summary>
    private async Task<(string message, string code)?> AuthorizeWriteAsync(Guid clientId, Guid uid, CancellationToken ct)
    {
        var client = await _db.Clients.AsNoTracking()
            .Select(c => new { c.Id, c.AccountManagerId })
            .FirstOrDefaultAsync(c => c.Id == clientId, ct);
        if (client is null) return ("العميل غير موجود.", "client.not_found");

        if (client.AccountManagerId == uid) return null;

        if (_currentUser.IsInAnyRole(Roles.ClientCoreManagers))
        {
            var vis = await _access.ResolveAsync(ct);
            if (vis.CanViewClient(clientId)) return null;
            // العميل خارج النطاق ⇒ لا يُكشَف وجوده.
            return ("العميل غير موجود.", "client.not_found");
        }

        var readable = await _access.ResolveAsync(ct);
        if (!readable.CanViewClient(clientId)) return ("العميل غير موجود.", "client.not_found");
        return ("لا تملك صلاحية إدارة مستندات هذا العميل.", "auth.forbidden");
    }

    /// <summary>
    /// صلاحيّة إدارة سياسة الرؤية = صلاحيّة كتابة مستندات العميل نفسها (لا سياسة منفصلة).
    /// تُستعمَل لكشف قائمتَي الأدوار/المستخدمين المصرّح لهم في القراءة (§12).
    /// </summary>
    private async Task<bool> CanManageVisibilityAsync(Guid clientId, CancellationToken ct)
        => _currentUser.UserId is Guid uid && await AuthorizeWriteAsync(clientId, uid, ct) is null;

    /// <summary>
    /// تحقّق سياسة الرؤية (§7): «أدوار محدّدة» تتطلّب دورًا معروفًا واحدًا على الأقلّ،
    /// و«مستخدمون محدّدون» تتطلّب مستخدمًا واحدًا على الأقلّ. غير المخصّصة تتجاهل القائمتين صراحةً.
    /// </summary>
    private static (string message, string code)? ValidateVisibility(
        DocumentVisibilityType type,
        IReadOnlyList<string>? roles,
        IReadOnlyList<Guid>? users,
        out List<string> canonicalRoles,
        out List<Guid> allowedUserIds)
    {
        canonicalRoles = new List<string>();
        allowedUserIds = new List<Guid>();

        if (!Enum.IsDefined(typeof(DocumentVisibilityType), type))
            return ("سياسة الرؤية غير معتمَدة.", "client_document.visibility_invalid");

        if (DocumentVisibilityPolicy.RequiresRoles(type))
        {
            foreach (var raw in roles ?? Array.Empty<string>())
            {
                var canonical = DocumentVisibilityPolicy.CanonicalRole(raw);
                if (canonical is null)
                    return ("اسم دور غير معروف في سياسة الرؤية.", "client_document.visibility_role_invalid");
                if (!canonicalRoles.Contains(canonical)) canonicalRoles.Add(canonical);
            }
            if (canonicalRoles.Count == 0)
                return ("سياسة «أدوار محدّدة» تتطلّب اختيار دور واحد على الأقلّ.", "client_document.visibility_roles_required");
        }
        else if (DocumentVisibilityPolicy.RequiresUsers(type))
        {
            foreach (var id in users ?? Array.Empty<Guid>())
                if (id != Guid.Empty && !allowedUserIds.Contains(id)) allowedUserIds.Add(id);
            if (allowedUserIds.Count == 0)
                return ("سياسة «مستخدمون محدّدون» تتطلّب اختيار مستخدم واحد على الأقلّ.", "client_document.visibility_users_required");
        }

        return null;
    }

    /// <summary>
    /// يطبّق سياسة الرؤية على الكيان: يتحقّق، ثمّ يستبدل قائمتَي الأدوار/المستخدمين استبدالًا كاملًا،
    /// ويختم بأثر التعديل (من ومتى). القوائم تُفرَّغ حتمًا في السياسات غير المخصّصة.
    /// </summary>
    private async Task<(string message, string code)?> ApplyVisibilityAsync(
        ClientDocument document,
        DocumentVisibilityType type,
        IReadOnlyList<string>? roles,
        IReadOnlyList<Guid>? users,
        Guid uid,
        CancellationToken ct)
    {
        var invalid = ValidateVisibility(type, roles, users, out var canonicalRoles, out var allowedUserIds);
        if (invalid is not null) return invalid;

        if (allowedUserIds.Count > 0)
        {
            var existing = await _db.Users
                .Where(u => allowedUserIds.Contains(u.Id) && u.IsActive)
                .Select(u => u.Id)
                .ToListAsync(ct);
            if (existing.Count != allowedUserIds.Count)
                return ("أحد المستخدمين المختارين غير موجود أو غير نشط.", "client_document.visibility_user_invalid");
        }

        if (document.AllowedRoles.Count > 0) _db.ClientDocumentAllowedRoles.RemoveRange(document.AllowedRoles);
        if (document.AllowedUsers.Count > 0) _db.ClientDocumentAllowedUsers.RemoveRange(document.AllowedUsers);
        document.AllowedRoles.Clear();
        document.AllowedUsers.Clear();

        foreach (var role in canonicalRoles)
            document.AllowedRoles.Add(new ClientDocumentAllowedRole { ClientDocumentId = document.Id, RoleName = role });
        foreach (var userId in allowedUserIds)
            document.AllowedUsers.Add(new ClientDocumentAllowedUser { ClientDocumentId = document.Id, UserId = userId });

        document.VisibilityType = type;
        document.VisibilityUpdatedAtUtc = DateTime.UtcNow;
        document.VisibilityUpdatedByUserId = uid;
        return null;
    }

    private static (string message, string code)? ValidateMetadata(
        string? title, string? categoryCode, string? confidentialityCode, params string?[] freeText)
    {
        if (string.IsNullOrWhiteSpace(title))
            return ("عنوان المستند مطلوب.", "client_document.title_required");
        if (!DocumentCodeConstants.IsValidDocumentCategory(categoryCode))
            return ("تصنيف المستند غير معتمَد.", "client_document.category_invalid");
        if (!DocumentCodeConstants.IsValidConfidentiality(confidentialityCode))
            return ("درجة السرّيّة غير معتمَدة.", "client_document.confidentiality_invalid");
        if (ClientFieldGuards.AnyContainsSecret(freeText.Append(title).ToArray()))
            return ("لا يجوز تخزين كلمات مرور أو رموز وصول في بيانات المستند.", "client_document.secret_forbidden");
        return null;
    }

    private async Task<Dictionary<Guid, string>> ResolveNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();
        return await _db.Users.Where(u => distinct.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    /// <summary>حمولة تدقيق بلا أيّ مسار تخزين — البصمة والحجم فقط.</summary>
    private static string AuditPayload(ClientDocument document, ClientDocumentVersion version)
        => JsonSerializer.Serialize(new
        {
            document.ClientId,
            document.CategoryCode,
            version.VersionNo,
            version.SizeBytes,
            version.Sha256,
            ScanStatus = version.ScanStatus.ToString()
        });

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// <paramref name="canManage"/> يتحكّم بكشف قائمتَي الأدوار/المستخدمين المصرّح لهم (§12):
    /// من لا يملك صلاحيّة إدارة مستندات العميل يحصل على <c>null</c> فيهما لا على قائمة فارغة.
    /// </summary>
    private static ClientDocumentDto Map(ClientDocument d, IReadOnlyDictionary<Guid, string> names, bool canManage)
    {
        var current = d.CurrentVersion;
        var forceAttachment = current is not null
            && DocumentContentPolicy.ShouldForceAttachment(current.OriginalFileName);

        return new ClientDocumentDto(
            d.Id, d.ClientId, d.Title, d.Description, d.CategoryCode, d.Tags, d.ConfidentialityCode,
            d.LifecycleStatus, d.ApprovalStatusCode, d.VersionCount, d.UploadedByUserId,
            names.TryGetValue(d.UploadedByUserId, out var name) ? name : null,
            d.IsArchived, d.ArchivedAtUtc, d.ArchiveReason,
            d.VisibilityType,
            canManage ? d.AllowedRoles.Select(r => r.RoleName).OrderBy(r => r).ToList() : null,
            canManage ? d.AllowedUsers.Select(a => a.UserId).ToList() : null,
            canManage,
            d.CurrentVersionId, current?.VersionNo, current?.OriginalFileName, current?.ContentType,
            current?.SizeBytes, current?.ScanStatus, forceAttachment,
            d.CreatedAtUtc, d.UpdatedAtUtc);
    }

    private static ClientDocumentVersionDto MapVersion(ClientDocumentVersion v, IReadOnlyDictionary<Guid, string> names)
        => new(
            v.Id, v.ClientDocumentId, v.VersionNo, v.OriginalFileName, v.ContentType, v.SizeBytes, v.Sha256,
            v.ScanStatus, v.ScanEngine, v.ScannedAtUtc, v.ScanDetail, v.UploadedByUserId,
            names.TryGetValue(v.UploadedByUserId, out var name) ? name : null,
            v.ChangeNote, v.IsCurrent, v.CreatedAtUtc);
}
