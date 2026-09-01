using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Entities.Submissions;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Reporting.Infrastructure.Services;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// <b>R22B / DEFECT-IDEMPOTENCY-01 — عقد «هويّة التقرير = نَسَب القالب لا لقطة إصدارٍ منه».</b>
/// <para>
/// <b>العيب المُصلَح:</b> كان مفتاح البحث عن تقرير قائم هو
/// <c>(ReportTemplateVersionId, SubmitterId, PeriodKey)</c> — أي <b>مقيَّدًا بالإصدار</b>. فحين يتغيّر
/// «الإصدار النافذ» (أعلى <c>VersionNumber</c> بين المنشورة) يصير مفتاح البحث مغايرًا لمفتاح التقرير
/// القائم ⟹ لا يراه النظام فيُنشئ تقريرًا ثانيًا لنفس (موظّف، قالب، فترة). وهو الخطر الذي فجّره
/// تقلُّبُ حالة النشر عند كلّ إقلاع (<c>DEFECT-SEEDER-01</c>).
/// </para>
/// <para>
/// <b>العقد المُثبَت هنا:</b> المفتاح المنطقيّ صار
/// <c>(ReportTemplateId, SubmitterId, PeriodKey)</c> عبر <b>كلّ</b> إصدارات القالب.
/// <c>0</c> ⟹ يُنشأ · <c>1</c> ⟹ يُعاد القائم كما هو بإصداره هو (لا بديل ولا استبدال) ·
/// <c>&gt;1</c> ⟹ Conflict تشخيصيّ يسرد المعرّفات بلا اختيار عشوائيّ.
/// وانتهاكُ التفرّد لا يُبتلع إلّا إن كان من الفهرس المحدَّد بعينه وفسّره سجلٌّ قائم.
/// </para>
/// <para><b>الأعلام المُقيسة:</b>
/// <c>SAME_VERSION_CONCURRENCY_GUARD = PASS</c> ·
/// <c>APPLICATION_CROSS_VERSION_CHECK = PASS</c> ·
/// <c>DATABASE_CROSS_VERSION_UNIQUENESS = NOT_IMPLEMENTED</c> ·
/// <c>CROSS_VERSION_PUBLISH_RACE = ACCEPTED_RESIDUAL_RISK</c> ·
/// <c>MIGRATION_DEFERRED = YES</c>.
/// <br/>
/// <b>ما لا يُدَّعى هنا صراحةً:</b> لا يوجد اختبار يزعم أنّ «تبديل الإصدار النافذ بين لحظتَي حسمه في
/// طلبين متزامنين» مضمون قاعديًّا — فهو غير مضمون بلا عمود مُزال التطبيع وفهرس فريد (= Migration).
/// المضمون قاعديًّا هو تصادمُ طلبين حَسَما <b>نفس</b> الإصدار (I1)، والباقي حارسٌ تطبيقيّ + رصدٌ
/// تشخيصيّ (I4). التذكرة المؤجَّلة: <c>FOLLOW_UP = DEFECT-IDEMPOTENCY-DB-INVARIANT</c>.
/// </para>
/// </summary>
[Collection("SubmissionIdempotencyIsolated")]
public class SubmissionIdempotencyContractTests
{
    private readonly SubmissionIdempotencyIsolatedFactory _factory;

    public SubmissionIdempotencyContractTests(SubmissionIdempotencyIsolatedFactory factory) => _factory = factory;

    // ===================== أدوات مساعدة =====================

    /// <summary>
    /// قالب أسبوعيّ <b>تكميليّ</b> منشور. التكميليّ مقصود: يعزل عقدَ الهويّة عن حارس «التقرير الأساسيّ
    /// الواحد لكلّ فترة» فلا يختلط سببُ الرفض في اختبارات تستعمل أكثر من قالب لنفس الفترة.
    /// </summary>
    private static async Task<(Guid TemplateId, Guid VersionId)> PublishTemplateAsync(HttpClient admin)
    {
        var created = await (await admin.PostAsJsonAsync("/api/report-templates",
                new CreateTemplateRequest($"قالب هويّة {Guid.NewGuid():N}", null, null,
                    PeriodType.Weekly, TemplateClassification.Supplementary)))
            .ReadAsync<ReportTemplateDetailDto>();
        var versionId = created!.Versions.Single().Id;
        await admin.PostAsJsonAsync($"/api/report-templates/versions/{versionId}/fields",
            new UpsertFieldRequest("قيمة", "value", FieldType.Number, true, null, null));
        (await admin.PostAsync($"/api/report-templates/versions/{versionId}/publish", null)).EnsureSuccessStatusCode();
        return (created.Id, versionId);
    }

    /// <summary>ينشر إصدارًا أحدث بالمسار الإداريّ الرسميّ وحده (لا كتابة مباشرة على حالة النشر).</summary>
    private static async Task<Guid> PublishNextVersionAsync(HttpClient admin, Guid templateId)
    {
        var draft = await (await admin.PostAsync($"/api/report-templates/{templateId}/versions", null))
            .ReadAsync<TemplateVersionDto>();
        (await admin.PostAsync($"/api/report-templates/versions/{draft!.Id}/publish", null)).EnsureSuccessStatusCode();
        return draft.Id;
    }

    /// <summary>عدد تقارير المستخدم لنفس الفترة عبر <b>كلّ</b> إصدارات القالب = المفتاح المنطقيّ نفسه.</summary>
    private async Task<int> CountByLineageAsync(Guid templateId, Guid userId, string periodKey)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ReportSubmissions
            .Where(s => s.SubmitterId == userId && s.PeriodKey == periodKey)
            .Join(db.ReportTemplateVersions, s => s.ReportTemplateVersionId, v => v.Id, (s, v) => v.ReportTemplateId)
            .CountAsync(tid => tid == templateId);
    }

    private static Task<HttpResponseMessage> CreateAsync(HttpClient client, Guid templateId, string periodKey)
        => client.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Weekly, periodKey));

    private static async Task<SubmissionDto> CreateOkAsync(HttpClient client, Guid templateId, string periodKey)
    {
        var res = await CreateAsync(client, templateId, periodKey);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.ReadAsync<SubmissionDto>())!;
    }

    // ===================== الاختبارات التسعة =====================

    /// <summary>
    /// (1) طلبان متزامنان يحسمان <b>نفس</b> الإصدار النافذ ⟹ تقرير واحد لا اثنان.
    /// <para>
    /// جزآن: (أ) ثمانية طلبات متوازية ⟹ معرّف واحد وصفّ واحد؛ (ب) <b>قياس حتميّ</b> للضمان القاعديّ
    /// بمحاولة إدراج صفّ ثانٍ مباشرةً على نفس <c>(ReportTemplateVersionId, SubmitterId, PeriodKey)</c>:
    /// PostgreSQL يرفضه بالفهرس الفريد الجزئيّ، ومرشِّح الخدمة يتعرّف على ذلك الانتهاك بعينه.
    /// الجزء (ب) ضروريّ لأنّ سباق HTTP توقيتيّ قد لا يقع، فلا يصحّ أن يُبنى عليه علم
    /// <c>SAME_VERSION_CONCURRENCY_GUARD</c> وحده.
    /// </para>
    /// </summary>
    [Fact]
    public async Task I1_TwoConcurrentRequests_ResolvingSameVersion_YieldExactlyOneReport()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, versionId) = await PublishTemplateAsync(admin);
        var (employee, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        const string period = "2026-W21";

        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => CreateAsync(employee, templateId, period)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var dtos = new List<SubmissionDto>();
        foreach (var r in responses) dtos.Add((await r.ReadAsync<SubmissionDto>())!);
        Assert.Single(dtos.Select(d => d.Id).Distinct());
        Assert.All(dtos, d => Assert.Equal(versionId, d.ReportTemplateVersionId));
        Assert.Equal(1, await CountByLineageAsync(templateId, employeeId, period));

        // (ب) الضمان القاعديّ نفسه، مقيسًا لا مفترَضًا.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ReportSubmissions.Add(new ReportSubmission
        {
            ReportTemplateVersionId = versionId,
            SubmitterId = employeeId,
            PeriodType = PeriodType.Weekly,
            PeriodKey = period,
            Status = SubmissionStatus.Draft
        });
        var violation = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.True(SubmissionService.IsPeriodUniqueViolation(violation));
        Assert.Equal(1, await CountByLineageAsync(templateId, employeeId, period));
    }

    /// <summary>
    /// (2) تقرير قائم على إصدار أقدم ثمّ يُنشَر إصدار أحدث ⟹ لا تقرير ثانٍ، ويبقى القائم
    /// مربوطًا <b>بإصداره هو</b> (التاريخ لا يُمسّ ولا يُرحَّل صامتًا).
    /// </summary>
    [Fact]
    public async Task I2_NewVersionPublishedAfterExistingReport_DoesNotCreateSecondReport()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, v1) = await PublishTemplateAsync(admin);
        var (employee, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        const string period = "2026-W22";

        var before = await CreateOkAsync(employee, templateId, period);
        Assert.Equal(v1, before.ReportTemplateVersionId);

        var v2 = await PublishNextVersionAsync(admin, templateId);
        Assert.NotEqual(v1, v2);

        var after = await CreateOkAsync(employee, templateId, period);

        Assert.Equal(before.Id, after.Id);
        Assert.Equal(v1, after.ReportTemplateVersionId);
        Assert.Equal(1, await CountByLineageAsync(templateId, employeeId, period));
    }

    /// <summary>
    /// (3) تكرار مسار الإنشاء خمس مرّات ⟹ العدد ثابت عند 1 والمعرّف واحد.
    /// ملحوظة مقيسة: لا مولِّد خلفيّ يُنشئ تقارير في المنظومة كلّها؛ مسار الإنشاء الوحيد هو
    /// <c>SubmissionService.CreateOrGetDraftAsync</c> نفسه، فتكرارُه هو المكافئ الأمين لإعادة تشغيل Job.
    /// </summary>
    [Fact]
    public async Task I3_FiveRepeatedRuns_KeepReportCountConstant()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _) = await PublishTemplateAsync(admin);
        var (employee, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        const string period = "2026-W23";

        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            ids.Add((await CreateOkAsync(employee, templateId, period)).Id);
            Assert.Equal(1, await CountByLineageAsync(templateId, employeeId, period));
        }

        Assert.Single(ids.Distinct());
    }

    /// <summary>
    /// (4) ازدواج تاريخيّ (أكثر من تقرير لنفس القالب والمستخدم والفترة على إصدارين مختلفين —
    /// وهو ما يُخلِّفه العيبُ قبل الإصلاح) ⟹ <b>Conflict تشخيصيّ يسرد المعرّفات</b>،
    /// لا اختيار عشوائيّ لأحدهما ولا إنشاء ثالث.
    /// </summary>
    [Fact]
    public async Task I4_MultipleLegacyReports_ForSameLineage_ReturnDiagnosticConflict()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _) = await PublishTemplateAsync(admin);
        var (employee, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        const string period = "2026-W24";

        var legacy = await CreateOkAsync(employee, templateId, period);
        var v2 = await PublishNextVersionAsync(admin, templateId);

        // زرعُ الازدواج التاريخيّ مباشرةً في القاعدة: الفهرس الفريد لا يمنعه لأنّه على إصدار آخر.
        Guid duplicateId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var duplicate = new ReportSubmission
            {
                ReportTemplateVersionId = v2,
                SubmitterId = employeeId,
                PeriodType = PeriodType.Weekly,
                PeriodKey = period,
                Status = SubmissionStatus.Draft
            };
            db.ReportSubmissions.Add(duplicate);
            await db.SaveChangesAsync();
            duplicateId = duplicate.Id;
        }

        Assert.Equal(2, await CountByLineageAsync(templateId, employeeId, period));

        var res = await CreateAsync(employee, templateId, period);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);

        using var problem = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal("submission.duplicate_period_reports.conflict",
            problem.RootElement.GetProperty("type").GetString());
        var detail = problem.RootElement.GetProperty("detail").GetString()!;
        Assert.Contains(legacy.Id.ToString(), detail);
        Assert.Contains(duplicateId.ToString(), detail);

        // لا إنشاء ثالث نتيجةَ الرفض.
        Assert.Equal(2, await CountByLineageAsync(templateId, employeeId, period));
    }

    /// <summary>
    /// (5) <c>DbUpdateException</c> غير ناتج عن قيد الازدواج المحدَّد بعينه ⟹ <b>لا يُبتلع</b>.
    /// يُقاس المرشِّح مباشرةً (لا استنتاجًا)، ويُحرَس اسمُ الفهرس الثابت بمقارنته باسمه الفعليّ في
    /// القاعدة حتّى لا ينزلق صامتًا فيتحوّل الالتقاط المحدَّد إلى التقاطٍ لا يقع أبدًا.
    /// </summary>
    [Fact]
    public async Task I5_UnrelatedDbUpdateException_IsNotSwallowed_AndIndexNameDoesNotDrift()
    {
        // الحالة الوحيدة المسموح بالتقاطها: 23505 + اسم الفهرس المحدَّد.
        Assert.True(SubmissionService.IsPeriodUniqueViolation(
            Fabricate(PostgresErrorCodes.UniqueViolation, SubmissionService.PeriodUniqueIndexName)));

        // انتهاك تفرّد من قيد آخر ⟹ لا يُبتلع.
        Assert.False(SubmissionService.IsPeriodUniqueViolation(
            Fabricate(PostgresErrorCodes.UniqueViolation, "IX_report_submissions_SomeOtherUniqueIndex")));
        // نفس الفهرس لكن رمز خطأ آخر ⟹ لا يُبتلع.
        Assert.False(SubmissionService.IsPeriodUniqueViolation(
            Fabricate(PostgresErrorCodes.ForeignKeyViolation, SubmissionService.PeriodUniqueIndexName)));
        // خطأ قاعديّ بلا اسم قيد (NOT NULL مثلًا) ⟹ لا يُبتلع.
        Assert.False(SubmissionService.IsPeriodUniqueViolation(
            Fabricate(PostgresErrorCodes.NotNullViolation, null)));
        // استثناء داخليّ ليس من PostgreSQL أصلًا ⟹ لا يُبتلع.
        Assert.False(SubmissionService.IsPeriodUniqueViolation(
            new DbUpdateException("فشل الحفظ", new InvalidOperationException("سبب آخر"))));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var indexNames = await db.Database
            .SqlQueryRaw<string>("SELECT indexname AS \"Value\" FROM pg_indexes WHERE tablename = 'report_submissions'")
            .ToListAsync();
        Assert.Contains(SubmissionService.PeriodUniqueIndexName, indexNames);
    }

    private static DbUpdateException Fabricate(string sqlState, string? constraintName)
        => new("فشل الحفظ",
            new PostgresException("رسالة", "ERROR", "ERROR", sqlState, constraintName: constraintName));

    /// <summary>(6) نفس القالب في فترة مختلفة ⟹ مسموح: الفترة بُعدٌ حاكم في المفتاح.</summary>
    [Fact]
    public async Task I6_SameTemplate_DifferentPeriod_IsAllowed()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, _) = await PublishTemplateAsync(admin);
        var (employee, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee");

        var w25 = await CreateOkAsync(employee, templateId, "2026-W25");
        var w26 = await CreateOkAsync(employee, templateId, "2026-W26");

        Assert.NotEqual(w25.Id, w26.Id);
        Assert.Equal(1, await CountByLineageAsync(templateId, employeeId, "2026-W25"));
        Assert.Equal(1, await CountByLineageAsync(templateId, employeeId, "2026-W26"));
    }

    /// <summary>(7) قالب مختلف في نفس الفترة ⟹ مسموح: القالب بُعدٌ حاكم في المفتاح.</summary>
    [Fact]
    public async Task I7_DifferentTemplate_SamePeriod_IsAllowed()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateA, _) = await PublishTemplateAsync(admin);
        var (templateB, _) = await PublishTemplateAsync(admin);
        var (employee, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        const string period = "2026-W27";

        var a = await CreateOkAsync(employee, templateA, period);
        var b = await CreateOkAsync(employee, templateB, period);

        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(1, await CountByLineageAsync(templateA, employeeId, period));
        Assert.Equal(1, await CountByLineageAsync(templateB, employeeId, period));
    }

    /// <summary>
    /// (8) تقرير <b>نهائيّ</b> (مغلق) على إصدار أقدم ثمّ يُنشَر إصدار أحدث ⟹ يُمنع إنشاء بديل:
    /// يُعاد التقريرُ النهائيّ نفسه بحالته وإصداره، فلا مسودّة موازية تُفرغ التقرير المعتمد من معناه.
    /// </summary>
    [Fact]
    public async Task I8_FinalReportOnOlderVersion_BlocksCreatingSubstitute()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, v1) = await PublishTemplateAsync(admin);
        var (employee, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        const string period = "2026-W28";

        var original = await CreateOkAsync(employee, templateId, period);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.ReportSubmissions.FirstAsync(s => s.Id == original.Id);
            row.Status = SubmissionStatus.Closed;
            row.SubmittedAtUtc = DateTime.UtcNow;
            row.ClosedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        await PublishNextVersionAsync(admin, templateId);

        var again = await CreateOkAsync(employee, templateId, period);

        Assert.Equal(original.Id, again.Id);
        Assert.Equal(SubmissionStatus.Closed, again.Status);
        Assert.Equal(v1, again.ReportTemplateVersionId);
        Assert.Equal(1, await CountByLineageAsync(templateId, employeeId, period));
    }

    /// <summary>
    /// (9) مسودّة على إصدار <b>غير منشور</b> (حالة مسودّة الإنتاج اليتيمة التي خلّفها
    /// <c>DEFECT-SEEDER-01</c>) ⟹ تُمنع صناعةُ بديل، و<b>لا تصير يتيمة وظيفيًّا</b>: تُفتَح كما هي
    /// بدل الرفض برسالة «لا يوجد إصدار منشور» — لأنّ البحث بالنَسَب يسبق حسمَ الإصدار النافذ.
    /// </summary>
    [Fact]
    public async Task I9_DraftOnUnpublishedVersion_IsReturnedAndNotOrphaned()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, v1) = await PublishTemplateAsync(admin);
        var (employee, employeeId) = await TestAuth.CreateUserAsync(_factory, "Employee");
        const string period = "2026-W29";

        var draft = await CreateOkAsync(employee, templateId, period);
        Assert.Equal(v1, draft.ReportTemplateVersionId);

        // يُحاكى إلغاءُ النشر الذي كان يقع صامتًا عند الإقلاع: لم يعد للقالب أيّ إصدار منشور.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var version = await db.ReportTemplateVersions.FirstAsync(v => v.Id == v1);
            version.IsPublished = false;
            await db.SaveChangesAsync();
        }

        var again = await CreateOkAsync(employee, templateId, period);

        Assert.Equal(draft.Id, again.Id);
        Assert.Equal(v1, again.ReportTemplateVersionId);
        Assert.Equal(SubmissionStatus.Draft, again.Status);
        Assert.Equal(1, await CountByLineageAsync(templateId, employeeId, period));
    }
}
