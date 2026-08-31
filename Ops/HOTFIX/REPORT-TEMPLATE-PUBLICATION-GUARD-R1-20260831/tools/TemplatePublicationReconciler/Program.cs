using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Audit;
using Reporting.Infrastructure.Persistence;
using Reporting.Infrastructure.Services;
using TemplatePublicationReconciler;

// ============================================================================
// REPORT_TEMPLATE_PUBLICATION_GUARD_HOTFIX_R1 — أداة مصالحة نشر إصدارات القوالب
// ----------------------------------------------------------------------------
// الغرض: إعادة مجموعة الإصدارات المنشورة لعائلة قالب إلى حالتها المرجعيّة قبل عطب
// حارس البذر — لا اختيار «فائز» جديد، بل استعادة قرار نشر سابق أُلغي بلا إذن.
//
// العقد:
//   • Dry-Run افتراضيّ — لا كتابة إطلاقًا بلا --apply.
//   • تعمل على القوالب المسمّاة صراحةً في الخطّة وحدها.
//   • تمرّ بالمسار الرسميّ ReportTemplateService.PublishVersionAsync حصرًا.
//   • Idempotent — كلّ إصدار منشور سلفًا يُعَدّ ALREADY_PUBLISHED بلا أيّ كتابة.
//   • لا تحذف إصدارًا، ولا تُلغي نشر أيّ إصدار إطلاقًا (النشر فعل توسيعيّ فقط).
//   • ترفض التنفيذ كلّه إن خالفت البيانات البصمات المتوقَّعة (حارس البصمات).
//   • ترفض إن وُجد إصدار منشور خارج المجموعة المتوقَّعة (يستلزم إلغاء نشر ⟸ قرار منتج).
//   • لا تلمس أيّ تسليم (Draft/Submitted/Closed) وتُثبت ذلك قبل/بعد.
//   • تكتب تدقيقًا واضحًا في audit_logs + تقرير JSON قبل/بعد.
//
//   dotnet TemplatePublicationReconciler.dll --env-file /etc/reporting-api.env \
//       --plan plan.json --out report.json [--apply] [--verify-only]
// ============================================================================

string? envFile = null, planPath = null, outPath = null, connArg = null;
var apply = false;
var verifyOnly = false;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--env-file": envFile = args[++i]; break;
        case "--conn": connArg = args[++i]; break;
        case "--plan": planPath = args[++i]; break;
        case "--out": outPath = args[++i]; break;
        case "--apply": apply = true; break;
        case "--verify-only": verifyOnly = true; break;
        default: Console.Error.WriteLine($"وسيط غير معروف: {args[i]}"); return 2;
    }
}

if (planPath is null || outPath is null || (envFile is null && connArg is null))
{
    Console.Error.WriteLine("الاستعمال: --env-file <path> | --conn <cs>  --plan <plan.json>  --out <report.json>  [--apply] [--verify-only]");
    return 2;
}

if (apply && verifyOnly)
{
    Console.Error.WriteLine("--apply و--verify-only متعارضان.");
    return 2;
}

var mode = verifyOnly ? "VERIFY_ONLY" : apply ? "APPLY" : "DRY_RUN";
Console.WriteLine($"== وضع التشغيل: {mode} ==");

var conn = connArg ?? ReadConnectionString(envFile!);
if (string.IsNullOrWhiteSpace(conn))
{
    Console.Error.WriteLine("تعذّر قراءة ConnectionStrings__Default من ملفّ البيئة.");
    return 2;
}

var plan = JsonNode.Parse(await File.ReadAllTextAsync(planPath))!.AsObject();
var expectedDb = plan["database"]?.GetValue<string>();

var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(conn).Options;
await using var db = new AppDbContext(options);

// ---- حارس الهويّة: يمنع التنفيذ على قاعدة غير المقصودة -----------------------
var actualDb = (await db.Database.SqlQueryRaw<string>("SELECT current_database() AS \"Value\"").ToListAsync())[0];
if (expectedDb is not null && actualDb != expectedDb)
{
    Console.Error.WriteLine($"حارس الهويّة: الخطّة لـ«{expectedDb}» والاتّصال على «{actualDb}» — أُوقِف التنفيذ.");
    return 3;
}

// ---- الفاعل: مطلوب لتسجيل PublishedById والتدقيق ----------------------------
Guid actorId;
var actorEmail = plan["actorEmail"]?.GetValue<string>();
if (plan["actorId"]?.GetValue<string>() is { } rawActorId && Guid.TryParse(rawActorId, out var parsedActor))
{
    actorId = parsedActor;
}
else if (actorEmail is not null)
{
    var found = await db.Database
        .SqlQueryRaw<Guid>("SELECT \"Id\" AS \"Value\" FROM \"AspNetUsers\" WHERE lower(\"Email\") = lower({0})", actorEmail)
        .ToListAsync();
    if (found.Count != 1)
    {
        Console.Error.WriteLine($"تعذّر تعيين الفاعل من البريد «{actorEmail}» (النتائج: {found.Count}).");
        return 3;
    }
    actorId = found[0];
}
else
{
    Console.Error.WriteLine("الخطّة تفتقر إلى actorId أو actorEmail.");
    return 2;
}

// ---- المسار الرسميّ للنشر ---------------------------------------------------
IAuditService audit = new AuditService(db);
var templateService = new ReportTemplateService(db, new OfflineCurrentUser(actorId), new OfflineScopeResolver(), audit);

var results = new JsonArray();
var refusals = 0;
var toPublish = new List<(Guid VersionId, string Title, int VersionNumber, DateTime? OriginalPublishedAtUtc)>();

// ============================ المرور الأوّل: التحقّق ==========================
foreach (var node in plan["templates"]!.AsArray())
{
    var item = node!.AsObject();
    var title = item["title"]!.GetValue<string>();
    var templateId = Guid.Parse(item["templateId"]!.GetValue<string>());
    var effectiveExpected = item["effectiveVersionNumber"]!.GetValue<int>();
    var expectedSet = item["expectedPublishedVersionNumbers"]!.AsArray()
        .Select(n => n!.GetValue<int>()).OrderBy(n => n).ToList();

    var entry = new JsonObject
    {
        ["title"] = title,
        ["templateId"] = templateId.ToString(),
        ["effectiveVersionNumberExpected"] = effectiveExpected,
        ["expectedPublishedVersionNumbers"] = new JsonArray(expectedSet.Select(n => (JsonNode)n).ToArray()),
        ["applicabilityFloorExpectedUtc"] = item["applicabilityFloorUtc"]?.GetValue<string>()
    };
    results.Add(entry);

    var template = await db.ReportTemplates
        .Include(t => t.Versions).ThenInclude(v => v.Fields)
        .AsNoTracking()
        .FirstOrDefaultAsync(t => t.Id == templateId);

    if (template is null) { Refuse(entry, "TEMPLATE_NOT_FOUND", $"لا قالب بالمعرّف {templateId}."); continue; }
    if (template.Title != title) { Refuse(entry, "TITLE_MISMATCH", $"العنوان الفعليّ «{template.Title}» ≠ المتوقَّع «{title}»."); continue; }

    entry["before"] = FamilySnapshot(template.Versions);
    entry["effectiveBefore"] = EffectiveOf(template.Versions);
    entry["submissionsByVersionBefore"] = await SubmissionsSnapshotAsync(db, templateId);

    // (أ) لا إصدار منشور خارج المجموعة المتوقَّعة — وإلّا لزم إلغاء نشر وهو خارج النطاق.
    var publishedNow = template.Versions.Where(v => v.IsPublished).Select(v => v.VersionNumber).OrderBy(n => n).ToList();
    var extra = publishedNow.Except(expectedSet).ToList();
    if (extra.Count > 0)
    {
        Refuse(entry, "UNEXPECTED_PUBLISHED_VERSION",
            $"إصدارات منشورة خارج المجموعة المرجعيّة [{string.Join(",", extra.Select(n => "v" + n))}] — استعادتها تستلزم إلغاء نشر، وهو خارج نطاق الأداة ويلزمه قرار منتج.");
        continue;
    }

    // (ب) الفائز المتوقَّع هو الأعلى رقمًا في العائلة كلّها ⟹ عقد زمن التشغيل يضمنه.
    var maxNumber = template.Versions.Max(v => v.VersionNumber);
    if (effectiveExpected != maxNumber)
    { Refuse(entry, "EFFECTIVE_NOT_LATEST", $"الفائز المتوقَّع v{effectiveExpected} ليس الأعلى في العائلة (الأعلى v{maxNumber})."); continue; }
    if (!expectedSet.Contains(effectiveExpected))
    { Refuse(entry, "EFFECTIVE_NOT_IN_SET", $"الفائز المتوقَّع v{effectiveExpected} خارج المجموعة المرجعيّة."); continue; }

    // (ج) حارس البصمات لكلّ إصدار في المجموعة المرجعيّة.
    var pending = new JsonArray();
    var localRefusal = false;
    foreach (var vnode in item["versions"]!.AsArray())
    {
        var vi = vnode!.AsObject();
        var versionId = Guid.Parse(vi["versionId"]!.GetValue<string>());
        var versionNumber = vi["versionNumber"]!.GetValue<int>();
        var expectFieldCount = vi["expectFieldCount"]?.GetValue<int>();
        var expectWorkItems = vi["expectWorkItems"]?.GetValue<bool>() ?? false;
        var expectSchemaVersion2 = vi["expectSchemaVersion2"]?.GetValue<bool>() ?? false;

        var v = template.Versions.FirstOrDefault(x => x.Id == versionId);
        if (v is null) { Refuse(entry, "VERSION_NOT_IN_FAMILY", $"الإصدار {versionId} لا ينتمي للقالب."); localRefusal = true; break; }
        if (v.VersionNumber != versionNumber)
        { Refuse(entry, "VERSION_NUMBER_MISMATCH", $"رقم الإصدار الفعليّ {v.VersionNumber} ≠ المتوقَّع {versionNumber}."); localRefusal = true; break; }
        if (expectFieldCount is { } fc && v.Fields.Count != fc)
        { Refuse(entry, "FIELD_COUNT_MISMATCH", $"v{versionNumber}: عدد الحقول {v.Fields.Count} ≠ المتوقَّع {fc}."); localRefusal = true; break; }

        var blob = string.Concat(v.Fields.Select(f => (f.ConfigJson ?? string.Empty).Replace(" ", string.Empty)));
        if (expectWorkItems != blob.Contains("\"workItems\""))
        { Refuse(entry, "FINGERPRINT_WORKITEMS_MISMATCH", $"v{versionNumber}: بصمة workItems لا تطابق المتوقَّع ({expectWorkItems})."); localRefusal = true; break; }
        if (expectSchemaVersion2 != blob.Contains("\"schemaVersion\":2"))
        { Refuse(entry, "FINGERPRINT_SCHEMAVERSION_MISMATCH", $"v{versionNumber}: بصمة schemaVersion=2 لا تطابق المتوقَّع ({expectSchemaVersion2})."); localRefusal = true; break; }

        if (v.IsPublished)
        {
            pending.Add(new JsonObject { ["versionNumber"] = versionNumber, ["decision"] = "ALREADY_PUBLISHED" });
            continue;
        }

        pending.Add(new JsonObject
        {
            ["versionNumber"] = versionNumber,
            ["decision"] = verifyOnly ? "NOT_PUBLISHED" : apply ? "WILL_PUBLISH" : "WOULD_PUBLISH",
            ["originalPublishedAtUtc"] = v.PublishedAtUtc?.ToString("O")
        });
        if (!verifyOnly) toPublish.Add((versionId, title, versionNumber, v.PublishedAtUtc));
    }

    if (localRefusal) continue;
    entry["versions"] = pending;
    entry["decision"] = pending.Any(p => p!["decision"]!.GetValue<string>() is "WOULD_PUBLISH" or "WILL_PUBLISH" or "NOT_PUBLISHED")
        ? (verifyOnly ? "NOT_COMPLIANT" : apply ? "WILL_RECONCILE" : "WOULD_RECONCILE")
        : "ALREADY_COMPLIANT";
}

if (refusals > 0)
{
    Console.Error.WriteLine($"حارس البصمات: {refusals} بند/بنود مرفوضة — لن تُنفَّذ أيّ كتابة إطلاقًا.");
    await WriteReportAsync(outPath, mode, actualDb, actorId, results, refusals, "REFUSED");
    return 4;
}

if (verifyOnly)
{
    var compliant = results.All(r => r!["decision"]?.GetValue<string>() == "ALREADY_COMPLIANT");
    Console.WriteLine(compliant ? "التحقّق: كلّ القوالب مطابقة للمرجع." : "التحقّق: توجد قوالب غير مطابقة.");
    await WriteReportAsync(outPath, mode, actualDb, actorId, results, 0, compliant ? "VERIFIED_COMPLIANT" : "VERIFIED_NOT_COMPLIANT");
    return compliant ? 0 : 5;
}

// ============================ المرور الثاني: التنفيذ =========================
if (apply && toPublish.Count > 0)
{
    await using var tx = await db.Database.BeginTransactionAsync();
    foreach (var (versionId, title, versionNumber, originalPublishedAt) in toPublish)
    {
        var res = await templateService.PublishVersionAsync(versionId, actorId);
        if (!res.Succeeded)
        {
            Console.Error.WriteLine($"فشل نشر «{title}» v{versionNumber}: {res.Error}");
            await tx.RollbackAsync();
            await WriteReportAsync(outPath, mode, actualDb, actorId, results, 0, "PUBLISH_FAILED");
            return 6;
        }

        // استعادة تاريخ النشر الأصليّ: الحارس ألغى IsPublished ولم يمسّ PublishedAtUtc إطلاقًا،
        // بينما المسار الرسميّ يعيد كتابته بـ«الآن». وأرضيّة استحقاق القالب هي
        // MIN(PublishedAtUtc) على الإصدارات المنشورة (ExpectedSubmissionStatusResolver /
        // UnifiedReportStatusService) ⟹ ترك «الآن» يزيح الأرضيّة إلى الأمام ويُسقط فترات
        // كانت مستحقّة. الاستعادة هنا **حفظ لقيمة قائمة** لا قرار نشر جديد.
        if (originalPublishedAt is { } original)
        {
            var restored = await db.ReportTemplateVersions.FirstAsync(v => v.Id == versionId);
            restored.PublishedAtUtc = original;
            Console.WriteLine($"  استُعيد PublishedAtUtc الأصليّ لـ«{title}» v{versionNumber}: {original:O}");
        }

        await audit.LogAsync(actorId, "TemplateVersionReconciled", "ReportTemplateVersion", versionId,
            JsonSerializer.Serialize(new
            {
                ticket = "REPORT_TEMPLATE_PUBLICATION_GUARD_HOTFIX_R1",
                title,
                versionNumber,
                tool = "TemplatePublicationReconciler",
                reason = "restore_pre_seeder_publication_state",
                restoredPublishedAtUtc = originalPublishedAt?.ToString("O")
            }));
        Console.WriteLine($"نُشِر «{title}» v{versionNumber} عبر المسار الرسميّ.");
    }
    await db.SaveChangesAsync();
    await tx.CommitAsync();
}

// ============================ التحقّق البَعديّ ================================
db.ChangeTracker.Clear();
foreach (var node in results)
{
    var entry = node!.AsObject();
    var templateId = Guid.Parse(entry["templateId"]!.GetValue<string>());
    var versions = await db.ReportTemplateVersions.AsNoTracking()
        .Where(v => v.ReportTemplateId == templateId).ToListAsync();

    var expectedSet = entry["expectedPublishedVersionNumbers"]!.AsArray().Select(n => n!.GetValue<int>()).OrderBy(n => n).ToList();
    var publishedAfter = versions.Where(v => v.IsPublished).Select(v => v.VersionNumber).OrderBy(n => n).ToList();

    entry["after"] = FamilySnapshot(versions);
    entry["effectiveAfter"] = EffectiveOf(versions);
    entry["publishedSetAfter"] = new JsonArray(publishedAfter.Select(n => (JsonNode)n).ToArray());
    entry["submissionsByVersionAfter"] = await SubmissionsSnapshotAsync(db, templateId);
    entry["historicalSubmissionsUnchanged"] =
        entry["submissionsByVersionBefore"]!.ToJsonString() == entry["submissionsByVersionAfter"]!.ToJsonString();
    entry["publishedSetMatchesReference"] = publishedAfter.SequenceEqual(expectedSet);

    // أرضيّة استحقاق القالب = MIN(PublishedAtUtc) على الإصدارات المنشورة.
    var floorAfter = versions.Where(v => v.IsPublished && v.PublishedAtUtc.HasValue)
        .Select(v => v.PublishedAtUtc!.Value).DefaultIfEmpty().Min();
    entry["applicabilityFloorAfterUtc"] = floorAfter == default ? null : floorAfter.ToString("O");
    if (entry["applicabilityFloorExpectedUtc"]?.GetValue<string>() is { } expectedFloor)
        entry["applicabilityFloorPreserved"] =
            DateTime.TryParse(expectedFloor, null, System.Globalization.DateTimeStyles.RoundtripKind, out var ef)
            && Math.Abs((floorAfter - ef).TotalSeconds) < 1;
    entry["effectiveMatchesExpected"] =
        entry["effectiveAfter"]?.GetValue<int?>() == entry["effectiveVersionNumberExpected"]!.GetValue<int>();
}

var dryRun = mode == "DRY_RUN";
var allGood = results.All(r =>
    r!["historicalSubmissionsUnchanged"]?.GetValue<bool>() == true &&
    (dryRun || (r["publishedSetMatchesReference"]?.GetValue<bool>() == true &&
                r["effectiveMatchesExpected"]?.GetValue<bool>() == true &&
                r["applicabilityFloorPreserved"]?.GetValue<bool>() != false)));

var verdict = dryRun
    ? (results.All(r => r!["decision"]?.GetValue<string>() == "ALREADY_COMPLIANT") ? "DRY_RUN_NOTHING_TO_DO" : "DRY_RUN_OK")
    : allGood ? "APPLIED_AND_VERIFIED" : "APPLIED_BUT_VERIFICATION_FAILED";

await WriteReportAsync(outPath, mode, actualDb, actorId, results, 0, verdict);
Console.WriteLine($"== الحكم: {verdict} == التقرير: {outPath}");
return verdict == "APPLIED_BUT_VERIFICATION_FAILED" ? 7 : 0;

// ============================ دوالّ مساعدة ==================================
void Refuse(JsonObject entry, string code, string reason)
{
    entry["decision"] = "REFUSED";
    entry["refusalCode"] = code;
    entry["reason"] = reason;
    refusals++;
    Console.Error.WriteLine($"مرفوض [{code}] {entry["title"]}: {reason}");
}

static int? EffectiveOf(IEnumerable<Reporting.Domain.Entities.Templates.ReportTemplateVersion> versions)
    => versions.Where(v => v.IsPublished).OrderByDescending(v => v.VersionNumber)
               .Select(v => (int?)v.VersionNumber).FirstOrDefault();

static JsonArray FamilySnapshot(IEnumerable<Reporting.Domain.Entities.Templates.ReportTemplateVersion> versions)
{
    var arr = new JsonArray();
    foreach (var v in versions.OrderBy(v => v.VersionNumber))
        arr.Add(new JsonObject
        {
            ["versionId"] = v.Id.ToString(),
            ["versionNumber"] = v.VersionNumber,
            ["isPublished"] = v.IsPublished,
            ["publishedAtUtc"] = v.PublishedAtUtc?.ToString("O")
        });
    return arr;
}

static async Task<JsonObject> SubmissionsSnapshotAsync(AppDbContext db, Guid templateId)
{
    var rows = await db.ReportSubmissions.AsNoTracking()
        .Where(s => db.ReportTemplateVersions.Any(v => v.Id == s.ReportTemplateVersionId && v.ReportTemplateId == templateId))
        .GroupBy(s => new { s.ReportTemplateVersionId, s.Status })
        .Select(g => new { g.Key.ReportTemplateVersionId, g.Key.Status, Count = g.Count() })
        .ToListAsync();

    var obj = new JsonObject();
    foreach (var r in rows.OrderBy(r => r.ReportTemplateVersionId).ThenBy(r => r.Status))
        obj[$"{r.ReportTemplateVersionId}:{r.Status}"] = r.Count;
    return obj;
}

static async Task WriteReportAsync(string path, string mode, string database, Guid actorId,
    JsonArray results, int refusals, string verdict)
{
    var report = new JsonObject
    {
        ["ticket"] = "REPORT_TEMPLATE_PUBLICATION_GUARD_HOTFIX_R1",
        ["tool"] = "TemplatePublicationReconciler",
        ["mode"] = mode,
        ["database"] = database,
        ["actorId"] = actorId.ToString(),
        ["generatedAtUtc"] = DateTime.UtcNow.ToString("O"),
        ["refusals"] = refusals,
        ["verdict"] = verdict,
        ["templates"] = results
    };
    await File.WriteAllTextAsync(path,
        report.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
}

static string? ReadConnectionString(string envFile)
{
    string? value = null;
    foreach (var raw in File.ReadLines(envFile))
    {
        var line = raw.Trim();
        if (line.Length == 0 || line.StartsWith('#')) continue;
        var eq = line.IndexOf('=');
        if (eq <= 0) continue;
        if (line[..eq].Trim() != "ConnectionStrings__Default") continue;
        value = line[(eq + 1)..].Trim().Trim('"'); // آخر تعريف يفوز — كما تفعل systemd EnvironmentFile
    }
    return value;
}
