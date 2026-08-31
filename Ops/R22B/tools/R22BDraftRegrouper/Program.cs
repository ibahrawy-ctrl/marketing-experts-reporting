using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;

// R22B — تجميع مدخلات قسم المشاريع داخل مسودّة واحدة حسب projectId.
//
// القاعدة: كلّ مدخل قديم يمثّل «بند عمل» لا «بطاقة مشروع». الهدف بطاقة واحدة لكلّ مشروع
// تحوي بنود العمل الخاصّة به، بترتيب أوّل ظهور للمشروع وبترتيب المدخلات القديمة داخله.
//
// Dry-Run افتراضيّ: لا يكتب شيئًا إلّا مع --apply. مسودّات فقط. لا هجرة، لا حذف، لا SQL بيانات خام.
//
//   dotnet R22BDraftRegrouper.dll --conn "<cs>" --expect-db reporting_prod \
//       --submission <guid> --out report.json [--expect-payload-md5 <md5>] [--apply]

const long AdvisoryLockKey = 0x5232_3242_0002;

string? envFile = null, conn = null, expectDb = null, outPath = null, expectMd5 = null;
Guid submissionId = Guid.Empty;
var apply = false;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--env-file": envFile = args[++i]; break;
        case "--conn": conn = args[++i]; break;
        case "--expect-db": expectDb = args[++i]; break;
        case "--submission": submissionId = Guid.Parse(args[++i]); break;
        case "--out": outPath = args[++i]; break;
        case "--expect-payload-md5": expectMd5 = args[++i]; break;
        case "--apply": apply = true; break;
        default: Console.Error.WriteLine($"وسيط غير معروف: {args[i]}"); return 2;
    }
}

if (outPath is null || submissionId == Guid.Empty || (envFile is null && conn is null) || expectDb is null)
{
    Console.Error.WriteLine(
        "الاستعمال: --conn <cs> | --env-file <path>  --expect-db <db>  --submission <guid>  --out <report.json>  [--expect-payload-md5 <md5>] [--apply]");
    return 2;
}

conn ??= ReadConnectionString(envFile!);
if (string.IsNullOrWhiteSpace(conn)) { Console.Error.WriteLine("تعذّر قراءة ConnectionStrings__Default."); return 2; }

var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(conn).Options;
await using var db = new AppDbContext(options);

var actualDb = (await db.Database.SqlQueryRaw<string>("SELECT current_database() AS \"Value\"").ToListAsync())[0];
if (actualDb != expectDb)
{
    Console.Error.WriteLine($"حارس الهويّة: المتوقَّع «{expectDb}» والاتّصال على «{actualDb}» — أُوقِف التنفيذ.");
    return 3;
}

var report = new JsonObject
{
    ["database"] = actualDb,
    ["submissionId"] = submissionId.ToString(),
    ["mode"] = apply ? "APPLY" : "DRY_RUN",
    ["runAtUtc"] = DateTime.UtcNow.ToString("O"),
};

await using var tx = await db.Database.BeginTransactionAsync();
await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", new object[] { AdvisoryLockKey });

var sub = await db.ReportSubmissions.FirstOrDefaultAsync(s => s.Id == submissionId);
if (sub is null) return await FailAsync("التسليم غير موجود.");

// ===== حراسة الحالة: مسودّة فقط، غير مُرسَلة وغير مغلقة =====
if (sub.Status != SubmissionStatus.Draft) return await FailAsync($"الحالة «{sub.Status}» ليست Draft — لا تُمسّ.");
if (sub.SubmittedAtUtc is not null) return await FailAsync("سبق إرسالها (SubmittedAtUtc) — لا تُمسّ.");
if (sub.ClosedAtUtc is not null) return await FailAsync("مُغلقة (ClosedAtUtc) — لا تُمسّ.");

report["periodKey"] = sub.PeriodKey;
report["submitterId"] = sub.SubmitterId.ToString();
report["status"] = sub.Status.ToString();
report["templateVersionId"] = sub.ReportTemplateVersionId.ToString();
report["updatedAtUtcBefore"] = sub.UpdatedAtUtc?.ToString("O");

var prsFields = await db.TemplateFields
    .Where(f => f.ReportTemplateVersionId == sub.ReportTemplateVersionId
                && f.FieldType == FieldType.ProjectRepeatableSection)
    .Select(f => new { f.Id, f.Label, f.ConfigJson })
    .ToListAsync();

var allValues = await db.SubmissionFieldValues.Where(v => v.ReportSubmissionId == submissionId).ToListAsync();
var prsIds = prsFields.Select(f => f.Id).ToHashSet();

// بصمة الحمولة قبل الكتابة — حارس التزامن (لا نكتب فوق تعديل المستخدم).
var payloadMd5Before = Md5(string.Join("\u001e",
    allValues.Where(v => prsIds.Contains(v.TemplateFieldId))
             .OrderBy(v => v.TemplateFieldId)
             .Select(v => v.ValueJson ?? "")));
report["prsPayloadMd5Before"] = payloadMd5Before;

if (expectMd5 is not null && !string.Equals(expectMd5, payloadMd5Before, StringComparison.OrdinalIgnoreCase))
    return await FailAsync($"حارس التزامن: البصمة المتوقَّعة {expectMd5} والمقيسة {payloadMd5Before} — تغيّرت المسودّة، أُوقِف التنفيذ.");

// بصمة الحقول غير-PRS (ملخّصات التقرير العامّة) — يجب ألّا تتغيّر إطلاقًا.
string GeneralMd5() => Md5(string.Join("\u001e",
    allValues.Where(v => !prsIds.Contains(v.TemplateFieldId))
             .OrderBy(v => v.TemplateFieldId)
             .Select(v => string.Join("~", v.ValueText ?? "", v.ValueNumber?.ToString() ?? "",
                                           v.ValueDate?.ToString("O") ?? "", v.ValueBool?.ToString() ?? "", v.ValueJson ?? ""))));
var generalBefore = GeneralMd5();
report["generalFieldsMd5Before"] = generalBefore;
report["generalFieldCount"] = allValues.Count - allValues.Count(v => prsIds.Contains(v.TemplateFieldId));

var sections = new JsonArray();
var totalsOk = true;

foreach (var f in prsFields)
{
    var v = allValues.FirstOrDefault(x => x.TemplateFieldId == f.Id);
    if (v is null || string.IsNullOrWhiteSpace(v.ValueJson)) continue;
    if (JsonNode.Parse(v.ValueJson) is not JsonArray oldEntries) continue;

    var sec = new JsonObject { ["fieldId"] = f.Id.ToString(), ["label"] = f.Label };

    // مفاتيح حقول بند العمل المصرَّح بها في القالب — أيّ مفتاح خارجها = مفتاح مجهول.
    var declared = new HashSet<string>(StringComparer.Ordinal);
    foreach (var d in JsonNode.Parse(f.ConfigJson ?? "{}")?["workItems"]?["fields"] as JsonArray ?? new JsonArray())
        if (d?["key"]?.GetValue<string>() is { } k) declared.Add(k);
    var declaredProjectKeys = new HashSet<string>(StringComparer.Ordinal);
    foreach (var d in JsonNode.Parse(f.ConfigJson ?? "{}")?["fields"] as JsonArray ?? new JsonArray())
        if (d?["key"]?.GetValue<string>() is { } k) declaredProjectKeys.Add(k);

    // ===== قراءة الحالة القديمة =====
    // كلّ مدخل قديم = بند عمل واحد. قيمه إمّا في answers (v1) أو في workItems[0].answers (بعد ترحيل §4.3).
    var olds = new List<OldEntry>();
    foreach (var node in oldEntries)
    {
        if (node is not JsonObject e) return await FailAsync("مدخل ليس كائنًا — بنية غير متوقَّعة.");
        var pid = e["projectId"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(pid)) return await FailAsync("مدخل بلا projectId — لا يمكن التجميع بأمان.");

        var projectAnswers = e["answers"] as JsonObject ?? new JsonObject();
        var wis = e["workItems"] as JsonArray;

        // قيم البند: من workItems إن وُجدت، وإلّا من answers مباشرةً (v1).
        var itemAnswerSets = new List<JsonObject>();
        var carriedProjectAnswers = new JsonObject();

        if (wis is not null && wis.Count > 0)
        {
            foreach (var w in wis)
                itemAnswerSets.Add(Clone(w?["answers"] as JsonObject ?? new JsonObject()));
            foreach (var kv in projectAnswers) carriedProjectAnswers[kv.Key] = Clone1(kv.Value);
        }
        else
        {
            var itemPart = new JsonObject();
            foreach (var kv in projectAnswers)
            {
                if (declared.Contains(kv.Key)) itemPart[kv.Key] = Clone1(kv.Value);
                else carriedProjectAnswers[kv.Key] = Clone1(kv.Value);
            }
            if (itemPart.Count > 0) itemAnswerSets.Add(itemPart);
        }

        // خصائص أخرى على مستوى المدخل (غير projectId/answers/workItems) تُنقَل كما هي.
        var extras = new JsonObject();
        foreach (var kv in e)
            if (kv.Key is not ("projectId" or "answers" or "workItems"))
                extras[kv.Key] = Clone1(kv.Value);

        olds.Add(new OldEntry(pid!, itemAnswerSets, carriedProjectAnswers, extras));
    }

    // ===== القياس قبل =====
    var uniqueOrder = new List<string>();
    foreach (var o in olds) if (!uniqueOrder.Contains(o.ProjectId)) uniqueOrder.Add(o.ProjectId);

    // رباعيّات مرتّبة قبل: (projectId, rankداخل المشروع, fieldKey, value)
    var before = new List<string>();
    var perProjectRank = new Dictionary<string, int>(StringComparer.Ordinal);
    var unknownKeys = new SortedSet<string>(StringComparer.Ordinal);
    foreach (var o in olds)
    {
        perProjectRank.TryGetValue(o.ProjectId, out var r);
        foreach (var set in o.ItemAnswerSets)
        {
            foreach (var kv in set.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                before.Add($"{o.ProjectId}|{r}|{kv.Key}|{Canon(kv.Value)}");
                if (!declared.Contains(kv.Key)) unknownKeys.Add(kv.Key);
            }
            r++;
        }
        // مفاتيح مستوى المشروع تبقى مستوى مشروع — تُقاس منفصلة ولا تدخل بنود العمل.
        perProjectRank[o.ProjectId] = r;
    }

    var projectLevelBefore = new List<string>();
    foreach (var o in olds)
        foreach (var kv in o.ProjectAnswers.OrderBy(x => x.Key, StringComparer.Ordinal))
            projectLevelBefore.Add($"{o.ProjectId}|{kv.Key}|{Canon(kv.Value)}");

    // ===== التجميع =====
    var newArr = new JsonArray();
    var perProjectItems = new JsonArray();
    foreach (var pid in uniqueOrder)
    {
        var group = olds.Where(o => o.ProjectId == pid).ToList();

        // دمج إجابات مستوى المشروع: أوّل قيمة غير فارغة تفوز، والتعارض يوقف التنفيذ.
        var mergedProject = new JsonObject();
        foreach (var g in group)
            foreach (var kv in g.ProjectAnswers)
            {
                var s = Canon(kv.Value);
                if (s.Length == 0) continue;
                if (mergedProject.TryGetPropertyValue(kv.Key, out var prev) && Canon(prev) != s)
                    return await FailAsync($"تعارض قيمة على مستوى المشروع {pid} في المفتاح «{kv.Key}» — أُوقِف التجميع.");
                mergedProject[kv.Key] = Clone1(kv.Value);
            }

        var mergedExtras = new JsonObject();
        foreach (var g in group)
            foreach (var kv in g.Extras)
            {
                var s = Canon(kv.Value);
                if (mergedExtras.TryGetPropertyValue(kv.Key, out var prev) && Canon(prev) != s)
                    return await FailAsync($"تعارض خاصّيّة «{kv.Key}» على مستوى المدخل للمشروع {pid} — أُوقِف التجميع.");
                mergedExtras[kv.Key] = Clone1(kv.Value);
            }

        var items = new JsonArray();
        foreach (var g in group)
            foreach (var set in g.ItemAnswerSets)
                items.Add(new JsonObject { ["answers"] = Clone(set) });

        var card = new JsonObject { ["projectId"] = pid };
        foreach (var kv in mergedExtras) card[kv.Key] = Clone1(kv.Value);
        card["answers"] = mergedProject;
        card["workItems"] = items;
        newArr.Add(card);
        perProjectItems.Add(items.Count);
    }

    // ===== القياس بعد =====
    var after = new List<string>();
    foreach (var card in newArr)
    {
        var pid = card!["projectId"]!.GetValue<string>();
        var items = card["workItems"]!.AsArray();
        for (var i = 0; i < items.Count; i++)
            foreach (var kv in (items[i]!["answers"]!.AsObject()).OrderBy(x => x.Key, StringComparer.Ordinal))
                after.Add($"{pid}|{i}|{kv.Key}|{Canon(kv.Value)}");
    }

    var projectLevelAfter = new List<string>();
    foreach (var card in newArr)
    {
        var pid = card!["projectId"]!.GetValue<string>();
        foreach (var kv in card["answers"]!.AsObject().OrderBy(x => x.Key, StringComparer.Ordinal))
            projectLevelAfter.Add($"{pid}|{kv.Key}|{Canon(kv.Value)}");
    }

    var bs = before.OrderBy(x => x, StringComparer.Ordinal).ToList();
    var asx = after.OrderBy(x => x, StringComparer.Ordinal).ToList();
    var missing = Diff(bs, asx);
    var orphan = Diff(asx, bs);

    // «نقل ملخّص عامّ إلى بند عمل» = ظهور مفتاح مصرَّح على مستوى المشروع داخل بنود العمل.
    var summariesMoved = after.Count(a => declaredProjectKeys.Contains(a.Split('|')[2]));

    var uniqueAfter = newArr.Select(c => c!["projectId"]!.GetValue<string>()).ToList();

    sec["oldProjectEntries"] = olds.Count;
    sec["uniqueProjectIds"] = uniqueOrder.Count;
    sec["projectEntryCountAfter"] = newArr.Count;
    sec["uniqueProjectIdCountAfter"] = uniqueAfter.Distinct().Count();
    sec["duplicateProjectIds"] = uniqueAfter.Count - uniqueAfter.Distinct().Count();
    sec["totalWorkItemCount"] = after.Count == 0 ? 0 : newArr.Sum(c => c!["workItems"]!.AsArray().Count);
    sec["workItemsPerProject"] = perProjectItems.DeepClone();
    sec["projectIdOrder"] = new JsonArray(uniqueOrder.Select(x => (JsonNode)JsonValue.Create(x)!).ToArray());
    sec["semanticValuePairsBefore"] = before.Count;
    sec["semanticValuePairsAfter"] = after.Count;
    sec["missingValues"] = missing.Count;
    sec["orphanValues"] = orphan.Count;
    sec["addedValues"] = orphan.Count;
    sec["unknownKeys"] = unknownKeys.Count;
    sec["unknownKeyList"] = new JsonArray(unknownKeys.Select(x => (JsonNode)JsonValue.Create(x)!).ToArray());
    sec["generalSummariesMoved"] = summariesMoved;
    sec["projectLevelPairsBefore"] = projectLevelBefore.Count;
    sec["projectLevelPairsAfter"] = projectLevelAfter.Count;
    sec["semanticMd5Before"] = Md5(string.Join("|", bs));
    sec["semanticMd5After"] = Md5(string.Join("|", asx));
    sec["payloadMd5Before"] = Md5(v.ValueJson!);
    if (missing.Count > 0) sec["missingSample"] = new JsonArray(missing.Take(5).Select(x => (JsonNode)JsonValue.Create(x)!).ToArray());
    if (orphan.Count > 0) sec["orphanSample"] = new JsonArray(orphan.Take(5).Select(x => (JsonNode)JsonValue.Create(x)!).ToArray());

    var ok = missing.Count == 0 && orphan.Count == 0 && unknownKeys.Count == 0 && summariesMoved == 0
             && sec["duplicateProjectIds"]!.GetValue<int>() == 0
             && projectLevelBefore.Count >= projectLevelAfter.Count;
    sec["safe"] = ok;
    if (!ok) totalsOk = false;

    var newJson = newArr.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    sec["payloadMd5After"] = Md5(newJson);

    if (apply && ok)
    {
        v.ValueJson = newJson;
        v.UpdatedAtUtc = DateTime.UtcNow;
        sec["written"] = true;
    }
    else sec["written"] = false;

    sections.Add(sec);
}

report["sections"] = sections;
report["safe"] = totalsOk;

if (GeneralMd5() != generalBefore) return await FailAsync("تغيّرت الحقول العامّة — أُوقِف التنفيذ وتراجعت المعاملة.");
report["generalFieldsMd5After"] = GeneralMd5();

if (apply && totalsOk)
{
    sub.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();
    await tx.CommitAsync();
    report["applied"] = true;
}
else
{
    await tx.RollbackAsync();
    report["applied"] = false;
    if (apply && !totalsOk) report["abort"] = "فشل فحص السلامة الدلاليّة — لم يُكتب شيء.";
}

report["updatedAtUtcAfter"] = sub.UpdatedAtUtc?.ToString("O");
await File.WriteAllTextAsync(outPath, report.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(report.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
return totalsOk ? 0 : 1;

async Task<int> FailAsync(string reason)
{
    await tx.RollbackAsync();
    report["applied"] = false;
    report["blockReason"] = reason;
    await File.WriteAllTextAsync(outPath!, report.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    Console.Error.WriteLine($"أُوقِف التنفيذ: {reason}");
    return 4;
}

static List<string> Diff(List<string> a, List<string> b)
{
    var pool = b.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
    var res = new List<string>();
    foreach (var x in a)
    {
        if (pool.TryGetValue(x, out var n) && n > 0) pool[x] = n - 1;
        else res.Add(x);
    }
    return res;
}

static JsonObject Clone(JsonObject o) => (JsonObject)JsonNode.Parse(o.ToJsonString())!;
static JsonNode? Clone1(JsonNode? n) => n is null ? null : JsonNode.Parse(n.ToJsonString());
static string Canon(JsonNode? n) => n is null ? "" : n.ToJsonString();
static string Md5(string s) => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();

static string? ReadConnectionString(string envFile)
{
    foreach (var raw in File.ReadAllLines(envFile))
    {
        var line = raw.Trim();
        if (!line.StartsWith("ConnectionStrings__Default=", StringComparison.OrdinalIgnoreCase)) continue;
        var value = line["ConnectionStrings__Default=".Length..].Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') value = value[1..^1];
        return value;
    }
    return null;
}

internal sealed record OldEntry(string ProjectId, List<JsonObject> ItemAnswerSets, JsonObject ProjectAnswers, JsonObject Extras);
