using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using R22BWorkItemsPublisher;
using Reporting.Infrastructure.Persistence;

// R22B — منفّذ خطّة تحويل حقول المشروع إلى بنود عمل.
// Dry-Run افتراضيّ: لا يكتب شيئًا إلّا مع --apply. لا هجرات، لا حذف، معاملة واحدة لكلّ قالب.
//
//   dotnet R22BWorkItemsPublisher.dll --env-file /etc/khubara-reporting-test.env \
//       --plan plan-test.json --out report-test.json [--apply]

string? envFile = null, planPath = null, outPath = null, connArg = null;
var apply = false;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--env-file": envFile = args[++i]; break;
        case "--conn": connArg = args[++i]; break;
        case "--plan": planPath = args[++i]; break;
        case "--out": outPath = args[++i]; break;
        case "--apply": apply = true; break;
        default: Console.Error.WriteLine($"وسيط غير معروف: {args[i]}"); return 2;
    }
}

if (planPath is null || outPath is null || (envFile is null && connArg is null))
{
    Console.Error.WriteLine("الاستعمال: --env-file <path> | --conn <cs>  --plan <plan.json>  --out <report.json>  [--apply]");
    return 2;
}

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

// حارس الهويّة: يمنع التنفيذ على قاعدة غير المقصودة.
var actualDb = (await db.Database.SqlQueryRaw<string>("SELECT current_database() AS \"Value\"").ToListAsync())[0];
if (expectedDb is not null && actualDb != expectedDb)
{
    Console.Error.WriteLine($"حارس الهويّة: الخطّة لـ«{expectedDb}» والاتّصال على «{actualDb}» — أُوقِف التنفيذ.");
    return 3;
}

var results = new JsonArray();
var failed = false;

foreach (var node in plan["templates"]!.AsArray())
{
    var t = node!.AsObject();
    var title = t["title"]!.GetValue<string>();
    var mode = t["mode"]?.GetValue<string>() ?? "all";

    GridConversion? grid = null;
    if (mode == "grid")
    {
        var specs = t["itemFields"]!.AsArray()
            .Select(f => new ItemFieldSpec(
                f!["key"]!.GetValue<string>(),
                f["type"]!.GetValue<string>(),
                f["label"]!.GetValue<string>()))
            .ToList();
        grid = new GridConversion(t["gridKey"]!.GetValue<string>(), specs);
    }

    var keys = (t["keys"]?.AsArray() ?? new JsonArray())
        .Select(k => k!.GetValue<string>()).ToList();

    var report = await Publisher.RunAsync(db, title, keys, apply, grid);

    Console.WriteLine($"[{report.Outcome}] {title}  v{report.SourceVersionNumber} -> v{report.TargetVersionNumber}  " +
                      $"موجودة={report.MovedKeys.Count} باقية={report.KeptProjectKeys.Count} مسودّات={report.Drafts.Count}");
    if (report.BlockReason is not null) Console.WriteLine($"    سبب التوقّف: {report.BlockReason}");
    foreach (var d in report.Drafts)
        Console.WriteLine($"    مسودّة {d.SubmissionId} v{d.FromVersionNumber} {d.PeriodKey} قيم={d.ValueCount}/{d.NonEmptyValueCount} " +
                          $"مشاريع={d.ProjectEntryCount} بنود={d.WorkItemCountAfter} md5 {d.PayloadMd5Before[..8]}->{d.PayloadMd5After[..8]} " +
                          $"مؤهَّلة={d.Eligible} منفَّذة={d.Processed} :: {d.Reason}");

    if (report.Outcome is Outcome.TemplateNotFound or Outcome.NoPublishedVersion
        or Outcome.NoProjectSection or Outcome.ContractMismatch or Outcome.Blocked)
        failed = true;

    results.Add(JsonNode.Parse(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = false })));
}

var envelope = new JsonObject
{
    ["database"] = actualDb,
    ["mode"] = apply ? "APPLY" : "DRY_RUN",
    ["runAtUtc"] = DateTime.UtcNow.ToString("O"),
    ["templates"] = results,
};
await File.WriteAllTextAsync(outPath, envelope.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"التقرير: {outPath}  (الوضع: {envelope["mode"]}، القاعدة: {actualDb})");

return failed ? 1 : 0;

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
