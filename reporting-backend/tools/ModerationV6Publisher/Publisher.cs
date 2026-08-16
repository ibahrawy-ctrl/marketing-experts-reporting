using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.EntityFrameworkCore;
using Reporting.Domain.Entities.Submissions;
using Reporting.Domain.Entities.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

namespace ModerationV6Publisher;

/// <summary>
/// النتيجة النهائية لتشغيل الأداة.
/// </summary>
public enum PublishOutcome
{
    /// <summary>Dry-Run: تُظهر الخطة ولا تكتب شيئًا.</summary>
    Planned,
    /// <summary>--apply: نُشِر V6 (وأُعيد توجيه المسودّات المؤهَّلة) داخل معاملة واحدة.</summary>
    Applied,
    /// <summary>V6 موجود ومطابق للعقد تمامًا — لا تغيير (idempotent).</summary>
    AlreadyApplied,
    /// <summary>يوجد إصدار يحمل content_highlights لكنه لا يطابق عقد V6 — توقّف.</summary>
    ContractMismatch,
    /// <summary>القالب أو إصدار V5 الصحيح غير موجود — توقّف.</summary>
    V5NotFound,
}

/// <summary>قرار إعادة توجيه مسودّة واحدة (W30 وأمثالها) من V5 إلى V6.</summary>
public sealed record RepointDecision(
    Guid SubmissionId,
    string PeriodKey,
    Guid SubmitterId,
    int FromVersionNumber,
    int ValueCount,
    int NonEmptyValueCount,
    int AttachmentCount,
    int ReferenceCount,
    bool UniqueConflict,
    bool Eligible,
    string Reason,
    bool Repointed);

/// <summary>تقرير تشغيل الأداة — يُطبَع كاملًا في Dry-Run و--apply.</summary>
public sealed class PublisherReport
{
    public PublishOutcome Outcome { get; set; }
    public bool Applied { get; set; }
    public Guid TemplateId { get; set; }
    public Guid? V5VersionId { get; set; }
    public int? V5VersionNumber { get; set; }
    public int V5FieldCount { get; set; }
    public int ProposedV6FieldCount { get; set; }
    public Guid? V6VersionId { get; set; }
    public int? V6VersionNumber { get; set; }
    public List<string> AddedKeys { get; } = new();
    public List<RepointDecision> Repoints { get; } = new();
    public int MigrationCountBefore { get; set; }
    public int MigrationCountAfter { get; set; }
    public List<string> Notes { get; } = new();
    public string? BlockReason { get; set; }
}

/// <summary>
/// نواة نشر إصدار V6 لقالب المديرشن — إضافة 6 حقول تحليل محتوى فقط فوق الـ15 القائمة،
/// بلا حذف/تعديل/إعادة تسمية لأي حقل V5، بلا Migration، وبإعادة توجيه المسودّات الفارغة المؤهَّلة بأمان.
///
/// القرارات المفروضة:
/// • idempotent: إن وُجد V6 مطابق ⟶ AlreadyApplied (لا كتابة). إن وُجد V6 مختلف ⟶ ContractMismatch (توقّف).
/// • معاملة واحدة + قفل advisory يمنع التشغيل المتزامن.
/// • لا حذف نهائيًّا لأي مسودّة — إعادة توجيه فقط، وبعد اجتياز كل بوّابات السلامة؛ وإلا تُترك كما هي مع طبع سبب الحظر.
/// </summary>
public static class Publisher
{
    /// <summary>معرّف قالب «تقرير المديرشن الأسبوعي» على الإنتاج (الافتراضي).</summary>
    public static readonly Guid ModerationTemplateId = Guid.Parse("db8c764d-6f10-4997-8c07-512334dad30b");

    // مفتاح قفل advisory ثابت خاص بهذه الأداة (يمنع تشغيلين متزامنين على نفس القاعدة).
    private const long AdvisoryLockKey = 0x4D4F44_5636L; // "MOD" + "V6"

    // الـ15 مفتاحًا القائمة في V5 — يجب أن تبقى كلها دون مساس.
    public static readonly string[] V5Keys =
    {
        "project_status", "time_consumption",
        "incoming_messages", "answered_messages", "avg_response_minutes",
        "problematic_comments", "escalations", "complaints", "converted_opportunities",
        "cases_grid",
        "done", "issues", "recurring_questions", "next_week", "recommendations",
    };

    // الحقول الستة الجديدة (عقد V6) — تُضاف فقط، كلها required=false، بلا أي رقم/نسبة/KPI.
    public static readonly string[] NewKeys =
    {
        "content_highlights", "audience_insight", "lessons_learned",
        "decisions_required", "risk_exists", "risk_note",
    };

    public static readonly string[] ContentHighlightsColumns =
    {
        "التصنيف", "المنصة", "نوع المحتوى", "رابط المحتوى أو تعريفه",
        "لماذا تم اختياره؟", "الدرس المستفاد أو الإجراء المقترح",
    };

    public static readonly string[] RiskExistsOptions = { "نعم", "لا" };

    private sealed record NewSub(string Key, string Label, string Type, string[]? Columns, string[]? Options);

    private static readonly NewSub[] NewSubs =
    {
        new("content_highlights", "تحليل المحتوى (أفضل/أضعف)", "Grid", ContentHighlightsColumns, null),
        new("audience_insight", "أبرز ملاحظات الجمهور", "LongText", null, null),
        new("lessons_learned", "الدروس المستفادة", "LongText", null, null),
        new("decisions_required", "القرارات المطلوبة من الإدارة", "LongText", null, null),
        new("risk_exists", "هل يوجد خطر على المشروع؟", "Select", null, RiskExistsOptions),
        new("risk_note", "ما هو الخطر وما المطلوب؟", "LongText", null, null),
    };

    private static readonly JsonSerializerOptions JsonWrite = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
        // مطلوب في .NET 8 كي يُسلسِل JsonNode المُنشأ يدويًّا (JsonValue) دون رمي TypeInfoResolver.
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    /// <summary>
    /// نفّذ الخطة. apply=false ⟶ Dry-Run (معاملة تُلغى دائمًا). apply=true ⟶ Commit عند النجاح.
    /// </summary>
    public static async Task<PublisherReport> RunAsync(AppDbContext db, Guid templateId, bool apply, CancellationToken ct = default)
    {
        var report = new PublisherReport { TemplateId = templateId, Applied = false };

        // عدّ الهجرات قبل — الأداة لا تشغّل أي هجرة إطلاقًا (يجب أن يبقى ثابتًا).
        report.MigrationCountBefore = await CountMigrationsAsync(db, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // قفل advisory على مستوى المعاملة — حماية من التشغيل المتزامن.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", new object[] { AdvisoryLockKey }, ct);

        var template = await db.ReportTemplates
            .Include(t => t.Versions).ThenInclude(v => v.Fields)
            .FirstOrDefaultAsync(t => t.Id == templateId, ct);

        if (template is null)
        {
            report.Outcome = PublishOutcome.V5NotFound;
            report.BlockReason = $"القالب {templateId} غير موجود.";
            await tx.RollbackAsync(ct);
            report.MigrationCountAfter = report.MigrationCountBefore;
            return report;
        }

        // كشف إصدار V6 قائم = إصدار يحوي حقلًا فرعيًّا مفتاحه content_highlights داخل ProjectRepeatableSection.
        ReportTemplateVersion? existingV6 = null;
        foreach (var v in template.Versions)
        {
            var prs = v.Fields.FirstOrDefault(f => f.FieldType == FieldType.ProjectRepeatableSection);
            if (prs is null) continue;
            var subs = ParseSubKeys(prs.ConfigJson);
            if (subs.ContainsKey("content_highlights")) { existingV6 = v; break; }
        }

        if (existingV6 is not null)
        {
            var prs = existingV6.Fields.First(f => f.FieldType == FieldType.ProjectRepeatableSection);
            var (isFullV6, mismatchReason) = ValidateV6Contract(prs.ConfigJson);
            report.V6VersionId = existingV6.Id;
            report.V6VersionNumber = existingV6.VersionNumber;
            if (isFullV6)
            {
                report.Outcome = PublishOutcome.AlreadyApplied;
                report.Notes.Add($"إصدار V6 مطابق للعقد موجود مسبقًا (v{existingV6.VersionNumber}) — لا تغيير (idempotent).");
            }
            else
            {
                report.Outcome = PublishOutcome.ContractMismatch;
                report.BlockReason = $"إصدار يحوي content_highlights موجود (v{existingV6.VersionNumber}) لكنه لا يطابق عقد V6: {mismatchReason}";
            }
            await tx.RollbackAsync(ct);
            report.MigrationCountAfter = report.MigrationCountBefore;
            return report;
        }

        // لا V6 بعد — تحقّق من V5 الصحيح (أحدث إصدار منشور يحمل الـ15 مفتاحًا داخل ProjectRepeatableSection).
        var v5 = template.Versions
            .Where(v => v.IsPublished)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault();

        if (v5 is null)
        {
            report.Outcome = PublishOutcome.V5NotFound;
            report.BlockReason = "لا يوجد إصدار منشور للقالب.";
            await tx.RollbackAsync(ct);
            report.MigrationCountAfter = report.MigrationCountBefore;
            return report;
        }

        var v5Prs = v5.Fields.FirstOrDefault(f => f.FieldType == FieldType.ProjectRepeatableSection);
        if (v5Prs is null)
        {
            report.Outcome = PublishOutcome.V5NotFound;
            report.BlockReason = $"الإصدار المنشور v{v5.VersionNumber} لا يحوي ProjectRepeatableSection.";
            await tx.RollbackAsync(ct);
            report.MigrationCountAfter = report.MigrationCountBefore;
            return report;
        }

        var v5Subs = ParseSubKeys(v5Prs.ConfigJson);
        var missing = V5Keys.Where(k => !v5Subs.ContainsKey(k)).ToList();
        if (missing.Count > 0)
        {
            report.Outcome = PublishOutcome.V5NotFound;
            report.BlockReason = $"الإصدار المنشور v{v5.VersionNumber} لا يطابق عقد V5 — مفاتيح ناقصة: {string.Join(", ", missing)}.";
            await tx.RollbackAsync(ct);
            report.MigrationCountAfter = report.MigrationCountBefore;
            return report;
        }

        report.V5VersionId = v5.Id;
        report.V5VersionNumber = v5.VersionNumber;
        report.V5FieldCount = v5.Fields.Count;
        report.AddedKeys.AddRange(NewKeys);
        report.ProposedV6FieldCount = v5.Fields.Count; // نفس عدد الحقول العليا؛ الإضافة داخل ProjectRepeatableSection.

        // بناء ConfigJson لإصدار V6: نسخ حرفيّ لإعداد V5 مع إلحاق الحقول الستة فقط (الـ15 تبقى byte-identical).
        var v6ConfigJson = BuildV6Config(v5Prs.ConfigJson);
        var v6SubCount = ParseSubKeys(v6ConfigJson).Count;

        // مسودّات ما قبل V6 المرشّحة لإعادة التوجيه (Draft فقط، غير محذوفة).
        var v6VersionNumber = template.Versions.Max(v => v.VersionNumber) + 1;

        // إنشاء صفّ إصدار V6 (نسخ كل حقول V5؛ استبدال ConfigJson لحقل القسم المتكرر فقط).
        var v6 = new ReportTemplateVersion
        {
            ReportTemplateId = template.Id,
            VersionNumber = v6VersionNumber,
            IsPublished = false,
        };
        foreach (var f in v5.Fields.OrderBy(f => f.Order))
        {
            v6.Fields.Add(new TemplateField
            {
                Label = f.Label,
                Key = f.Key,
                FieldType = f.FieldType,
                IsRequired = f.IsRequired,
                HelpText = f.HelpText,
                Order = f.Order,
                ConfigJson = f.FieldType == FieldType.ProjectRepeatableSection ? v6ConfigJson : f.ConfigJson,
            });
        }

        // تقييم إعادة توجيه المسودّات (بوّابات السلامة الصارمة) — نحتاج معرّف V6 المستقبلي؛
        // نضيفه للسياق أولًا كي نحصل على Id، ثم نقيّم التعارض الفريد ضدّه.
        db.ReportTemplateVersions.Add(v6);

        var candidates = await (
            from s in db.ReportSubmissions
            join v in db.ReportTemplateVersions on s.ReportTemplateVersionId equals v.Id
            where v.ReportTemplateId == templateId
                  && s.Status == SubmissionStatus.Draft
                  && s.ReportTemplateVersionId != v6.Id
            select new { s, FromVer = v.VersionNumber }).ToListAsync(ct);

        foreach (var c in candidates)
        {
            var decision = await EvaluateRepointAsync(db, c.s, c.FromVer, v6.Id, templateId, ct);
            report.Repoints.Add(decision);
        }

        report.V6VersionId = v6.Id;
        report.V6VersionNumber = v6VersionNumber;

        if (apply)
        {
            // نشر V6 بعد اجتياز كل التحقّقات.
            v6.IsPublished = true;
            v6.PublishedAtUtc = DateTime.UtcNow;

            // إعادة توجيه المسودّات المؤهَّلة فقط (لا حذف مطلقًا).
            var repointed = new List<RepointDecision>();
            foreach (var d in report.Repoints)
            {
                if (!d.Eligible) { repointed.Add(d); continue; }
                var sub = await db.ReportSubmissions.FirstAsync(x => x.Id == d.SubmissionId, ct);
                sub.ReportTemplateVersionId = v6.Id;
                repointed.Add(d with { Repointed = true });
            }
            report.Repoints.Clear();
            report.Repoints.AddRange(repointed);

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            report.Outcome = PublishOutcome.Applied;
            report.Applied = true;
        }
        else
        {
            report.Outcome = PublishOutcome.Planned;
            await tx.RollbackAsync(ct);
        }

        report.MigrationCountAfter = await CountMigrationsAsync(db, ct);
        return report;
    }

    private static async Task<RepointDecision> EvaluateRepointAsync(
        AppDbContext db, ReportSubmission sub, int fromVer, Guid v6Id, Guid templateId, CancellationToken ct)
    {
        // قيم الحقول لهذه المسودّة.
        var values = await db.SubmissionFieldValues
            .Where(fv => fv.ReportSubmissionId == sub.Id)
            .ToListAsync(ct);

        var valueCount = values.Count;
        var nonEmpty = values.Count(v => !IsEmptyValue(v));

        // المرفقات = قيم حقول من نوع رفع/صورة تحمل محتوى.
        var attachmentFieldIds = await (
            from fv in db.SubmissionFieldValues
            join tf in db.TemplateFields on fv.TemplateFieldId equals tf.Id
            where fv.ReportSubmissionId == sub.Id
                  && (tf.FieldType == FieldType.FileUpload || tf.FieldType == FieldType.Image)
            select fv.Id).ToListAsync(ct);
        var attachmentCount = values.Count(v => attachmentFieldIds.Contains(v.Id) && !IsEmptyValue(v));

        // مراجع تمنع الإعادة بأمان — خطوات الاعتماد المرتبطة بالمسودّة.
        var approvalSteps = await db.ApprovalSteps.CountAsync(a => a.ReportSubmissionId == sub.Id, ct);
        var referenceCount = approvalSteps;

        // تعارض القيد الفريد: تسليم غير محذوف آخر على V6 لنفس (Submitter, PeriodKey).
        var uniqueConflict = await db.ReportSubmissions.AnyAsync(x =>
            x.Id != sub.Id
            && x.ReportTemplateVersionId == v6Id
            && x.SubmitterId == sub.SubmitterId
            && x.PeriodKey == sub.PeriodKey
            && !x.IsDeleted, ct);

        var reasons = new List<string>();
        if (sub.Status != SubmissionStatus.Draft) reasons.Add("الحالة ليست Draft");
        if (sub.SubmittedAtUtc is not null) reasons.Add("سبق إرسالها (SubmittedAtUtc)");
        if (sub.ClosedAtUtc is not null) reasons.Add("مُغلقة (ClosedAtUtc)");
        if (nonEmpty > 0) reasons.Add($"تحوي {nonEmpty} قيمة غير فارغة");
        if (attachmentCount > 0) reasons.Add($"تحوي {attachmentCount} مرفقًا");
        if (approvalSteps > 0) reasons.Add($"مرتبطة بـ {approvalSteps} خطوة اعتماد");
        if (uniqueConflict) reasons.Add("تعارض مع القيد الفريد على V6 (نفس Submitter+PeriodKey)");

        var eligible = reasons.Count == 0;
        var reason = eligible
            ? "مؤهَّلة لإعادة التوجيه بأمان (مسودّة فارغة، بلا مرفقات/مراجع/تعارض)."
            : $"محظورة — لا حذف ولا إعادة توجيه: {string.Join("؛ ", reasons)}.";

        return new RepointDecision(
            sub.Id, sub.PeriodKey, sub.SubmitterId, fromVer,
            valueCount, nonEmpty, attachmentCount, referenceCount, uniqueConflict,
            eligible, reason, Repointed: false);
    }

    private static bool IsEmptyValue(SubmissionFieldValue v)
    {
        if (v.ValueNumber is not null) return false;
        if (v.ValueDate is not null) return false;
        if (v.ValueBool is not null) return false;
        if (!string.IsNullOrWhiteSpace(v.ValueText)) return false;
        if (!string.IsNullOrWhiteSpace(v.ValueJson) && !IsEmptyJson(v.ValueJson)) return false;
        return true;
    }

    // JSON فارغ فعليًّا: null / [] / {} / مصفوفة صفوف كلها خلايا فارغة / كائن answers كله فارغ.
    private static bool IsEmptyJson(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            return NodeIsEmpty(node);
        }
        catch { return false; } // JSON مشوّه ⇒ نعامله كـ«غير فارغ» احتياطًا (لا نعيد التوجيه).
    }

    private static bool NodeIsEmpty(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return true;
            case JsonArray arr:
                return arr.All(NodeIsEmpty);
            case JsonObject obj:
                return obj.All(kv => NodeIsEmpty(kv.Value));
            case JsonValue val:
                if (val.TryGetValue<string>(out var s)) return string.IsNullOrWhiteSpace(s);
                return false; // رقم/بوول ⇒ غير فارغ.
            default:
                return false;
        }
    }

    // مفاتيح الحقول الفرعية داخل ProjectRepeatableSection ⟶ (type, columns, options).
    private static Dictionary<string, (string Type, string[]? Columns, string[]? Options)> ParseSubKeys(string? configJson)
    {
        var result = new Dictionary<string, (string, string[]?, string[]?)>();
        if (string.IsNullOrWhiteSpace(configJson)) return result;
        try
        {
            var node = JsonNode.Parse(configJson)?.AsObject();
            if (node is null || node["fields"] is not JsonArray fields) return result;
            foreach (var f in fields)
            {
                if (f is not JsonObject o) continue;
                var key = o["key"]?.GetValue<string>();
                if (string.IsNullOrEmpty(key)) continue;
                var type = o["type"]?.GetValue<string>() ?? "";
                var cols = (o["columns"] as JsonArray)?.Select(x => x?.GetValue<string>() ?? "").ToArray();
                var opts = (o["options"] as JsonArray)?.Select(x => x?.GetValue<string>() ?? "").ToArray();
                result[key] = (type, cols, opts);
            }
        }
        catch { /* مشوّه ⇒ خريطة فارغة */ }
        return result;
    }

    private static (bool IsFull, string Reason) ValidateV6Contract(string? configJson)
    {
        var subs = ParseSubKeys(configJson);

        var missing15 = V5Keys.Where(k => !subs.ContainsKey(k)).ToList();
        if (missing15.Count > 0) return (false, $"مفاتيح V5 ناقصة: {string.Join(", ", missing15)}");

        var missingNew = NewKeys.Where(k => !subs.ContainsKey(k)).ToList();
        if (missingNew.Count > 0) return (false, $"مفاتيح V6 ناقصة: {string.Join(", ", missingNew)}");

        if (subs["content_highlights"].Type != "Grid") return (false, "content_highlights ليس من نوع Grid");
        if (subs["audience_insight"].Type != "LongText") return (false, "audience_insight ليس LongText");
        if (subs["lessons_learned"].Type != "LongText") return (false, "lessons_learned ليس LongText");
        if (subs["decisions_required"].Type != "LongText") return (false, "decisions_required ليس LongText");
        if (subs["risk_note"].Type != "LongText") return (false, "risk_note ليس LongText");
        if (subs["risk_exists"].Type != "Select") return (false, "risk_exists ليس Select");

        var opts = subs["risk_exists"].Options ?? Array.Empty<string>();
        if (!opts.SequenceEqual(RiskExistsOptions)) return (false, "خيارات risk_exists لا تطابق (نعم/لا)");

        return (true, "مطابق");
    }

    // ينسخ إعداد V5 حرفيًّا ويلحق الحقول الستة فقط في نهاية مصفوفة fields.
    private static string BuildV6Config(string? v5ConfigJson)
    {
        var node = JsonNode.Parse(string.IsNullOrWhiteSpace(v5ConfigJson) ? "{}" : v5ConfigJson)!.AsObject();
        if (node["fields"] is not JsonArray fields)
        {
            fields = new JsonArray();
            node["fields"] = fields;
        }
        foreach (var ns in NewSubs)
        {
            var o = new JsonObject
            {
                ["key"] = ns.Key,
                ["label"] = ns.Label,
                ["type"] = ns.Type,
                ["required"] = false,
            };
            if (ns.Columns is not null)
            {
                var a = new JsonArray();
                foreach (var c in ns.Columns) a.Add(c);
                o["columns"] = a;
            }
            if (ns.Options is not null)
            {
                var a = new JsonArray();
                foreach (var op in ns.Options) a.Add(op);
                o["options"] = a;
            }
            fields.Add(o);
        }
        return node.ToJsonString(JsonWrite);
    }

    private static async Task<int> CountMigrationsAsync(AppDbContext db, CancellationToken ct)
    {
        try
        {
            return await db.Database
                .SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM \"__EFMigrationsHistory\"")
                .FirstAsync(ct);
        }
        catch { return -1; }
    }
}
