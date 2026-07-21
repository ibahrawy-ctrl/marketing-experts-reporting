using Reporting.Application.Common;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.UnitTests;

/// <summary>
/// PROJECT-FIRST-EXECUTION-AGGREGATION-CONTRACT-R1 (Phase 12) — اختبارات وحدة حتميّة (بلا قاعدة بيانات)
/// للعقد القياسي: تطبيع حالة المشروع (RAG ثلاثيّ)، خرائط المفاتيح الحقيقية (v5) لكل قالب تنفيذ،
/// واشتقاق مفتاح الفترة السابقة عبر واجهة التقويم (السبت→الجمعة). كلّها دوال ثابتة صرفة.
/// </summary>
public class ProjectFirstExecutionSchemaTests
{
    // ===== تطبيع حالة المشروع (Phase 4) =====

    [Theory]
    [InlineData("🟢 ممتاز", ProjectFirstExecutionSchema.StatusHealthy)]
    [InlineData("ممتازة", ProjectFirstExecutionSchema.StatusHealthy)]
    [InlineData("🟡 مستقر", ProjectFirstExecutionSchema.StatusStable)]
    [InlineData("مستقر", ProjectFirstExecutionSchema.StatusStable)]
    [InlineData("🔴 يحتاج تدخل", ProjectFirstExecutionSchema.StatusNeedsIntervention)]
    [InlineData("يحتاج تدخل عاجل", ProjectFirstExecutionSchema.StatusNeedsIntervention)]
    [InlineData("", ProjectFirstExecutionSchema.StatusUnspecified)]
    [InlineData("   ", ProjectFirstExecutionSchema.StatusUnspecified)]
    [InlineData(null, ProjectFirstExecutionSchema.StatusUnspecified)]
    [InlineData("قيمة قديمة غير معروفة", ProjectFirstExecutionSchema.StatusUnspecified)]
    public void NormalizeStatus_MapsRagKeywords(string? raw, string expected)
        => Assert.Equal(expected, ProjectFirstExecutionSchema.NormalizeStatus(raw));

    [Fact]
    public void NormalizeStatus_IsTolerantOfColorMarkAndWhitespace()
    {
        // نفس الكلمة مع/بدون رمز لونيّ ومسافات تعطي نفس القيمة الآليّة.
        Assert.Equal(
            ProjectFirstExecutionSchema.NormalizeStatus("ممتاز"),
            ProjectFirstExecutionSchema.NormalizeStatus("  🟢  ممتاز  "));
    }

    // ===== خرائط المفاتيح الحقيقية (v5) لكل قالب (Phase 5) =====

    [Theory]
    [InlineData(ProjectFirstExecutionSchema.ContentTitle)]
    [InlineData(ProjectFirstExecutionSchema.DesignTitle)]
    [InlineData(ProjectFirstExecutionSchema.VideoTitle)]
    [InlineData(ProjectFirstExecutionSchema.ModerationTitle)]
    public void MapFor_ResolvesEachExecutionTemplate(string title)
        => Assert.NotNull(ProjectFirstExecutionSchema.MapFor(title));

    [Theory]
    [InlineData("تقرير غير موجود")]
    [InlineData("")]
    [InlineData(null)]
    public void MapFor_ReturnsNullForNonExecutionTitles(string? title)
        => Assert.Null(ProjectFirstExecutionSchema.MapFor(title));

    [Fact]
    public void ContentMap_UsesRealV5ProductionKeys()
    {
        var map = ProjectFirstExecutionSchema.MapFor(ProjectFirstExecutionSchema.ContentTitle)!;
        Assert.Equal(new[] { "required_pieces" }, map.Planned);
        Assert.Equal(new[] { "delivered_pieces" }, map.Completed);
        Assert.Equal(new[] { "approved_first_time" }, map.Approved);
        Assert.Equal(new[] { "returned_once", "returned_more" }, map.Revisions);
        Assert.Equal(new[] { "late_pieces" }, map.Delayed);
        // عائلة المديرشن فارغة قصدًا في قوالب الإنتاج.
        Assert.Empty(map.MessagesIn);
        Assert.Empty(map.Responses);
        Assert.Empty(map.IssueComments);
        Assert.Empty(map.Escalations);
    }

    [Fact]
    public void DesignAndVideoMaps_UseDistinctProductionKeys()
    {
        var d = ProjectFirstExecutionSchema.MapFor(ProjectFirstExecutionSchema.DesignTitle)!;
        Assert.Equal(new[] { "requested_designs" }, d.Planned);
        Assert.Equal(new[] { "delivered_designs" }, d.Completed);
        Assert.Equal(new[] { "revised_designs" }, d.Revisions);
        Assert.Equal(new[] { "late_designs" }, d.Delayed);

        var v = ProjectFirstExecutionSchema.MapFor(ProjectFirstExecutionSchema.VideoTitle)!;
        Assert.Equal(new[] { "requested_videos" }, v.Planned);
        Assert.Equal(new[] { "delivered_videos" }, v.Completed);
        Assert.Equal(new[] { "revised_videos" }, v.Revisions);
        Assert.Equal(new[] { "late_videos" }, v.Delayed);
    }

    [Fact]
    public void ModerationMap_FillsOnlyModerationFamily_NoProductionDoubleCount()
    {
        var m = ProjectFirstExecutionSchema.MapFor(ProjectFirstExecutionSchema.ModerationTitle)!;
        // عائلة الإنتاج فارغة كي لا يزدوج TotalOutput = Completed + Responses.
        Assert.Empty(m.Planned);
        Assert.Empty(m.Completed);
        Assert.Empty(m.Approved);
        Assert.Empty(m.Revisions);
        Assert.Empty(m.Delayed);
        // عائلة المديرشن هي المصدر الوحيد.
        Assert.Equal(new[] { "incoming_messages" }, m.MessagesIn);
        Assert.Equal(new[] { "answered_messages" }, m.Responses);
        Assert.Equal(new[] { "complaints" }, m.IssueComments);
        Assert.Equal(new[] { "escalations" }, m.Escalations);
    }

    [Fact]
    public void ExecutionTemplateTitles_ContainsExactlyTheFourTemplates()
    {
        Assert.Equal(4, ProjectFirstExecutionSchema.ExecutionTemplateTitles.Length);
        Assert.Contains(ProjectFirstExecutionSchema.ContentTitle, ProjectFirstExecutionSchema.ExecutionTemplateTitles);
        Assert.Contains(ProjectFirstExecutionSchema.ModerationTitle, ProjectFirstExecutionSchema.ExecutionTemplateTitles);
    }

    // ===== اشتقاق مفتاح الفترة السابقة (Phase 5 — المقارنة الدوريّة) =====

    [Fact]
    public void PreviousPeriodKey_Weekly_ShiftsOneOperationalWeekBack()
    {
        // W27 يبدأ السبت 2026-06-27 ⇒ الأسبوع السابق يبدأ السبت 2026-06-20 = W26.
        var prev = ReportCalendarPolicy.PreviousPeriodKey(PeriodType.Weekly, "2026-W27");
        Assert.Equal("2026-W26", prev);
    }

    [Theory]
    [InlineData("2026-03", "2026-02")]
    [InlineData("2026-01", "2025-12")] // التفاف السنة
    public void PreviousPeriodKey_Monthly_HandlesYearWrap(string key, string expected)
        => Assert.Equal(expected, ReportCalendarPolicy.PreviousPeriodKey(PeriodType.Monthly, key));

    [Theory]
    [InlineData("2026-Q3", "2026-Q2")]
    [InlineData("2026-Q1", "2025-Q4")] // التفاف السنة
    public void PreviousPeriodKey_Quarterly_HandlesYearWrap(string key, string expected)
        => Assert.Equal(expected, ReportCalendarPolicy.PreviousPeriodKey(PeriodType.Quarterly, key));

    [Fact]
    public void PreviousPeriodKey_Daily_SubtractsOneDay()
        => Assert.Equal("2026-06-30", ReportCalendarPolicy.PreviousPeriodKey(PeriodType.Daily, "2026-07-01"));

    [Theory]
    [InlineData(PeriodType.Weekly, "2026-06")]     // مفتاح غير أسبوعي
    [InlineData(PeriodType.Weekly, "not-a-key")]
    [InlineData(PeriodType.Monthly, "2026-13")]    // شهر خارج المدى
    [InlineData(PeriodType.Quarterly, "2026-Q5")]  // ربع خارج المدى
    [InlineData(PeriodType.Quarterly, "2026-03")]  // ليس بصيغة الربع
    [InlineData(PeriodType.Daily, "2026-99-99")]
    [InlineData(PeriodType.Yearly, "2026")]        // نوع غير مدعوم
    [InlineData(PeriodType.Weekly, "")]
    [InlineData(PeriodType.Weekly, null)]
    public void PreviousPeriodKey_ReturnsNull_ForInvalidOrUnsupported(PeriodType type, string? key)
        => Assert.Null(ReportCalendarPolicy.PreviousPeriodKey(type, key));
}
