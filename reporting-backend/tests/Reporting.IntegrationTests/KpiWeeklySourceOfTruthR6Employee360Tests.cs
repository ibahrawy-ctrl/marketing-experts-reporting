using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Common;
using Reporting.Domain.Entities.Kpi;
using Reporting.Domain.Enums;
using Reporting.Infrastructure.Persistence;
using Xunit;

namespace Reporting.IntegrationTests;

/// <summary>
/// <b>R6/§7/Consumers/2 — «Employee 360 summaries and details use Weekly Approved only».</b>
/// <para>
/// مفصولة عن <see cref="KpiWeeklySourceOfTruthR6Tests"/> لأنّ Employee 360 يعيش على مصنع المرحلة
/// الثانية وقاعدته المعزولة (<see cref="Phase2WebApplicationFactory"/>)، وخلط المصنعين في صنف واحد
/// غير ممكن. المضمون جزء من نفس الحزمة الإلزاميّة، والاسم يحمل رقم البند ليبقى الأثر مقروءًا.
/// </para>
/// <para>
/// البند يقول <b>summaries and details</b> معًا، وهذا ليس تزيّدًا: القسم التفصيليّ والملخّص التشغيليّ
/// مصدران مستقلّان في نفس الشاشة. إصلاح أحدهما وحده كان يترك المدير أمام «عدد التقييمات = 2»
/// في الأعلى و«تقييم واحد» في التفصيل تحته — تناقض داخليّ يهدم الثقة بالرقمين معًا.
/// </para>
/// </summary>
[Collection("Phase2")]
public class KpiWeeklySourceOfTruthR6Employee360Tests
{
    private readonly Phase2WebApplicationFactory _factory;

    public KpiWeeklySourceOfTruthR6Employee360Tests(Phase2WebApplicationFactory factory) => _factory = factory;

    /// <summary>
    /// يبذر قالبًا بكادنس محدَّد وإصدارًا منشورًا منه ثمّ صفَّ تقييم بحالته ومساره — مباشرةً في القاعدة.
    /// مسار الكتابة الربعيّة مُقفَل بعد R6، فتمثيل السجلّات الربعيّة <b>القائمة</b> (التي تبقى بلا حذف
    /// ولا هجرة — WS-2) لا يكون إلّا بالبذر. والغرض هنا قياس <b>القراءة</b> لا الكتابة.
    /// </summary>
    private async Task<Guid> SeedEvaluationAsync(
        Guid subjectId, KpiCadence cadence, PeriodType periodType, string periodKey,
        KpiEvaluationStatus status, decimal score)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var template = new KpiTemplate
        {
            Title = $"قالب R6-360 {Guid.NewGuid():N}",
            Cadence = cadence,
            Status = TemplateStatus.Published,
            IsActive = true
        };
        db.KpiTemplates.Add(template);
        var version = new KpiTemplateVersion
        {
            KpiTemplateId = template.Id,
            VersionNumber = 1,
            IsPublished = true,
            PublishedAtUtc = DateTime.UtcNow
        };
        db.KpiTemplateVersions.Add(version);

        var row = new KpiEvaluation
        {
            KpiTemplateVersionId = version.Id,
            SubjectUserId = subjectId,
            PeriodType = periodType,
            PeriodKey = periodKey,
            Status = status,
            TotalScore = score,
            SubmittedAtUtc = DateTime.UtcNow,
            ReviewedAtUtc = DateTime.UtcNow
        };
        db.KpiEvaluations.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    [Fact]
    public async Task مستهلك04_ملفّ_الموظّف_الشامل_ملخّصًا_وتفصيلًا_لا_يقرأ_إلّا_النبض_المعتمَد()
    {
        var (client, userId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);

        var week = TestCalendar.Cycle(1);
        var quarter = QuarterKeyOf(TestCalendar.CycleStart(week).AddDays(3)); // مرجع الثلاثاء

        var weeklyApproved = await SeedEvaluationAsync(
            userId, KpiCadence.WeeklyPulse, PeriodType.Weekly, week, KpiEvaluationStatus.Approved, 70m);
        await SeedEvaluationAsync(
            userId, KpiCadence.Quarterly, PeriodType.Quarterly, quarter, KpiEvaluationStatus.Approved, 99m);
        await SeedEvaluationAsync(
            userId, KpiCadence.WeeklyPulse, PeriodType.Weekly, TestCalendar.Cycle(2),
            KpiEvaluationStatus.Submitted, 95m);

        var res = await client.GetAsync("/api/employees/me/profile-360");
        res.EnsureSuccessStatusCode();
        var root = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.Clone();
        var sections = root.GetProperty("sections");

        // (أ) التفصيل: صفّ واحد فقط — الأسبوعيّ المعتمَد.
        var items = sections.GetProperty("kpi").GetProperty("items").EnumerateArray().ToList();
        var item = Assert.Single(items);
        Assert.Equal(weeklyApproved, item.GetProperty("evaluationId").GetGuid());
        Assert.Equal(70m, item.GetProperty("totalScore").GetDecimal());
        Assert.Equal(nameof(PeriodType.Weekly), item.GetProperty("periodType").GetString());
        Assert.Equal(nameof(KpiEvaluationStatus.Approved), item.GetProperty("status").GetString());

        // (ب) الملخّص التشغيليّ: العدّاد وآخر درجة يخضعان لنفس المصدر لا لعدّ عامّ للمعتمَد.
        var summary = sections.GetProperty("operationalSummary").GetProperty("summary");
        Assert.Equal(1, summary.GetProperty("kpiEvaluationCount").GetInt32());
        Assert.Equal(70m, summary.GetProperty("lastKpiScore").GetDecimal());
        Assert.Equal(week, summary.GetProperty("lastKpiPeriodKey").GetString());
    }

    /// <summary>
    /// الموظّف الذي لا يملك إلّا سجلّات ربعيّة إرثيّة يُعلَن قسمه <c>NoData</c> صراحةً.
    /// «لا بيانات» حكم صادق، أمّا عرض 99 من مسار مُلغى فهو نجاح أعمال زائف.
    /// </summary>
    [Fact]
    public async Task مستهلك05_من_لا_يملك_إلّا_سجلّات_ربعيّة_إرثيّة_قسمه_بلا_بيانات_لا_بدرجة_مضلّلة()
    {
        var (client, userId) = await Phase2TestAuth.CreateUserAsync(_factory, Roles.Employee);
        var quarter = QuarterKeyOf(TestCalendar.CycleStart(TestCalendar.Cycle(1)).AddDays(3));

        await SeedEvaluationAsync(
            userId, KpiCadence.Quarterly, PeriodType.Quarterly, quarter, KpiEvaluationStatus.Approved, 99m);

        var res = await client.GetAsync("/api/employees/me/profile-360");
        res.EnsureSuccessStatusCode();
        var sections = JsonDocument.Parse(await res.Content.ReadAsStringAsync())
            .RootElement.Clone().GetProperty("sections");

        var kpi = sections.GetProperty("kpi");
        Assert.Equal("NoData", kpi.GetProperty("status").GetString());
        Assert.Equal(0, kpi.GetProperty("items").GetArrayLength());

        var summary = sections.GetProperty("operationalSummary").GetProperty("summary");
        Assert.Equal(0, summary.GetProperty("kpiEvaluationCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, summary.GetProperty("lastKpiScore").ValueKind);
    }

    /// <inheritdoc cref="KpiWeeklySourceOfTruthR6Tests"/>
    private static string QuarterKeyOf(DateOnly d) =>
        FormattableString.Invariant($"{d.Year:0000}-Q{(d.Month - 1) / 3 + 1}");
}
