using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModerationV6Publisher;
using Reporting.Domain.Entities.Submissions;
using Reporting.Domain.Entities.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// MODERATION-CONTENT-PERFORMANCE-R1B — اختبارات نواة أداة نشر V6 (Publisher) ضدّ AppDbContext الحقيقي.
/// تُثبِت: Dry-Run لا يكتب شيئًا؛ غياب V5؛ الخطة عند غياب V6؛ idempotency؛ ContractMismatch؛
/// بوّابات إعادة توجيه W30 (فارغة⇒مؤهَّلة، ذات قيمة/مُرسَلة/مرفق⇒محظورة)؛ حماية القيد الفريد والتراجع؛
/// ثبات V5 بعد التطبيق؛ عدد الحقول 21؛ المفاتيح المضافة الستة حصرًا؛ ثبات عدّ الهجرات (لا Migration).
/// كل اختبار يبذر قالبًا بمعرّف فريد ومُسلِّمًا فريدًا كي لا يتلوّث بقاعدة الاختبار المشتركة الدائمة.
/// </summary>
[Collection("Integration")]
public class ModerationV6PublisherTests
{
    private readonly CustomWebApplicationFactory _factory;
    public ModerationV6PublisherTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static readonly string[] V5Keys =
    {
        "project_status", "time_consumption",
        "incoming_messages", "answered_messages", "avg_response_minutes",
        "problematic_comments", "escalations", "complaints", "converted_opportunities",
        "cases_grid",
        "done", "issues", "recurring_questions", "next_week", "recommendations",
    };

    private static readonly string[] NewKeys =
    {
        "content_highlights", "audience_insight", "lessons_learned",
        "decisions_required", "risk_exists", "risk_note",
    };

    private static string TypeFor(string key) => key switch
    {
        "project_status" => "Select",
        "time_consumption" => "Percentage",
        "cases_grid" => "Grid",
        "done" or "issues" or "recurring_questions" or "next_week" or "recommendations" => "LongText",
        _ => "Number",
    };

    // إعداد ProjectRepeatableSection لإصدار V5 (الـ15 مفتاحًا). partialV6=true ⇒ يضيف content_highlights فقط (عقد ناقص).
    private static string PrsConfig(bool partialV6 = false)
    {
        var fields = new List<object>();
        foreach (var k in V5Keys)
        {
            if (k == "cases_grid")
                fields.Add(new { key = k, label = k, type = "Grid", required = false, columns = new[] { "نوع الحالة", "الوصف", "القناة", "الحالة", "هل تم التصعيد؟", "الإجراء التالي" } });
            else if (TypeFor(k) == "Select")
                fields.Add(new { key = k, label = k, type = "Select", required = false, options = new[] { "🟢 ممتاز", "🟡 مستقر", "🔴 يحتاج تدخل" } });
            else
                fields.Add(new { key = k, label = k, type = TypeFor(k), required = false });
        }
        if (partialV6)
            fields.Add(new { key = "content_highlights", label = "تحليل المحتوى", type = "Grid", required = false, columns = new[] { "التصنيف" } });
        return JsonSerializer.Serialize(new { projectRequired = true, minProjects = 1, maxProjects = 10, fields });
    }

    // يبذر قالب مديرشن بإصدار V5 منشور واحد. يعيد معرّف القالب + معرّف حقل الـPRS.
    private static (Guid templateId, Guid v5VersionId, Guid prsFieldId) SeedModerationTemplate(
        AppDbContext db, bool publishV5 = true, string? extraPartialV6 = null, bool addFileUploadField = false)
    {
        var tpl = new ReportTemplate
        {
            Title = "قالب مديرشن اختبار " + Guid.NewGuid().ToString("N")[..8],
            DefaultPeriodType = PeriodType.Weekly,
            Status = TemplateStatus.Published,
            OwnerId = Guid.NewGuid(),
            IsActive = true,
        };
        var prsField = new TemplateField
        {
            Label = "قسم المشاريع",
            Key = "projects",
            FieldType = FieldType.ProjectRepeatableSection,
            Order = 1,
            IsRequired = false,
            ConfigJson = PrsConfig(),
        };
        var v5 = new ReportTemplateVersion
        {
            ReportTemplateId = tpl.Id,
            VersionNumber = 1,
            IsPublished = publishV5,
            PublishedAtUtc = publishV5 ? DateTime.UtcNow : null,
        };
        v5.Fields.Add(prsField);
        if (addFileUploadField)
            v5.Fields.Add(new TemplateField { Label = "مرفق", Key = "attachment", FieldType = FieldType.FileUpload, Order = 2, IsRequired = false });
        tpl.Versions.Add(v5);

        if (extraPartialV6 is not null)
        {
            var v2 = new ReportTemplateVersion { ReportTemplateId = tpl.Id, VersionNumber = 2, IsPublished = true, PublishedAtUtc = DateTime.UtcNow };
            v2.Fields.Add(new TemplateField { Label = "قسم المشاريع", Key = "projects", FieldType = FieldType.ProjectRepeatableSection, Order = 1, IsRequired = false, ConfigJson = PrsConfig(partialV6: true) });
            tpl.Versions.Add(v2);
        }

        db.ReportTemplates.Add(tpl);
        db.SaveChanges();
        return (tpl.Id, v5.Id, prsField.Id);
    }

    private static ReportSubmission SeedDraft(AppDbContext db, Guid versionId, string periodKey = "2026-W30", DateTime? submittedAt = null)
    {
        var sub = new ReportSubmission
        {
            ReportTemplateVersionId = versionId,
            SubmitterId = Guid.NewGuid(),
            PeriodType = PeriodType.Weekly,
            PeriodKey = periodKey,
            Status = SubmissionStatus.Draft,
            SubmittedAtUtc = submittedAt,
        };
        db.ReportSubmissions.Add(sub);
        db.SaveChanges();
        return sub;
    }

    private AppDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<AppDbContext>();

    private static int SubKeyCount(string? configJson)
    {
        var node = JsonNode.Parse(configJson ?? "{}")?.AsObject();
        return (node?["fields"] as JsonArray)?.Count ?? 0;
    }

    // ---- 1: Dry-Run لا يكتب شيئًا (لا إصدار V6 يُنشأ) ----
    [Fact]
    public async Task S01_DryRun_WritesNothing()
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var (templateId, _, _) = SeedModerationTemplate(db);

        var report = await Publisher.RunAsync(db, templateId, apply: false);

        Assert.Equal(PublishOutcome.Planned, report.Outcome);
        Assert.False(report.Applied);

        using var scope2 = _factory.Services.CreateScope();
        var versions = await Db(scope2).ReportTemplateVersions.CountAsync(v => v.ReportTemplateId == templateId);
        Assert.Equal(1, versions); // لا V6 مكتوب.
    }

    // ---- 2: غياب V5 (لا إصدار منشور) ⇒ V5NotFound ----
    [Fact]
    public async Task S02_NoPublishedV5_ReturnsV5NotFound()
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var (templateId, _, _) = SeedModerationTemplate(db, publishV5: false);

        var report = await Publisher.RunAsync(db, templateId, apply: false);

        Assert.Equal(PublishOutcome.V5NotFound, report.Outcome);
        Assert.NotNull(report.BlockReason);
    }

    // ---- 3: لا V6 بعد ⇒ خطة (Planned) بعدد الحقول المضافة الستة ----
    [Fact]
    public async Task S03_NoV6Yet_PlansV6()
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var (templateId, _, _) = SeedModerationTemplate(db);

        var report = await Publisher.RunAsync(db, templateId, apply: false);

        Assert.Equal(PublishOutcome.Planned, report.Outcome);
        Assert.Equal(6, report.AddedKeys.Count);
        Assert.Equal(2, report.V6VersionNumber);
    }

    // ---- 4: V6 مطابق موجود ⇒ AlreadyApplied (idempotent) ----
    [Fact]
    public async Task S04_MatchingV6_ReturnsAlreadyApplied()
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var (templateId, _, _) = SeedModerationTemplate(db);

        var applied = await Publisher.RunAsync(db, templateId, apply: true);
        Assert.Equal(PublishOutcome.Applied, applied.Outcome);

        using var scope2 = _factory.Services.CreateScope();
        var again = await Publisher.RunAsync(Db(scope2), templateId, apply: false);
        Assert.Equal(PublishOutcome.AlreadyApplied, again.Outcome);
    }

    // ---- 5: إصدار يحمل content_highlights بعقد ناقص ⇒ ContractMismatch (توقّف) ----
    [Fact]
    public async Task S05_DifferentV6_ReturnsContractMismatch()
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var (templateId, _, _) = SeedModerationTemplate(db, extraPartialV6: "yes");

        var report = await Publisher.RunAsync(db, templateId, apply: false);

        Assert.Equal(PublishOutcome.ContractMismatch, report.Outcome);
        Assert.NotNull(report.BlockReason);
    }

    // ---- 6: مسودّة W30 فارغة ⇒ مؤهَّلة (بلا تعارض فريد) ----
    [Fact]
    public async Task S06_EmptyW30Draft_IsEligible()
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var (templateId, v5Id, _) = SeedModerationTemplate(db);
        var draft = SeedDraft(db, v5Id);

        var report = await Publisher.RunAsync(db, templateId, apply: false);

        var d = Assert.Single(report.Repoints);
        Assert.Equal(draft.Id, d.SubmissionId);
        Assert.True(d.Eligible);
        Assert.Equal(0, d.NonEmptyValueCount);
        Assert.False(d.UniqueConflict); // بوّابة القيد الفريد تُقيَّم وتكون سالبة للمسودّة النظيفة.
    }

    // ---- 7: مسودّة W30 تحوي قيمة غير فارغة ⇒ محظورة ----
    [Fact]
    public async Task S07_W30WithValue_IsBlocked()
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var (templateId, v5Id, prsFieldId) = SeedModerationTemplate(db);
        var draft = SeedDraft(db, v5Id);
        db.SubmissionFieldValues.Add(new SubmissionFieldValue { ReportSubmissionId = draft.Id, TemplateFieldId = prsFieldId, ValueText = "قيمة فعلية" });
        db.SaveChanges();

        var report = await Publisher.RunAsync(db, templateId, apply: false);

        var d = Assert.Single(report.Repoints);
        Assert.False(d.Eligible);
        Assert.True(d.NonEmptyValueCount > 0);
    }

    // ---- 8: مسودّة سبق إرسالها (SubmittedAtUtc) ⇒ محظورة ----
    [Fact]
    public async Task S08_W30PreviouslySubmitted_IsBlocked()
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var (templateId, v5Id, _) = SeedModerationTemplate(db);
        SeedDraft(db, v5Id, submittedAt: DateTime.UtcNow);

        var report = await Publisher.RunAsync(db, templateId, apply: false);

        var d = Assert.Single(report.Repoints);
        Assert.False(d.Eligible);
    }

    // ---- 9: مسودّة W30 تحوي مرفقًا ⇒ محظورة ----
    [Fact]
    public async Task S09_W30WithAttachment_IsBlocked()
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var (templateId, v5Id, _) = SeedModerationTemplate(db, addFileUploadField: true);
        var draft = SeedDraft(db, v5Id);
        var uploadField = await db.TemplateFields.FirstAsync(f => f.ReportTemplateVersionId == v5Id && f.FieldType == FieldType.FileUpload);
        db.SubmissionFieldValues.Add(new SubmissionFieldValue { ReportSubmissionId = draft.Id, TemplateFieldId = uploadField.Id, ValueJson = "[{\"fileName\":\"a.pdf\"}]" });
        db.SaveChanges();

        var report = await Publisher.RunAsync(db, templateId, apply: false);

        var d = Assert.Single(report.Repoints);
        Assert.False(d.Eligible);
        Assert.True(d.AttachmentCount > 0);
    }

    // ---- 10: بوّابة التعارض الفريد تُقيَّم صراحةً (سالبة لمسودّة نظيفة وحيدة) ----
    [Fact]
    public async Task S10_UniqueConflictGate_IsEvaluated()
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var (templateId, v5Id, _) = SeedModerationTemplate(db);
        SeedDraft(db, v5Id, periodKey: "2026-W31");

        var report = await Publisher.RunAsync(db, templateId, apply: false);

        var d = Assert.Single(report.Repoints);
        Assert.False(d.UniqueConflict);
        Assert.True(d.Eligible);
    }

    // ---- 11: تعارض قيد فريد وقت التطبيق ⇒ تراجع كامل (لا V6، لا إعادة توجيه) ----
    [Fact]
    public async Task S11_UniqueViolationOnApply_RollsBackEverything()
    {
        Guid templateId, submitter;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = Db(scope);
            var (tid, v5Id, _) = SeedModerationTemplate(db);
            templateId = tid;
            // إصدار منشور أقدم إضافي (v3) لوضع مسودّتين لنفس (المُسلِّم، الفترة) على إصدارين مختلفين —
            // كلاهما فارغ ومؤهَّل ⇒ عند التطبيق يُعاد توجيههما لنفس V6 ⇒ انتهاك القيد الفريد ⇒ استثناء.
            var v3 = new ReportTemplateVersion { ReportTemplateId = templateId, VersionNumber = 3, IsPublished = false };
            v3.Fields.Add(new TemplateField { Label = "قسم المشاريع", Key = "projects", FieldType = FieldType.ProjectRepeatableSection, Order = 1, ConfigJson = PrsConfig() });
            db.ReportTemplateVersions.Add(v3);
            db.SaveChanges();

            submitter = Guid.NewGuid();
            db.ReportSubmissions.Add(new ReportSubmission { ReportTemplateVersionId = v5Id, SubmitterId = submitter, PeriodType = PeriodType.Weekly, PeriodKey = "2026-W30", Status = SubmissionStatus.Draft });
            db.ReportSubmissions.Add(new ReportSubmission { ReportTemplateVersionId = v3.Id, SubmitterId = submitter, PeriodType = PeriodType.Weekly, PeriodKey = "2026-W30", Status = SubmissionStatus.Draft });
            db.SaveChanges();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            await Assert.ThrowsAnyAsync<DbUpdateException>(() => Publisher.RunAsync(Db(scope), templateId, apply: true));
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = Db(scope);
            // لا V6 نُشِر (لا إصدار منشور برقم 4/بعقد V6) — التراجع أزال كل شيء.
            var publishedWithHighlights = await db.ReportTemplateVersions
                .Where(v => v.ReportTemplateId == templateId && v.IsPublished)
                .Include(v => v.Fields)
                .ToListAsync();
            Assert.All(publishedWithHighlights, v =>
                Assert.DoesNotContain(v.Fields, f => f.FieldType == FieldType.ProjectRepeatableSection && (f.ConfigJson ?? "").Contains("content_highlights")));
        }
    }

    // ---- 12: بعد التطبيق يبقى إصدار V5 دون مساس (ConfigJson/النشر/الرقم) ----
    [Fact]
    public async Task S12_V5_UnchangedAfterApply()
    {
        Guid templateId, v5Id, prsFieldId;
        using (var scope = _factory.Services.CreateScope())
        {
            var (tid, vid, prsId) = SeedModerationTemplate(Db(scope));
            templateId = tid; v5Id = vid; prsFieldId = prsId;
        }

        // نقرأ اللقطة عبر نطاق جديد (round-trip حقيقي) كي تكون بتنسيق jsonb المطبَّع
        // مثل قراءة ما بعد التطبيق تمامًا — فتُثبِت المقارنة عدم تغيّر التخزين الفعلي لا اختلاف التنسيق.
        string beforeConfig;
        using (var scope = _factory.Services.CreateScope())
        {
            beforeConfig = (await Db(scope).TemplateFields.AsNoTracking().FirstAsync(f => f.Id == prsFieldId)).ConfigJson!;
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var report = await Publisher.RunAsync(Db(scope), templateId, apply: true);
            Assert.Equal(PublishOutcome.Applied, report.Outcome);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = Db(scope);
            var v5 = await db.ReportTemplateVersions.Include(v => v.Fields).FirstAsync(v => v.Id == v5Id);
            Assert.True(v5.IsPublished);
            Assert.Equal(1, v5.VersionNumber);
            var prs = v5.Fields.First(f => f.FieldType == FieldType.ProjectRepeatableSection);
            Assert.Equal(beforeConfig, prs.ConfigJson); // byte-identical.
            Assert.Equal(15, SubKeyCount(prs.ConfigJson)); // الـ15 دون زيادة.
        }
    }

    // ---- 13: عدد الحقول الفرعية لإصدار V6 = 21 (15 + 6) ----
    [Fact]
    public async Task S13_V6_HasTwentyOneSubFields()
    {
        Guid templateId;
        using (var scope = _factory.Services.CreateScope())
            (templateId, _, _) = SeedModerationTemplate(Db(scope));

        using (var scope = _factory.Services.CreateScope())
            await Publisher.RunAsync(Db(scope), templateId, apply: true);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = Db(scope);
            var v6 = await db.ReportTemplateVersions.Include(v => v.Fields)
                .Where(v => v.ReportTemplateId == templateId && v.VersionNumber == 2).FirstAsync();
            Assert.True(v6.IsPublished);
            var prs = v6.Fields.First(f => f.FieldType == FieldType.ProjectRepeatableSection);
            Assert.Equal(21, SubKeyCount(prs.ConfigJson));
        }
    }

    // ---- 14: المفاتيح المضافة هي الستة حصرًا، والـ15 القائمة محفوظة ----
    [Fact]
    public async Task S14_AddedKeys_AreExactlyTheSix()
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var (templateId, _, _) = SeedModerationTemplate(db);

        var report = await Publisher.RunAsync(db, templateId, apply: false);

        Assert.Equal(NewKeys.OrderBy(x => x), report.AddedKeys.OrderBy(x => x));

        // العقد الناتج يحوي كل الـ15 + الستة، بلا حذف/إعادة تسمية.
        using var scope2 = _factory.Services.CreateScope();
        var applied = await Publisher.RunAsync(Db(scope2), templateId, apply: true);
        Assert.Equal(PublishOutcome.Applied, applied.Outcome);
        using var scope3 = _factory.Services.CreateScope();
        var v6 = await Db(scope3).ReportTemplateVersions.Include(v => v.Fields)
            .Where(v => v.ReportTemplateId == templateId && v.VersionNumber == 2).FirstAsync();
        var prs = v6.Fields.First(f => f.FieldType == FieldType.ProjectRepeatableSection);
        var node = JsonNode.Parse(prs.ConfigJson!)!.AsObject();
        var keys = (node["fields"] as JsonArray)!.Select(f => f!["key"]!.GetValue<string>()).ToHashSet();
        foreach (var k in V5Keys) Assert.Contains(k, keys);
        foreach (var k in NewKeys) Assert.Contains(k, keys);
    }

    // ---- 15: عدّ الهجرات ثابت قبل/بعد التطبيق (الأداة لا تشغّل أي Migration) ----
    [Fact]
    public async Task S15_MigrationCount_UnchangedAcrossApply()
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var (templateId, _, _) = SeedModerationTemplate(db);

        var report = await Publisher.RunAsync(db, templateId, apply: true);

        Assert.Equal(PublishOutcome.Applied, report.Outcome);
        Assert.Equal(report.MigrationCountBefore, report.MigrationCountAfter);
        Assert.True(report.MigrationCountBefore > 0);
    }
}
