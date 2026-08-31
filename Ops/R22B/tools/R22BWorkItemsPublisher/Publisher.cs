using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Reporting.Domain.Entities.Submissions;
using Reporting.Domain.Entities.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace R22BWorkItemsPublisher;

public enum Outcome
{
    Planned,
    Applied,
    AlreadyCompliant,
    TemplateNotFound,
    NoPublishedVersion,
    NoProjectSection,
    ContractMismatch,
    Blocked,
}

/// <summary>قرار معالجة مسودّة واحدة.</summary>
public sealed record DraftDecision(
    Guid SubmissionId,
    string PeriodKey,
    Guid SubmitterId,
    int FromVersionNumber,
    string Status,
    int ValueCount,
    int NonEmptyValueCount,
    int AttachmentCount,
    int ApprovalStepCount,
    bool UniqueConflict,
    bool IsEmpty,
    int ProjectEntryCount,
    int WorkItemCountAfter,
    string PayloadMd5Before,
    string PayloadMd5After,
    bool Eligible,
    string Reason,
    bool Processed);

public sealed class Report
{
    public string TemplateTitle { get; set; } = "";
    public Guid TemplateId { get; set; }
    public Outcome Outcome { get; set; }
    public bool Applied { get; set; }
    public int? SourceVersionNumber { get; set; }
    public Guid? SourceVersionId { get; set; }
    public int SourceFieldCount { get; set; }
    public string SourceConfigMd5 { get; set; } = "";
    public int? TargetVersionNumber { get; set; }
    public Guid? TargetVersionId { get; set; }
    public int TargetFieldCount { get; set; }
    public string TargetConfigMd5 { get; set; } = "";
    public List<string> MovedKeys { get; } = new();
    public List<string> KeptProjectKeys { get; } = new();
    public List<DraftDecision> Drafts { get; } = new();
    public int MigrationCountBefore { get; set; }
    public int MigrationCountAfter { get; set; }
    public int HistoricalSubmissionCount { get; set; }
    public string HistoricalFingerprint { get; set; } = "";
    public List<string> Notes { get; } = new();
    public string? BlockReason { get; set; }
}

/// <summary>حقل بند عمل مشتقّ من عمود شبكة: المفتاح تقنيّ، والتسمية والترتيب محفوظان حرفيًّا.</summary>
public sealed record ItemFieldSpec(string Key, string Type, string Label);

/// <summary>
/// تحويل شبكة (Grid) داخل بطاقة المشروع إلى مجموعة بنود عمل:
/// أعمدة الشبكة تصير حقول البند، وكلّ صفّ في الشبكة يصير بندًا مستقلًّا بلا فقد خليّة.
/// </summary>
public sealed record GridConversion(string GridKey, IReadOnlyList<ItemFieldSpec> ItemFields);

public static class Publisher
{
    // قفل advisory مستقلّ عن أدوات الصيانة الأخرى.
    private const long AdvisoryLockKey = 0x5232_3242_0001;

    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = false };

    /// <summary>
    /// ينشئ إصدارًا خليفة يحوّل الحقول المذكورة من مستوى المشروع إلى مستوى بند العمل، ثم ينشره،
    /// ثمّ يعالج المسودّات المفتوحة (فارغة: إعادة ربط · مأهولة: ترحيل answers ⇒ workItems[0]).
    /// Dry-Run افتراضيّ: يُرجِع الخطّة كاملة ثمّ يتراجع.
    /// </summary>
    public static async Task<Report> RunAsync(
        AppDbContext db,
        string templateTitle,
        IReadOnlyList<string> keysToMove,
        bool apply,
        GridConversion? grid = null,
        CancellationToken ct = default)
    {
        var report = new Report { TemplateTitle = templateTitle };
        report.MigrationCountBefore = await CountMigrationsAsync(db, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", new object[] { AdvisoryLockKey }, ct);

        // الاكتشاف بالاسم داخل هذه البيئة — لا نسخ معرّفات بين البيئات.
        var matches = await db.ReportTemplates
            .Include(t => t.Versions).ThenInclude(v => v.Fields)
            .Where(t => t.Title == templateTitle)
            .ToListAsync(ct);

        if (matches.Count != 1)
        {
            report.Outcome = Outcome.TemplateNotFound;
            report.BlockReason = matches.Count == 0
                ? $"لا قالب بالاسم «{templateTitle}» في هذه القاعدة."
                : $"أكثر من قالب ({matches.Count}) بالاسم «{templateTitle}» — التباس يمنع التنفيذ.";
            await tx.RollbackAsync(ct);
            report.MigrationCountAfter = report.MigrationCountBefore;
            return report;
        }

        var template = matches[0];
        report.TemplateId = template.Id;

        var source = template.Versions.Where(v => v.IsPublished)
            .OrderByDescending(v => v.VersionNumber).FirstOrDefault();
        if (source is null)
        {
            report.Outcome = Outcome.NoPublishedVersion;
            report.BlockReason = "لا إصدار منشور.";
            await tx.RollbackAsync(ct);
            report.MigrationCountAfter = report.MigrationCountBefore;
            return report;
        }

        var srcPrs = source.Fields.FirstOrDefault(f => f.FieldType == FieldType.ProjectRepeatableSection);
        if (srcPrs is null)
        {
            report.Outcome = Outcome.NoProjectSection;
            report.BlockReason = $"الإصدار المنشور v{source.VersionNumber} بلا ProjectRepeatableSection.";
            await tx.RollbackAsync(ct);
            report.MigrationCountAfter = report.MigrationCountBefore;
            return report;
        }

        report.SourceVersionId = source.Id;
        report.SourceVersionNumber = source.VersionNumber;
        report.SourceFieldCount = source.Fields.Count;
        report.SourceConfigMd5 = Md5(srcPrs.ConfigJson ?? "");

        // خطّ أساس التقارير التاريخيّة — يجب ألّا يتغيّر إطلاقًا.
        var (histCount, histFp) = await HistoricalFingerprintAsync(db, template.Id, ct);
        report.HistoricalSubmissionCount = histCount;
        report.HistoricalFingerprint = histFp;

        var srcCfg = JsonNode.Parse(srcPrs.ConfigJson ?? "{}")!.AsObject();
        var srcSchema = srcCfg["schemaVersion"]?.GetValue<int>() ?? 1;

        // idempotent: مطبَّق سلفًا.
        if (srcSchema >= 2 && srcCfg["workItems"] is JsonObject already)
        {
            report.TargetVersionId = source.Id;
            report.TargetVersionNumber = source.VersionNumber;
            report.TargetFieldCount = source.Fields.Count;
            report.TargetConfigMd5 = report.SourceConfigMd5;
            foreach (var f in already["fields"]?.AsArray() ?? new JsonArray())
                report.MovedKeys.Add(f?["key"]?.GetValue<string>() ?? "?");
            report.Outcome = Outcome.AlreadyCompliant;
            report.Notes.Add($"الإصدار الفعّال v{source.VersionNumber} يحمل schemaVersion=2 وworkItems سلفًا — لا إصدار جديد.");
            // ما زلنا نقيّم المسودّات (قد تكون عالقة على إصدار أقدم).
            // مفاتيح النقل تُشتقّ من عقد الإصدار الفعّال نفسه لا من الطلب — أدقّ وأأمن.
            await EvaluateDraftsAsync(db, template.Id, source.Id, source, srcCfg, report.MovedKeys.ToList(), report, ct);
            if (apply && report.Drafts.Any(d => d.Eligible))
            {
                await ApplyDraftsAsync(db, source, report, ct);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                report.Applied = true;
            }
            else
            {
                await tx.RollbackAsync(ct);
            }
            report.MigrationCountAfter = await CountMigrationsAsync(db, ct);
            return report;
        }

        // التحقّق من العقد: كلّ مفتاح مطلوب نقله موجود فعلًا في حقول المشروع.
        var srcFields = srcCfg["fields"]?.AsArray() ?? new JsonArray();
        var srcKeys = srcFields.Select(f => f?["key"]?.GetValue<string>() ?? "").ToList();

        // وضع «الكلّ»: قائمة مفاتيح فارغة تعني نقل كلّ حقول المشروع بحرفيّتها إلى بند العمل.
        var moveKeys = (grid is null && keysToMove.Count == 0) ? srcKeys : keysToMove;

        var moved = new JsonArray();
        var kept = new JsonArray();

        if (grid is null)
        {
            var missing = moveKeys.Where(k => !srcKeys.Contains(k)).ToList();
            if (missing.Count > 0)
            {
                report.Outcome = Outcome.ContractMismatch;
                report.BlockReason = $"مفاتيح غير موجودة في v{source.VersionNumber}: {string.Join(", ", missing)}. الموجود: {string.Join(", ", srcKeys)}";
                await tx.RollbackAsync(ct);
                report.MigrationCountAfter = report.MigrationCountBefore;
                return report;
            }

            // بناء ConfigJson الخليفة: نقل الحقول المطلوبة **بحرفيّتها** إلى workItems.fields، والباقي يبقى في fields.
            foreach (var f in srcFields)
            {
                var clone = JsonNode.Parse(f!.ToJsonString())!;
                var key = f["key"]?.GetValue<string>() ?? "";
                if (moveKeys.Contains(key)) { moved.Add(clone); report.MovedKeys.Add(key); }
                else { kept.Add(clone); report.KeptProjectKeys.Add(key); }
            }
        }
        else
        {
            // وضع الشبكة: الشبكة تُستبدَل ببنود عمل، وباقي حقول المشروع تبقى حرفيًّا.
            var gridNode = srcFields.FirstOrDefault(f => f?["key"]?.GetValue<string>() == grid.GridKey);
            if (gridNode is null)
            {
                report.Outcome = Outcome.ContractMismatch;
                report.BlockReason = $"الشبكة «{grid.GridKey}» غير موجودة في v{source.VersionNumber}. الموجود: {string.Join(", ", srcKeys)}";
                await tx.RollbackAsync(ct);
                report.MigrationCountAfter = report.MigrationCountBefore;
                return report;
            }

            var cols = (gridNode["columns"] as JsonArray)?.Select(c => c?.GetValue<string>() ?? "").ToList() ?? new List<string>();
            var expected = grid.ItemFields.Select(x => x.Label).ToList();
            if (!cols.SequenceEqual(expected))
            {
                report.Outcome = Outcome.ContractMismatch;
                report.BlockReason = $"أعمدة «{grid.GridKey}» تغيّرت عن المقيس. الفعليّ: [{string.Join(" | ", cols)}] — المتوقَّع: [{string.Join(" | ", expected)}]";
                await tx.RollbackAsync(ct);
                report.MigrationCountAfter = report.MigrationCountBefore;
                return report;
            }

            foreach (var f in srcFields)
            {
                var key = f?["key"]?.GetValue<string>() ?? "";
                if (key == grid.GridKey) continue;
                kept.Add(JsonNode.Parse(f!.ToJsonString())!);
                report.KeptProjectKeys.Add(key);
            }
            foreach (var spec in grid.ItemFields)
            {
                moved.Add(new JsonObject
                {
                    ["key"] = spec.Key,
                    ["type"] = spec.Type,
                    ["label"] = spec.Label,
                    ["columns"] = null,
                    ["options"] = null,
                    ["required"] = false,
                });
                report.MovedKeys.Add(spec.Key);
            }
            report.Notes.Add($"الشبكة «{grid.GridKey}» ({cols.Count} أعمدة) استُبدلت بـ{grid.ItemFields.Count} حقل بند عمل؛ التسميات والترتيب محفوظة حرفيًّا.");
        }

        var targetCfg = new JsonObject();
        foreach (var kv in srcCfg)
        {
            if (kv.Key is "fields" or "schemaVersion" or "workItems") continue;
            targetCfg[kv.Key] = kv.Value is null ? null : JsonNode.Parse(kv.Value.ToJsonString());
        }
        targetCfg["schemaVersion"] = 2;
        targetCfg["fields"] = kept;
        targetCfg["workItems"] = new JsonObject
        {
            ["key"] = "workItems",
            ["label"] = "بنود العمل",
            ["itemLabel"] = "بند عمل",
            ["addLabel"] = "+ إضافة بند عمل",
            ["minItems"] = 1,
            ["maxItems"] = 0,
            ["uniqueBy"] = new JsonArray(),
            ["fields"] = moved,
        };
        var targetConfigJson = targetCfg.ToJsonString(Pretty);

        var targetVersionNumber = template.Versions.Max(v => v.VersionNumber) + 1;
        var target = new ReportTemplateVersion
        {
            ReportTemplateId = template.Id,
            VersionNumber = targetVersionNumber,
            IsPublished = false,
        };
        foreach (var f in source.Fields.OrderBy(f => f.Order))
        {
            target.Fields.Add(new TemplateField
            {
                Label = f.Label,
                Key = f.Key,
                FieldType = f.FieldType,
                IsRequired = f.IsRequired,
                HelpText = f.HelpText,
                Order = f.Order,
                ConfigJson = f.FieldType == FieldType.ProjectRepeatableSection ? targetConfigJson : f.ConfigJson,
            });
        }
        db.ReportTemplateVersions.Add(target);

        report.TargetVersionId = target.Id;
        report.TargetVersionNumber = targetVersionNumber;
        report.TargetFieldCount = target.Fields.Count;
        report.TargetConfigMd5 = Md5(targetConfigJson);

        await EvaluateDraftsAsync(db, template.Id, target.Id, target, srcCfg, moveKeys, report, ct);

        // وضع الشبكة: تحويل خلايا الشبكة إلى بنود عمل قرار دلاليّ مستقلّ ⟹ لا مسودّة تُرحَّل آليًّا.
        if (grid is not null)
        {
            for (var i = 0; i < report.Drafts.Count; i++)
                if (report.Drafts[i].Eligible)
                    report.Drafts[i] = report.Drafts[i] with
                    {
                        Eligible = false,
                        Reason = "محظورة — لا تُمسّ: وضع الشبكة لا يدعم ترحيل المسودّات آليًّا (تفكيك صفوف الشبكة إلى بنود عمل يحتاج قرارًا دلاليًّا مستقلًّا).",
                    };
        }

        if (apply)
        {
            target.IsPublished = true;
            target.PublishedAtUtc = DateTime.UtcNow;
            await ApplyDraftsAsync(db, target, report, ct);
            await db.SaveChangesAsync(ct);

            // تحقّق ما بعد الكتابة داخل نفس المعاملة: التاريخيّ لم يتغيّر.
            var (afterCount, afterFp) = await HistoricalFingerprintAsync(db, template.Id, ct);
            if (afterCount != histCount || afterFp != histFp)
            {
                await tx.RollbackAsync(ct);
                report.Outcome = Outcome.Blocked;
                report.BlockReason = "تغيّرت بصمة التقارير التاريخيّة — تراجُع كامل.";
                report.MigrationCountAfter = await CountMigrationsAsync(db, ct);
                return report;
            }

            await tx.CommitAsync(ct);
            report.Outcome = Outcome.Applied;
            report.Applied = true;
        }
        else
        {
            report.Outcome = Outcome.Planned;
            await tx.RollbackAsync(ct);
        }

        report.MigrationCountAfter = await CountMigrationsAsync(db, ct);
        return report;
    }

    private static async Task EvaluateDraftsAsync(
        AppDbContext db, Guid templateId, Guid targetVersionId, ReportTemplateVersion target,
        JsonObject srcCfg, IReadOnlyList<string> keysToMove, Report report, CancellationToken ct)
    {
        var candidates = await (
            from s in db.ReportSubmissions
            join v in db.ReportTemplateVersions on s.ReportTemplateVersionId equals v.Id
            where v.ReportTemplateId == templateId
                  && s.Status == SubmissionStatus.Draft
                  && !s.IsDeleted
                  && s.ReportTemplateVersionId != targetVersionId
            select new { s, FromVer = v.VersionNumber, FromVerId = v.Id }).ToListAsync(ct);

        foreach (var c in candidates)
        {
            var values = await db.SubmissionFieldValues
                .Where(fv => fv.ReportSubmissionId == c.s.Id).ToListAsync(ct);

            var nonEmpty = values.Count(IsNonEmpty);

            var attachmentValueIds = await (
                from fv in db.SubmissionFieldValues
                join tf in db.TemplateFields on fv.TemplateFieldId equals tf.Id
                where fv.ReportSubmissionId == c.s.Id
                      && (tf.FieldType == FieldType.FileUpload || tf.FieldType == FieldType.Image)
                select fv.Id).ToListAsync(ct);
            var attachments = values.Count(v => attachmentValueIds.Contains(v.Id) && IsNonEmpty(v));

            var approvalSteps = await db.ApprovalSteps.CountAsync(a => a.ReportSubmissionId == c.s.Id, ct);

            var uniqueConflict = await db.ReportSubmissions.AnyAsync(x =>
                x.Id != c.s.Id && x.ReportTemplateVersionId == targetVersionId
                && x.SubmitterId == c.s.SubmitterId && x.PeriodKey == c.s.PeriodKey && !x.IsDeleted, ct);

            var before = PayloadMd5(values);

            // خريطة الحقول القديم ⇒ الجديد بالمفتاح ثمّ بالتسمية+النوع+الترتيب.
            var oldFields = await db.TemplateFields
                .Where(f => f.ReportTemplateVersionId == c.FromVerId).ToListAsync(ct);
            var unmapped = new List<string>();
            var map = new Dictionary<Guid, TemplateField>();
            foreach (var of in oldFields)
            {
                var nf = target.Fields.FirstOrDefault(x =>
                            !string.IsNullOrEmpty(of.Key) && x.Key == of.Key && x.FieldType == of.FieldType)
                      ?? target.Fields.FirstOrDefault(x =>
                            x.Label == of.Label && x.FieldType == of.FieldType && x.Order == of.Order);
                if (nf is null)
                {
                    if (values.Any(v => v.TemplateFieldId == of.Id && IsNonEmpty(v)))
                        unmapped.Add($"{of.Key ?? of.Label} [{of.FieldType}]");
                    continue;
                }
                map[of.Id] = nf;
            }

            // تحويل حمولة قسم المشاريع sv1 ⇒ sv2 (محاكاة لحساب البصمة البعديّة).
            var projectEntries = 0;
            var workItemsAfter = 0;
            string afterMd5;
            var simulated = new List<(Guid newFieldId, SubmissionFieldValue v, string? newJson)>();
            var transformFailed = (string?)null;

            foreach (var v in values)
            {
                if (!map.TryGetValue(v.TemplateFieldId, out var nf)) { simulated.Add((Guid.Empty, v, v.ValueJson)); continue; }
                string? newJson = v.ValueJson;
                if (nf.FieldType == FieldType.ProjectRepeatableSection && !string.IsNullOrWhiteSpace(v.ValueJson))
                {
                    try
                    {
                        var (converted, entries, items) = ConvertPayload(v.ValueJson!, keysToMove);
                        newJson = converted; projectEntries = entries; workItemsAfter = items;
                    }
                    catch (Exception ex) { transformFailed = ex.Message; }
                }
                simulated.Add((nf.Id, v, newJson));
            }
            afterMd5 = PayloadMd5(simulated.Select(s => CloneWith(s.v, s.newJson)).ToList());

            var reasons = new List<string>();
            if (c.s.SubmittedAtUtc is not null) reasons.Add("سبق إرسالها (SubmittedAtUtc)");
            if (c.s.ClosedAtUtc is not null) reasons.Add("مُغلقة (ClosedAtUtc)");
            if (attachments > 0) reasons.Add($"تحوي {attachments} مرفقًا");
            if (approvalSteps > 0) reasons.Add($"مرتبطة بـ {approvalSteps} خطوة اعتماد");
            if (uniqueConflict) reasons.Add("تعارض القيد الفريد على الإصدار الهدف");
            if (unmapped.Count > 0) reasons.Add($"حقول تحمل قيمًا ولا مقابل لها في الإصدار الخليفة: {string.Join(", ", unmapped)}");
            if (transformFailed is not null) reasons.Add($"تعذّر تحويل الحمولة: {transformFailed}");

            var eligible = reasons.Count == 0;
            report.Drafts.Add(new DraftDecision(
                c.s.Id, c.s.PeriodKey, c.s.SubmitterId, c.FromVer, c.s.Status.ToString(),
                values.Count, nonEmpty, attachments, approvalSteps, uniqueConflict,
                nonEmpty == 0, projectEntries, workItemsAfter, before, afterMd5,
                eligible,
                eligible
                    ? (nonEmpty == 0 ? "فارغة — إعادة ربط بلا تغيير قيم." : "مأهولة — إعادة ربط مع ترحيل answers ⇒ workItems[0] بلا فقد قيمة.")
                    : $"محظورة — لا تُمسّ: {string.Join("؛ ", reasons)}.",
                Processed: false));
        }
    }

    private static async Task ApplyDraftsAsync(AppDbContext db, ReportTemplateVersion target, Report report, CancellationToken ct)
    {
        var processed = new List<DraftDecision>();
        foreach (var d in report.Drafts)
        {
            if (!d.Eligible) { processed.Add(d); continue; }

            var sub = await db.ReportSubmissions.FirstAsync(x => x.Id == d.SubmissionId, ct);
            var oldVersionId = sub.ReportTemplateVersionId;
            var oldFields = await db.TemplateFields.Where(f => f.ReportTemplateVersionId == oldVersionId).ToListAsync(ct);
            var values = await db.SubmissionFieldValues.Where(fv => fv.ReportSubmissionId == d.SubmissionId).ToListAsync(ct);

            foreach (var v in values)
            {
                var of = oldFields.FirstOrDefault(f => f.Id == v.TemplateFieldId);
                if (of is null) continue;
                var nf = target.Fields.FirstOrDefault(x =>
                            !string.IsNullOrEmpty(of.Key) && x.Key == of.Key && x.FieldType == of.FieldType)
                      ?? target.Fields.FirstOrDefault(x =>
                            x.Label == of.Label && x.FieldType == of.FieldType && x.Order == of.Order);
                if (nf is null) continue;

                if (nf.FieldType == FieldType.ProjectRepeatableSection && !string.IsNullOrWhiteSpace(v.ValueJson))
                {
                    var keys = MovedKeysOf(nf.ConfigJson);
                    var (converted, _, _) = ConvertPayload(v.ValueJson!, keys);
                    v.ValueJson = converted;
                }
                v.TemplateFieldId = nf.Id;
                v.UpdatedAtUtc = DateTime.UtcNow;
            }

            // Submission ID والمستخدم والفترة والحالة والتواريخ تبقى كما هي — يتغيّر ربط الإصدار فقط.
            sub.ReportTemplateVersionId = target.Id;
            processed.Add(d with { Processed = true });
        }
        report.Drafts.Clear();
        report.Drafts.AddRange(processed);
    }

    /// <summary>يحوّل حمولة قسم المشاريع من schemaVersion 1 إلى 2 بلا فقد قيمة.</summary>
    public static (string json, int projectEntries, int workItems) ConvertPayload(string valueJson, IReadOnlyList<string> keysToMove)
    {
        var root = JsonNode.Parse(valueJson);
        if (root is not JsonArray arr) return (valueJson, 0, 0);

        var totalItems = 0;
        foreach (var node in arr)
        {
            if (node is not JsonObject entry) continue;
            if (entry["workItems"] is JsonArray existing) { totalItems += existing.Count; continue; }

            var answers = entry["answers"] as JsonObject ?? new JsonObject();
            var itemAnswers = new JsonObject();
            var keepAnswers = new JsonObject();
            foreach (var kv in answers)
            {
                var clone = kv.Value is null ? null : JsonNode.Parse(kv.Value.ToJsonString());
                if (keysToMove.Contains(kv.Key)) itemAnswers[kv.Key] = clone;
                else keepAnswers[kv.Key] = clone;
            }

            entry["answers"] = keepAnswers;
            entry["workItems"] = itemAnswers.Count > 0
                ? new JsonArray(new JsonObject { ["answers"] = itemAnswers })
                : new JsonArray();
            totalItems += itemAnswers.Count > 0 ? 1 : 0;
        }
        return (arr.ToJsonString(Pretty), arr.Count, totalItems);
    }

    private static List<string> MovedKeysOf(string? configJson)
    {
        var res = new List<string>();
        if (string.IsNullOrWhiteSpace(configJson)) return res;
        var wi = JsonNode.Parse(configJson)?["workItems"]?["fields"] as JsonArray;
        foreach (var f in wi ?? new JsonArray()) res.Add(f?["key"]?.GetValue<string>() ?? "");
        return res;
    }

    private static SubmissionFieldValue CloneWith(SubmissionFieldValue v, string? json) => new()
    {
        Id = v.Id,
        TemplateFieldId = v.TemplateFieldId,
        ValueText = v.ValueText,
        ValueNumber = v.ValueNumber,
        ValueDate = v.ValueDate,
        ValueBool = v.ValueBool,
        ValueJson = json,
    };

    private static bool IsNonEmpty(SubmissionFieldValue v)
    {
        if (v.ValueNumber is not null) return true;
        if (v.ValueDate is not null) return true;
        if (v.ValueBool is not null) return true;
        if (!string.IsNullOrWhiteSpace(v.ValueText)) return true;
        if (!string.IsNullOrWhiteSpace(v.ValueJson) && v.ValueJson!.Trim() is not ("[]" or "{}" or "null")) return true;
        return false;
    }

    /// <summary>بصمة القيم مستقلّة عن معرّفات الحقول — تُثبت أنّ القيم نفسها لم تُفقد.</summary>
    private static string PayloadMd5(IReadOnlyList<SubmissionFieldValue> values)
    {
        var parts = values
            .Select(v => string.Join("~",
                v.ValueText ?? "", v.ValueNumber?.ToString() ?? "", v.ValueDate?.ToString("O") ?? "",
                v.ValueBool?.ToString() ?? "", Canonical(v.ValueJson)))
            .OrderBy(x => x, StringComparer.Ordinal);
        return Md5(string.Join("|", parts));
    }

    /// <summary>تطبيع الحمولة لمقارنة القيم لا البنية: يستخرج كلّ أزواج (مفتاح=قيمة) الورقيّة مرتّبة.</summary>
    private static string Canonical(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "";
        try
        {
            var leaves = new List<string>();
            Walk(JsonNode.Parse(json), "", leaves);
            leaves.Sort(StringComparer.Ordinal);
            return string.Join(";", leaves);
        }
        catch { return json!; }

        static void Walk(JsonNode? n, string key, List<string> acc)
        {
            switch (n)
            {
                case JsonObject o:
                    foreach (var kv in o) Walk(kv.Value, kv.Key, acc);
                    break;
                case JsonArray a:
                    foreach (var e in a) Walk(e, key, acc);
                    break;
                case null: break;
                default: acc.Add($"{key}={n.ToJsonString()}"); break;
            }
        }
    }

    private static async Task<(int count, string fp)> HistoricalFingerprintAsync(AppDbContext db, Guid templateId, CancellationToken ct)
    {
        var rows = await (
            from s in db.ReportSubmissions
            join v in db.ReportTemplateVersions on s.ReportTemplateVersionId equals v.Id
            where v.ReportTemplateId == templateId && s.Status != SubmissionStatus.Draft
            select new { s.Id, v.VersionNumber, s.Status, s.PeriodKey, s.SubmittedAtUtc, s.ClosedAtUtc }).ToListAsync(ct);

        var valueRows = await (
            from fv in db.SubmissionFieldValues
            join s in db.ReportSubmissions on fv.ReportSubmissionId equals s.Id
            join v in db.ReportTemplateVersions on s.ReportTemplateVersionId equals v.Id
            where v.ReportTemplateId == templateId && s.Status != SubmissionStatus.Draft
            select new { fv.ReportSubmissionId, fv.TemplateFieldId, fv.ValueText, fv.ValueNumber, fv.ValueDate, fv.ValueBool, fv.ValueJson }).ToListAsync(ct);

        var sb = rows.OrderBy(r => r.Id)
            .Select(r => $"{r.Id}~{r.VersionNumber}~{r.Status}~{r.PeriodKey}~{r.SubmittedAtUtc:O}~{r.ClosedAtUtc:O}")
            .Concat(valueRows.OrderBy(r => r.ReportSubmissionId).ThenBy(r => r.TemplateFieldId)
                .Select(r => $"{r.ReportSubmissionId}~{r.TemplateFieldId}~{r.ValueText}~{r.ValueNumber}~{r.ValueDate:O}~{r.ValueBool}~{r.ValueJson}"));

        return (rows.Count, Md5(string.Join("|", sb)));
    }

    private static async Task<int> CountMigrationsAsync(AppDbContext db, CancellationToken ct)
        => (await db.Database.SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM \"__EFMigrationsHistory\"").ToListAsync(ct))[0];

    private static string Md5(string s)
        => Convert.ToHexString(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
}
