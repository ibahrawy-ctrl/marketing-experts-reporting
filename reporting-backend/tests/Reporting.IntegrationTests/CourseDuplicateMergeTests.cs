using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Application.Reports;
using Reporting.Application.Submissions;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// COURSE-DUPLICATE-MERGE-R1 — اختبارات تكامل لدمج دورة الدبلوم المكرّرة (Strategy B، توحيد وقت القراءة).
/// تُثبِت أنّ الأسماء البديلة المعتمَدة («الدبلوم الشامل» + الاسم الموحّد) تُجمَّع في مجموعة دورة واحدة باسم
/// عرض موحّد، وأنّ الإجماليات = مجموع، والمؤشرات المحسوبة يُعاد احتسابها على الإجمالي، والموظّفون المتمايزون
/// يُعدّون مرّة واحدة، مع عزل الفترة، وإسناد قائد الفريق، وثبات المسودّة، وثبات خلايا التقارير التاريخية.
/// العزل: مستخدمو اختبار فريدون + فريق فريد لكل اختبار CEO-scoped (فلتر teamId) لتفادي تراكم القاعدة المشتركة
/// الدائمة عبر التشغيلات؛ اختبار قائد الفريق معزول أصلًا بنطاق الشجرة. التواريخ ماضية غير-جمعة (≤ اليوم).
/// </summary>
[Collection("Integration")]
public class CourseDuplicateMergeTests
{
    private readonly CustomWebApplicationFactory _factory;

    public CourseDuplicateMergeTests(CustomWebApplicationFactory factory) => _factory = factory;

    // الاسمان البديلان الحقيقيّان للدورة المكرّرة (مثبتان في CourseNamePolicy).
    private const string AliasOld = "الدبلوم الشامل";
    private const string AliasCanonical = CourseNamePolicy.CanonicalDigitalDiploma; // «دبلوم التسويق الرقمي والنمو»

    private static async Task<(Guid TemplateId, Guid GridId)> GetSeededB2cTemplateAsync(HttpClient admin)
    {
        var list = await (await admin.GetAsync("/api/report-templates"))
            .ReadAsync<List<ReportTemplateDto>>();
        var summary = Assert.Single(list!.Where(t => t.Title == B2cByCourseReportSchema.TemplateTitle));
        var detail = await (await admin.GetAsync($"/api/report-templates/{summary.Id}"))
            .ReadAsync<ReportTemplateDetailDto>();
        var version = detail!.Versions.Single(v => v.IsPublished);
        var grid = Assert.Single(version.Fields.Where(f => f.FieldType == FieldType.TableGrid));
        return (detail.Id, grid.Id);
    }

    private static async Task AssignTemplateToEmployeeAsync(HttpClient admin, Guid templateId, Guid employeeId)
    {
        var res = await admin.PostAsJsonAsync($"/api/report-templates/{templateId}/assignments",
            new CreateAssignmentRequest(TemplateAssignmentScope.Employee, employeeId, TemplateAssignmentKind.Include, null));
        res.EnsureSuccessStatusCode();
    }

    // صفّ B2C بـ10 خلايا: الدورة، ساعات العمل، Leads، Contacted، Qualified، Follow-ups، Sales، Revenue، Lost، السبب.
    private static string[] B2cRow(string course, int work, int leads, int contacted, int qualified,
        int follow, int sales, int revenue, int lost)
        => new[] { course, work.ToString(), leads.ToString(), contacted.ToString(), qualified.ToString(),
                   follow.ToString(), sales.ToString(), revenue.ToString(), lost.ToString(), "" };

    // إنشاء مسودّة + حفظ صفوف الجدول (بلا إرسال). يُرجِع معرّف التسليم.
    private static async Task<Guid> CreateDraftWithRowsAsync(
        HttpClient employee, Guid templateId, Guid gridId, string date, params string[][] rows)
    {
        var draftRes = await employee.PostAsJsonAsync("/api/submissions",
            new CreateSubmissionRequest(templateId, PeriodType.Daily, date));
        Assert.True(draftRes.IsSuccessStatusCode,
            $"draft POST failed {(int)draftRes.StatusCode}: {await draftRes.Content.ReadAsStringAsync()}");
        var draft = await draftRes.ReadAsync<SubmissionDto>();
        var gridJson = JsonSerializer.Serialize(rows);
        var save = await employee.PutAsJsonAsync($"/api/submissions/{draft!.Id}/values",
            new SaveFieldValuesRequest(new[] { new FieldValueInput(gridId, null, null, null, null, gridJson) }));
        save.EnsureSuccessStatusCode();
        return draft.Id;
    }

    private static async Task SubmitAndApproveAsync(HttpClient employee, HttpClient approver, Guid submissionId)
    {
        (await employee.PostAsync($"/api/submissions/{submissionId}/submit", null)).EnsureSuccessStatusCode();
        var approved = await (await approver.PostAsJsonAsync($"/api/submissions/{submissionId}/approve",
            new ApprovalActionRequest("معتمد"))).ReadAsync<SubmissionDto>();
        Assert.True(approved!.Status is SubmissionStatus.Closed or SubmissionStatus.ApprovedByDirectManager);
    }

    private static async Task SubmitDailyAsync(HttpClient employee, HttpClient approver,
        Guid templateId, Guid gridId, string date, params string[][] rows)
    {
        var id = await CreateDraftWithRowsAsync(employee, templateId, gridId, date, rows);
        await SubmitAndApproveAsync(employee, approver, id);
    }

    // مجموعة الدورة الموحّدة (اسم العرض الرسميّ) من نتيجة التجميع.
    private static B2cCourseGroupRow CanonicalGroup(B2cCourseGroupedReport report)
        => Assert.Single(report.Courses.Where(c => c.Course == AliasCanonical));

    // ===== Test C — دمج التجميع: اسمان بديلان ⇒ مجموعة دورة واحدة باسم عرض موحّد =====
    [Fact]
    public async Task C_TwoAliases_MergeIntoSingleCanonicalCourseGroup()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, gridId) = await GetSeededB2cTemplateAsync(admin);
        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (emp1, emp1Id) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", ceoId);
        var (emp2, emp2Id) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", ceoId);
        await AssignTemplateToEmployeeAsync(admin, templateId, emp1Id);
        await AssignTemplateToEmployeeAsync(admin, templateId, emp2Id);
        // عزل: فريق فريد لهذا الاختبار + فلتر teamId يمنع تسرّب تسليمات تشغيلات سابقة (القاعدة المشتركة الدائمة).
        var teamId = await TestAuth.CreateTeamWithLeaderAsync(_factory, ceoId, emp1Id, emp2Id);

        const string date = "2026-01-06";
        await SubmitDailyAsync(emp1, ceo, templateId, gridId, date, B2cRow(AliasOld, 10, 40, 30, 20, 15, 6, 18000, 4));
        await SubmitDailyAsync(emp2, ceo, templateId, gridId, date, B2cRow(AliasCanonical, 5, 10, 8, 5, 3, 4, 6000, 1));

        var report = await (await ceo.GetAsync(
            $"/api/reporting/aggregation/b2c/by-course?periodType=Daily&periodKey={date}&teamId={teamId}"))
            .ReadAsync<B2cCourseGroupedReport>();

        // مجموعة واحدة فقط للدورة الموحّدة رغم اسمين بديلين مختلفين.
        var group = CanonicalGroup(report!);
        // لا يوجد أيّ مجموعة تحمل الاسم القديم منفصلة.
        Assert.DoesNotContain(report!.Courses, c => c.Course == AliasOld);
        Assert.Equal(AliasCanonical, group.Course);
    }

    // ===== Test D — المؤشرات الجمعيّة = مجموع دقيق =====
    // ===== Test F — الموظّفون المتمايزون يُعدّون مرّة واحدة =====
    [Fact]
    public async Task D_F_AdditiveMetricsAreExactSum_DistinctEmployeesCountedOnce()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, gridId) = await GetSeededB2cTemplateAsync(admin);
        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (emp1, emp1Id) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", ceoId);
        var (emp2, emp2Id) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", ceoId);
        await AssignTemplateToEmployeeAsync(admin, templateId, emp1Id);
        await AssignTemplateToEmployeeAsync(admin, templateId, emp2Id);
        var teamId = await TestAuth.CreateTeamWithLeaderAsync(_factory, ceoId, emp1Id, emp2Id);

        const string date = "2026-02-03";
        await SubmitDailyAsync(emp1, ceo, templateId, gridId, date, B2cRow(AliasOld, 10, 40, 30, 20, 15, 6, 18000, 4));
        await SubmitDailyAsync(emp2, ceo, templateId, gridId, date, B2cRow(AliasCanonical, 5, 10, 8, 5, 3, 4, 6000, 1));

        var report = await (await ceo.GetAsync(
            $"/api/reporting/aggregation/b2c/by-course?periodType=Daily&periodKey={date}&teamId={teamId}"))
            .ReadAsync<B2cCourseGroupedReport>();
        var group = CanonicalGroup(report!);

        // D — الجمعيّات = المجموع الدقيق.
        Assert.Equal(15m, group.WorkHours);
        Assert.Equal(50m, group.Leads);
        Assert.Equal(38m, group.Contacted);
        Assert.Equal(25m, group.Qualified);
        Assert.Equal(18m, group.FollowUps);
        Assert.Equal(10m, group.Sales);
        Assert.Equal(24000m, group.Revenue);
        Assert.Equal(5m, group.Lost);
        // الإجمالي = مجموع مساهمات الموظّفين في Drill-down.
        Assert.Equal(group.Sales, group.Employees.Sum(e => e.Sales));
        Assert.Equal(group.Revenue, group.Employees.Sum(e => e.Revenue));

        // F — موظّفان متمايزان يُعدّان مرّة واحدة لكلٍّ (لا تكرار بسبب اختلاف الاسم البديل).
        Assert.Equal(2, group.EmployeeCount);
        Assert.Equal(2, group.Employees.Count);
        Assert.Contains(group.Employees, e => e.EmployeeId == emp1Id);
        Assert.Contains(group.Employees, e => e.EmployeeId == emp2Id);
    }

    // ===== Test E — المؤشرات المحسوبة يُعاد احتسابها على الإجمالي (لا جمع النِّسب) =====
    [Fact]
    public async Task E_DerivedMetricsRecalculatedOnMergedTotals_NotSummedRates()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, gridId) = await GetSeededB2cTemplateAsync(admin);
        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (emp1, emp1Id) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", ceoId);
        var (emp2, emp2Id) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", ceoId);
        await AssignTemplateToEmployeeAsync(admin, templateId, emp1Id);
        await AssignTemplateToEmployeeAsync(admin, templateId, emp2Id);
        var teamId = await TestAuth.CreateTeamWithLeaderAsync(_factory, ceoId, emp1Id, emp2Id);

        const string date = "2026-03-03";
        // نِسب فرديّة مختلفة جدًّا: alias1 conv=15%، alias2 conv=40% ⇒ الجمع الساذج=55 بينما الصحيح=20.
        await SubmitDailyAsync(emp1, ceo, templateId, gridId, date, B2cRow(AliasOld, 10, 40, 30, 20, 15, 6, 18000, 4));
        await SubmitDailyAsync(emp2, ceo, templateId, gridId, date, B2cRow(AliasCanonical, 5, 10, 8, 5, 3, 4, 6000, 1));

        var report = await (await ceo.GetAsync(
            $"/api/reporting/aggregation/b2c/by-course?periodType=Daily&periodKey={date}&teamId={teamId}"))
            .ReadAsync<B2cCourseGroupedReport>();
        var group = CanonicalGroup(report!);

        // إجمالي: sales=10, leads=50 ⇒ conversion=20.0 (لا 55)، qualified=25 ⇒ 50.0، contacted=38 ⇒ 76.0، lost=5 ⇒ 10.0
        Assert.Equal(20.0m, group.ConversionRate);
        Assert.Equal(50.0m, group.QualificationRate);
        Assert.Equal(76.0m, group.ContactRate);
        Assert.Equal(10.0m, group.LostRate);
        // Per-hour: revenue=24000/work=15 ⇒ 1600.00؛ sales=10/15 ⇒ 0.67
        Assert.Equal(1600.00m, group.RevenuePerHour);
        Assert.Equal(0.67m, group.SalesPerHour);
    }

    // ===== Test F (تعزيز) — نفس الموظّف يسجّل الاسمين البديلين في صفّين ⇒ يُعدّ مرّة واحدة =====
    [Fact]
    public async Task F_SameEmployeeBothAliases_CountedOnce_ContributionsSummed()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, gridId) = await GetSeededB2cTemplateAsync(admin);
        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (emp, empId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", ceoId);
        await AssignTemplateToEmployeeAsync(admin, templateId, empId);
        var teamId = await TestAuth.CreateTeamWithLeaderAsync(_factory, ceoId, empId);

        const string date = "2026-04-07";
        // تسليم واحد بصفّين بالاسمين البديلين (تفرّد التسليم لكل فترة يمنع تسليمين).
        await SubmitDailyAsync(emp, ceo, templateId, gridId, date,
            B2cRow(AliasOld, 10, 40, 30, 20, 15, 6, 18000, 4),
            B2cRow(AliasCanonical, 5, 10, 8, 5, 3, 4, 6000, 1));

        var report = await (await ceo.GetAsync(
            $"/api/reporting/aggregation/b2c/by-course?periodType=Daily&periodKey={date}&teamId={teamId}"))
            .ReadAsync<B2cCourseGroupedReport>();
        var group = CanonicalGroup(report!);

        Assert.Equal(1, group.EmployeeCount);
        var e = Assert.Single(group.Employees);
        Assert.Equal(empId, e.EmployeeId);
        Assert.Equal(10m, e.Sales);        // 6 + 4
        Assert.Equal(24000m, e.Revenue);   // 18000 + 6000
        Assert.Equal(10m, group.Sales);
        Assert.Equal(24000m, group.Revenue);
    }

    // ===== Test G — عزل الفترة: مساهمة فترة أخرى لا تتسرّب =====
    [Fact]
    public async Task G_PeriodIsolation_OtherDateContributionNotLeaked()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, gridId) = await GetSeededB2cTemplateAsync(admin);
        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (emp, empId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", ceoId);
        await AssignTemplateToEmployeeAsync(admin, templateId, empId);
        var teamId = await TestAuth.CreateTeamWithLeaderAsync(_factory, ceoId, empId);

        const string dateA = "2026-05-05";
        const string dateB = "2026-05-12";
        await SubmitDailyAsync(emp, ceo, templateId, gridId, dateA, B2cRow(AliasOld, 10, 40, 30, 20, 15, 6, 18000, 4));
        await SubmitDailyAsync(emp, ceo, templateId, gridId, dateB, B2cRow(AliasCanonical, 5, 10, 8, 5, 3, 4, 6000, 1));

        var reportA = await (await ceo.GetAsync(
            $"/api/reporting/aggregation/b2c/by-course?periodType=Daily&periodKey={dateA}&teamId={teamId}"))
            .ReadAsync<B2cCourseGroupedReport>();
        var groupA = CanonicalGroup(reportA!);
        // فترة A تحمل مساهمة A فقط (لا يتسرّب B رغم أنّه اسم بديل للدورة نفسها).
        Assert.Equal(6m, groupA.Sales);
        Assert.Equal(18000m, groupA.Revenue);
        Assert.Equal(40m, groupA.Leads);
    }

    // ===== Test H — إسناد قائد الفريق: الدمج يحفظ مساهمة كل موظّف صحيحةً ضمن نطاق الفريق =====
    [Fact]
    public async Task H_TeamLeaderScope_MergedGroup_AttributesEachEmployeeCorrectly()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, gridId) = await GetSeededB2cTemplateAsync(admin);
        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (tl, tlId) = await TestAuth.CreateUserAsync(_factory, "TeamLeader", ceoId);
        var (inA, inAId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", tlId);
        var (inB, inBId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", tlId);
        var (outEmp, outId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", ceoId);
        await AssignTemplateToEmployeeAsync(admin, templateId, inAId);
        await AssignTemplateToEmployeeAsync(admin, templateId, inBId);
        await AssignTemplateToEmployeeAsync(admin, templateId, outId);

        const string date = "2026-06-02";
        await SubmitDailyAsync(inA, tl, templateId, gridId, date, B2cRow(AliasOld, 10, 40, 30, 20, 15, 6, 18000, 4));
        await SubmitDailyAsync(inB, tl, templateId, gridId, date, B2cRow(AliasCanonical, 5, 10, 8, 5, 3, 4, 6000, 1));
        await SubmitDailyAsync(outEmp, ceo, templateId, gridId, date, B2cRow(AliasOld, 8, 32, 24, 14, 7, 5, 15000, 2));

        var tlReport = await (await tl.GetAsync(
            $"/api/reporting/aggregation/b2c/by-course?periodType=Daily&periodKey={date}"))
            .ReadAsync<B2cCourseGroupedReport>();
        var group = CanonicalGroup(tlReport!);

        // قائد الفريق يرى موظّفَي فريقه فقط (لا الموظّف الخارج)، مدموجَين في مجموعة موحّدة واحدة.
        Assert.Equal(2, group.EmployeeCount);
        Assert.DoesNotContain(group.Employees, e => e.EmployeeId == outId);
        // إسناد صحيح لكل موظّف داخل الدمج.
        Assert.Equal(6m, Assert.Single(group.Employees.Where(e => e.EmployeeId == inAId)).Sales);
        Assert.Equal(4m, Assert.Single(group.Employees.Where(e => e.EmployeeId == inBId)).Sales);
        // إجمالي الفريق = مجموع موظّفيه فقط (بلا الموظّف الخارج 15000).
        Assert.Equal(10m, group.Sales);
        Assert.Equal(24000m, group.Revenue);
    }

    // ===== Test I — ثبات سلوك المسودّة: المسودّة (غير المُرسَلة) لا تدخل التجميع =====
    [Fact]
    public async Task I_DraftSubmission_ExcludedFromAggregation_Unchanged()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, gridId) = await GetSeededB2cTemplateAsync(admin);
        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (emp, empId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", ceoId);
        await AssignTemplateToEmployeeAsync(admin, templateId, empId);
        var teamId = await TestAuth.CreateTeamWithLeaderAsync(_factory, ceoId, empId);

        const string date = "2026-07-07";
        // مسودّة فقط (بلا إرسال/اعتماد).
        await CreateDraftWithRowsAsync(emp, templateId, gridId, date, B2cRow(AliasOld, 10, 40, 30, 20, 15, 6, 18000, 4));

        var report = await (await ceo.GetAsync(
            $"/api/reporting/aggregation/b2c/by-course?periodType=Daily&periodKey={date}&teamId={teamId}"))
            .ReadAsync<B2cCourseGroupedReport>();

        // لا مجموعة للدورة الموحّدة من مسودّة، ولا ظهور للموظّف.
        Assert.DoesNotContain(report!.Courses, c => c.Course == AliasCanonical || c.Course == AliasOld);
        Assert.DoesNotContain(report.Courses.SelectMany(c => c.Employees), e => e.EmployeeId == empId);
    }

    // ===== Test J — عدم إعادة إنشاء الاسم القديم ولا صفّ ثالث عند تكرار الـSeeder (idempotency) =====
    [Fact]
    public async Task J_CourseSeeder_Idempotent_NoResurrection_NoThirdCanonicalRow()
    {
        var canonicalKey = CourseNamePolicy.NormalizeForGrouping(AliasCanonical);

        async Task<(int diplomaGroupCount, int oldNameExactCount)> SnapshotAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var names = await db.Courses.Select(c => c.NameAr).ToListAsync();
            var diplomaGroup = names.Count(n => CourseNamePolicy.NormalizeForGrouping(n) == canonicalKey);
            var oldExact = names.Count(n => n == AliasOld);
            return (diplomaGroup, oldExact);
        }

        var before = await SnapshotAsync();
        // تشغيل الـSeeder مرّتين إضافيّتين — يجب أن يكون idempotent تمامًا.
        await CourseSeeder.SeedAsync(_factory.Services);
        await CourseSeeder.SeedAsync(_factory.Services);
        var after = await SnapshotAsync();

        // لا صفّ جديد في مجموعة الدبلوم (لا صفّ ثالث، لا اسم موحّد مضاف بجانب الاسم القائم).
        Assert.Equal(before.diplomaGroupCount, after.diplomaGroupCount);
        // لا إعادة إنشاء لـ«الدبلوم الشامل» بأيّ عدد إضافيّ.
        Assert.Equal(before.oldNameExactCount, after.oldNameExactCount);
        Assert.True(after.oldNameExactCount <= 1, "لا يجوز أن يتكرّر «الدبلوم الشامل».");
    }

    // ===== Test K — ثبات الكتالوج: مجموعة الدبلوم لا تحمل أكثر من دورة نشطة واحدة =====
    [Fact]
    public async Task K_CatalogInvariant_DiplomaGroup_AtMostOneActiveCourse()
    {
        var canonicalKey = CourseNamePolicy.NormalizeForGrouping(AliasCanonical);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var activeDiploma = (await db.Courses.Where(c => c.IsActive).Select(c => c.NameAr).ToListAsync())
            .Count(n => CourseNamePolicy.NormalizeForGrouping(n) == canonicalKey);
        Assert.True(activeDiploma <= 1,
            $"يجب ألّا تتجاوز الدورة الموحّدة النشطة صفًّا واحدًا (وُجد {activeDiploma}).");
    }

    // ===== Test L — ثبات التقارير التاريخية: التوحيد وقت القراءة لا يعيد كتابة خلايا TableGrid =====
    [Fact]
    public async Task L_HistoricalImmutability_AggregationDoesNotRewriteStoredGridCells()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var (templateId, gridId) = await GetSeededB2cTemplateAsync(admin);
        var (ceo, ceoId) = await TestAuth.CreateUserAsync(_factory, "CEO");
        var (emp, empId) = await TestAuth.CreateUserWithJobRoleCodeAsync(_factory, "Employee", "SALES_B2C", ceoId);
        await AssignTemplateToEmployeeAsync(admin, templateId, empId);
        var teamId = await TestAuth.CreateTeamWithLeaderAsync(_factory, ceoId, empId);

        const string date = "2026-07-14";
        var submissionId = await CreateDraftWithRowsAsync(emp, templateId, gridId, date,
            B2cRow(AliasOld, 10, 40, 30, 20, 15, 6, 18000, 4));
        await SubmitAndApproveAsync(emp, ceo, submissionId);

        async Task<string?> ReadStoredGridJsonAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await db.SubmissionFieldValues
                .Where(v => v.ReportSubmissionId == submissionId && v.TemplateFieldId == gridId)
                .Select(v => v.ValueJson)
                .SingleAsync();
        }

        var storedBefore = await ReadStoredGridJsonAsync();
        Assert.NotNull(storedBefore);
        Assert.Contains(AliasOld, storedBefore!);              // الخليّة تحمل الاسم القديم الأصليّ.

        // تشغيل التجميع (توحيد وقت القراءة ⇒ يُظهِر الاسم الموحّد للعرض).
        var report = await (await ceo.GetAsync(
            $"/api/reporting/aggregation/b2c/by-course?periodType=Daily&periodKey={date}&teamId={teamId}"))
            .ReadAsync<B2cCourseGroupedReport>();
        Assert.Equal(AliasCanonical, CanonicalGroup(report!).Course);

        // بعد التجميع: خليّة التقرير المخزَّنة لم تتغيّر إطلاقًا (Strategy B — دليل تاريخيّ ثابت).
        var storedAfter = await ReadStoredGridJsonAsync();
        Assert.Equal(storedBefore, storedAfter);
        Assert.Contains(AliasOld, storedAfter!);
        Assert.DoesNotContain(AliasCanonical, storedAfter!);   // لم يُكتَب الاسم الموحّد في الخليّة.
    }
}
