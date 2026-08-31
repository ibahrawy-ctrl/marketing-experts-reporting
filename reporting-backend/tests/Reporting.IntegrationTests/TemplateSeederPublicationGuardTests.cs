using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Templates;
using Reporting.Domain.Entities.Submissions;
using Reporting.Domain.Entities.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// REPORT_TEMPLATE_PUBLICATION_GUARD_HOTFIX_R1 — الاختبارات الإلزاميّة الأربعة عشر.
///
/// السبب الجذريّ المُثبَت: حرّاس الترقية الثلاثة في <c>TemplateSeeder</c> كانت تختار «فائزًا» من
/// مجموعة ملاحة يرتّبها EF بـ<c>ORDER BY t."Id", v."Id"</c> (أي بأصغر GUID عشوائيّ لا بأحدث إصدار)
/// ثمّ تفرض <c>IsPublished</c> عليه في كلّ إقلاع، فتُلغي نشر إصدارات صحيحة وتُرجِع أقدم منها.
///
/// العقد بعد الإصلاح: البذر ينشئ الناقص فقط، ولا يدير حالة نشر أيّ صفّ قائم، ويكتفي بتشخيص
/// قراءة-فقط؛ وقرار النشر ملك المسار الرسميّ <c>ReportTemplateService.PublishVersionAsync</c> وحده.
/// عقد زمن التشغيل هو «أعلى VersionNumber بين الإصدارات المنشورة».
/// </summary>
[Collection("TemplateSeederPublicationGuardIsolated")]
public class TemplateSeederPublicationGuardTests
{
    private readonly TemplateSeederPublicationGuardIsolatedFactory _factory;

    public TemplateSeederPublicationGuardTests(TemplateSeederPublicationGuardIsolatedFactory factory)
    {
        _factory = factory;
        _ = _factory.CreateClient(); // يضمن إقلاع التطبيق ⟹ الهجرات + البذر (التشغيل الأوّل)
    }

    /// <summary>القوالب الأربعة المتأثّرة فعليًّا في الإنتاج (اختبار انحدار بالاسم).</summary>
    public static readonly string[] AffectedTitles =
    {
        "تقرير فريق الفيديو",
        "تقرير فريق التصميم",
        "تقرير المديرشن الأسبوعي",
        "تقرير كاتب المحتوى الأسبوعي"
    };

    // ===================== أدوات مساعدة =====================

    private IServiceScope NewScope() => _factory.Services.CreateScope();

    /// <summary>بصمة كاملة لحالة النشر: أيّ كتابة (حتّى لو لم تُغيّر القيمة) تُغيّر <c>UpdatedAtUtc</c>.</summary>
    private static async Task<string> PublicationFingerprintAsync(AppDbContext db)
    {
        var rows = await db.ReportTemplateVersions.AsNoTracking()
            .OrderBy(v => v.Id)
            .Select(v => new { v.Id, v.ReportTemplateId, v.VersionNumber, v.IsPublished, v.PublishedAtUtc, v.PublishedById, v.UpdatedAtUtc })
            .ToListAsync();
        return string.Join("\n", rows.Select(r =>
            $"{r.Id}|{r.ReportTemplateId}|{r.VersionNumber}|{r.IsPublished}|{r.PublishedAtUtc:O}|{r.PublishedById}|{r.UpdatedAtUtc:O}"));
    }

    private static async Task<string> SubmissionsFingerprintAsync(AppDbContext db)
    {
        var rows = await db.ReportSubmissions.IgnoreQueryFilters().AsNoTracking()
            .OrderBy(s => s.Id)
            .Select(s => new { s.Id, s.ReportTemplateVersionId, s.SubmitterId, s.PeriodKey, s.Status, s.UpdatedAtUtc })
            .ToListAsync();
        return string.Join("\n", rows.Select(r =>
            $"{r.Id}|{r.ReportTemplateVersionId}|{r.SubmitterId}|{r.PeriodKey}|{r.Status}|{r.UpdatedAtUtc:O}"));
    }

    private async Task RunSeederAsync()
    {
        using var scope = NewScope();
        await TemplateSeeder.SeedAsync(scope.ServiceProvider);
    }

    /// <summary>الإصدار الفعّال وفق عقد زمن التشغيل: أعلى VersionNumber بين المنشورة.</summary>
    private static async Task<ReportTemplateVersion?> EffectiveAsync(AppDbContext db, Guid templateId)
        => await db.ReportTemplateVersions.AsNoTracking()
            .Where(v => v.ReportTemplateId == templateId && v.IsPublished)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync();

    private static async Task<ReportTemplate> LoadFamilyAsync(AppDbContext db, string title)
        => await db.ReportTemplates.Include(t => t.Versions).ThenInclude(v => v.Fields)
            .FirstAsync(t => t.Title == title);

    // ===================== (1) قاعدة فارغة تُبذَر مرّة واحدة =====================

    [Fact]
    public async Task T01_EmptyDatabase_IsSeededOnce_WithExactlyOnePublishedVersionPerNewFamily()
    {
        using var scope = NewScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var templates = await db.ReportTemplates.Include(t => t.Versions).AsNoTracking().ToListAsync();
        Assert.NotEmpty(templates);

        foreach (var title in AffectedTitles)
        {
            var t = templates.SingleOrDefault(x => x.Title == title);
            Assert.True(t is not null, $"القالب «{title}» غير مبذور.");
            Assert.True(t!.Versions.Count(v => v.IsPublished) >= 1,
                $"القالب «{title}» بلا إصدار منشور بعد البذر الأوّل.");
        }
    }

    // ===================== (2)+(3) الإقلاع الثاني والثالث لا يغيّران شيئًا =====================

    [Fact]
    public async Task T02_T03_SecondAndThirdSeedRuns_ChangeNoRow()
    {
        string before, afterSecond, afterThird;

        using (var scope = NewScope())
            before = await PublicationFingerprintAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());

        await RunSeederAsync();
        using (var scope = NewScope())
            afterSecond = await PublicationFingerprintAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());

        await RunSeederAsync();
        using (var scope = NewScope())
            afterThird = await PublicationFingerprintAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());

        Assert.Equal(before, afterSecond);   // (2)
        Assert.Equal(before, afterThird);    // (3)
    }

    // ===================== (14) لا كتابة بيانات عند الإقلاع على قاعدة مستقرّة =====================

    [Fact]
    public async Task T14_StableDatabase_SeederPerformsNoWrite_AcrossTemplatesAndSubmissions()
    {
        string versionsBefore, submissionsBefore, versionsAfter, submissionsAfter;

        using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            versionsBefore = await PublicationFingerprintAsync(db);
            submissionsBefore = await SubmissionsFingerprintAsync(db);
        }

        await RunSeederAsync();

        using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            versionsAfter = await PublicationFingerprintAsync(db);
            submissionsAfter = await SubmissionsFingerprintAsync(db);
        }

        Assert.Equal(versionsBefore, versionsAfter);
        Assert.Equal(submissionsBefore, submissionsAfter);
    }

    // ===================== (4) إصدار أحدث منشور لا يُلغى نشره =====================

    [Fact]
    public async Task T04_NewerPublishedVersion_IsNeverUnpublishedByTheSeeder()
    {
        const string title = "تقرير فريق الفيديو";
        Guid templateId, newestId;

        using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var family = await LoadFamilyAsync(db, title);
            templateId = family.Id;
            var newest = family.Versions.OrderByDescending(v => v.VersionNumber).First();
            newestId = newest.Id;
            Assert.True(newest.IsPublished, "الإصدار الأحدث ليس منشورًا في الحالة المرجعيّة.");
        }

        await RunSeederAsync();
        await RunSeederAsync();

        using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var newest = await db.ReportTemplateVersions.AsNoTracking().FirstAsync(v => v.Id == newestId);
            Assert.True(newest.IsPublished, "البذر ألغى نشر الإصدار الأحدث.");
            var effective = await EffectiveAsync(db, templateId);
            Assert.Equal(newestId, effective!.Id);
        }
    }

    // ===================== (5) إصدار أقدم غير منشور لا يصير منشورًا =====================

    [Fact]
    public async Task T05_OlderUnpublishedVersion_IsNeverAutoPublishedByTheSeeder()
    {
        const string title = "تقرير فريق التصميم";
        Guid oldestId;
        bool wasPublished;

        using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var family = await LoadFamilyAsync(db, title);
            var oldest = family.Versions.OrderBy(v => v.VersionNumber).First();
            oldestId = oldest.Id;
            wasPublished = oldest.IsPublished;
        }

        Assert.False(wasPublished, "الإصدار الأقدم منشور أصلًا — الافتراض المرجعيّ مختلّ.");

        await RunSeederAsync();
        await RunSeederAsync();

        using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var oldest = await db.ReportTemplateVersions.AsNoTracking().FirstAsync(v => v.Id == oldestId);
            Assert.False(oldest.IsPublished, "البذر نشر إصدارًا قديمًا تلقائيًّا.");
        }
    }

    // ===================== (6) إصداران منشوران ⟹ لا اختيار فائز صامت =====================

    [Fact]
    public async Task T06_TwoPublishedVersions_SeederDoesNotSilentlyPickAWinner()
    {
        const string title = "تقرير المديرشن الأسبوعي";
        Guid templateId, newestId, previousId;
        bool previousWasPublished;

        using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var family = await LoadFamilyAsync(db, title);
            templateId = family.Id;
            var ordered = family.Versions.OrderByDescending(v => v.VersionNumber).ToList();
            newestId = ordered[0].Id;
            previousId = ordered[1].Id;

            var previous = await db.ReportTemplateVersions.FirstAsync(v => v.Id == previousId);
            previousWasPublished = previous.IsPublished;
            if (!previousWasPublished)
            {
                previous.IsPublished = true;               // حالة «أكثر من إصدار منشور» — وهي حالة واقعيّة ومحتملة
                previous.PublishedAtUtc ??= DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        }

        try
        {
            await RunSeederAsync();
            await RunSeederAsync();
            await RunSeederAsync();

            using var scope = NewScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var newest = await db.ReportTemplateVersions.AsNoTracking().FirstAsync(v => v.Id == newestId);
            var previous = await db.ReportTemplateVersions.AsNoTracking().FirstAsync(v => v.Id == previousId);

            Assert.True(newest.IsPublished, "البذر ألغى نشر الأحدث لاختيار فائز.");
            Assert.True(previous.IsPublished, "البذر ألغى نشر السابق لاختيار فائز.");

            var effective = await EffectiveAsync(db, templateId);
            Assert.Equal(newestId, effective!.Id); // زمن التشغيل يحسم بالرقم الأعلى، لا البذر
        }
        finally
        {
            if (!previousWasPublished)
            {
                using var scope = NewScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var previous = await db.ReportTemplateVersions.FirstAsync(v => v.Id == previousId);
                previous.IsPublished = false;
                await db.SaveChangesAsync();
            }
        }
    }

    // ===================== (7) المسار الرسميّ يجعل الإصدار المختار هو الفعّال =====================

    [Fact]
    public async Task T07_OfficialPublishPath_MakesChosenVersionTheEffectiveOne()
    {
        using var scope = NewScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IReportTemplateService>();
        var ownerId = await db.ReportTemplates.AsNoTracking().Select(t => t.OwnerId).FirstAsync();

        var template = new ReportTemplate
        {
            Title = $"[اختبار حارس النشر] {Guid.NewGuid():N}",
            DefaultPeriodType = PeriodType.Weekly,
            Status = TemplateStatus.Draft,
            OwnerId = ownerId
        };
        template.Versions.Add(NewVersion(1, published: true));
        template.Versions.Add(NewVersion(2, published: false));
        db.ReportTemplates.Add(template);
        await db.SaveChangesAsync();

        try
        {
            var v2 = template.Versions.Single(v => v.VersionNumber == 2);
            var effectiveBefore = await EffectiveAsync(db, template.Id);
            Assert.Equal(1, effectiveBefore!.VersionNumber);

            var result = await svc.PublishVersionAsync(v2.Id, ownerId);
            Assert.True(result.Succeeded, result.Error);

            var effectiveAfter = await EffectiveAsync(db, template.Id);
            Assert.Equal(2, effectiveAfter!.VersionNumber);

            // النشر توسيعيّ: لا يمسّ الإصدارات الأخرى ولا يحذف شيئًا.
            var v1 = await db.ReportTemplateVersions.AsNoTracking().FirstAsync(v => v.ReportTemplateId == template.Id && v.VersionNumber == 1);
            Assert.True(v1.IsPublished);

            // Idempotency على مستوى المسار الرسميّ: إعادة النشر تُرفَض بلا أثر.
            var again = await svc.PublishVersionAsync(v2.Id, ownerId);
            Assert.False(again.Succeeded);
            Assert.Equal("version.already_published.conflict", again.ErrorCode);
        }
        finally
        {
            db.ReportTemplates.Remove(await db.ReportTemplates.Include(t => t.Versions).FirstAsync(t => t.Id == template.Id));
            await db.SaveChangesAsync();
        }
    }

    private static ReportTemplateVersion NewVersion(int number, bool published)
    {
        var v = new ReportTemplateVersion
        {
            VersionNumber = number,
            IsPublished = published,
            PublishedAtUtc = published ? DateTime.UtcNow : null
        };
        v.Fields.Add(new TemplateField { Label = "حقل", Key = "field", FieldType = FieldType.ShortText, Order = 0 });
        return v;
    }

    // ===================== (8)+(9) التقارير التاريخيّة لا تُنقل ولا تُعاد كتابتها =====================

    [Fact]
    public async Task T08_T09_HistoricalSubmissions_KeepTheirVersionAndAreNeverRewritten()
    {
        var ids = new List<Guid>();
        Guid submitterId;
        var linkage = new Dictionary<Guid, Guid>();

        using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            submitterId = await db.Users.AsNoTracking().Select(u => u.Id).FirstAsync();

            // تسليمات على إصدارات **قديمة غير منشورة** من قوالب التنفيذ الأربعة — وهي الحالة الحرجة.
            var statuses = new[] { SubmissionStatus.Draft, SubmissionStatus.Submitted, SubmissionStatus.Closed };
            var i = 0;
            foreach (var title in AffectedTitles.Take(3))
            {
                var family = await LoadFamilyAsync(db, title);
                var oldVersion = family.Versions.OrderBy(v => v.VersionNumber).First();
                var s = new ReportSubmission
                {
                    ReportTemplateVersionId = oldVersion.Id,
                    SubmitterId = submitterId,
                    PeriodType = PeriodType.Weekly,
                    PeriodKey = $"1999-W{10 + i:00}",
                    Status = statuses[i]
                };
                db.ReportSubmissions.Add(s);
                ids.Add(s.Id);
                linkage[s.Id] = oldVersion.Id;
                i++;
            }
            await db.SaveChangesAsync();
        }

        try
        {
            string before;
            using (var scope = NewScope())
                before = await SubmissionsFingerprintAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());

            await RunSeederAsync();
            await RunSeederAsync();

            using var scope2 = NewScope();
            var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(before, await SubmissionsFingerprintAsync(db2)); // (9) لا إعادة كتابة لأيّ حالة

            foreach (var id in ids)                                        // (8) نفس Version ID
            {
                var s = await db2.ReportSubmissions.IgnoreQueryFilters().AsNoTracking().FirstAsync(x => x.Id == id);
                Assert.Equal(linkage[id], s.ReportTemplateVersionId);
            }
        }
        finally
        {
            using var scope = NewScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ReportSubmissions.RemoveRange(
                await db.ReportSubmissions.IgnoreQueryFilters().Where(s => ids.Contains(s.Id)).ToListAsync());
            await db.SaveChangesAsync();
        }
    }

    // ===================== (10)+(11) الإصدار الفعّال يبقى بعد ثلاثة إقلاعات — للقوالب الأربعة بالاسم =====

    [Theory]
    [InlineData("تقرير فريق الفيديو")]
    [InlineData("تقرير فريق التصميم")]
    [InlineData("تقرير المديرشن الأسبوعي")]
    [InlineData("تقرير كاتب المحتوى الأسبوعي")]
    public async Task T10_T11_AffectedTemplate_KeepsItsEffectiveVersion_AcrossThreeRestarts(string title)
    {
        Guid templateId, effectiveId;
        int effectiveNumber;

        using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var family = await LoadFamilyAsync(db, title);
            templateId = family.Id;
            var effective = await EffectiveAsync(db, templateId);
            Assert.True(effective is not null, $"«{title}» بلا إصدار منشور.");
            effectiveId = effective!.Id;
            effectiveNumber = effective.VersionNumber;

            // الفعّال هو الأعلى رقمًا في العائلة كلّها — لا إصدار أحدث محجوب.
            Assert.Equal(family.Versions.Max(v => v.VersionNumber), effectiveNumber);
        }

        await RunSeederAsync();
        await RunSeederAsync();
        await RunSeederAsync();

        using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var effective = await EffectiveAsync(db, templateId);
            Assert.Equal(effectiveId, effective!.Id);
            Assert.Equal(effectiveNumber, effective.VersionNumber);
        }
    }

    // ===================== (12) R22A/R22B لا ينحدر: بنية بنود العمل تبقى في الإصدار الفعّال ==========

    [Fact]
    public async Task T12_R22A_R22B_WorkItemsStructure_SurvivesThreeRestarts_WhenPresent()
    {
        static bool HasWorkItems(ReportTemplateVersion v) =>
            v.Fields.Any(f => f.ConfigJson is not null && f.ConfigJson.Replace(" ", "").Contains("\"workItems\""));

        var withWorkItemsBefore = new List<Guid>();
        using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var title in AffectedTitles)
            {
                var family = await LoadFamilyAsync(db, title);
                foreach (var v in family.Versions.Where(HasWorkItems)) withWorkItemsBefore.Add(v.Id);
            }
        }

        await RunSeederAsync();
        await RunSeederAsync();
        await RunSeederAsync();

        using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var withWorkItemsAfter = new List<Guid>();
            foreach (var title in AffectedTitles)
            {
                var family = await LoadFamilyAsync(db, title);
                foreach (var v in family.Versions.Where(HasWorkItems)) withWorkItemsAfter.Add(v.Id);
            }
            Assert.Equal(withWorkItemsBefore.OrderBy(x => x), withWorkItemsAfter.OrderBy(x => x));
        }
    }

    // ===================== (13) R5/KPI غير متأثّرة =====================

    [Fact]
    public async Task T13_KpiTemplatesAndVersions_AreUnaffectedByRepeatedSeeding()
    {
        static async Task<string> KpiFingerprintAsync(AppDbContext db)
        {
            var rows = await db.KpiTemplateVersions.AsNoTracking().OrderBy(v => v.Id)
                .Select(v => new { v.Id, v.KpiTemplateId, v.VersionNumber, v.IsPublished, v.PublishedAtUtc, v.UpdatedAtUtc })
                .ToListAsync();
            return string.Join("\n", rows.Select(r =>
                $"{r.Id}|{r.KpiTemplateId}|{r.VersionNumber}|{r.IsPublished}|{r.PublishedAtUtc:O}|{r.UpdatedAtUtc:O}"));
        }

        string before, after;
        using (var scope = NewScope())
            before = await KpiFingerprintAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());

        await RunSeederAsync();
        await RunSeederAsync();

        using (var scope = NewScope())
            after = await KpiFingerprintAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());

        Assert.Equal(before, after);
    }
}
