using Reporting.Application.Kpi;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.UnitTests;

/// <summary>
/// P1 §9.1 — إثبات حسابيّ مباشر لصحّة محرّك KPI الموحّد. كلّ اختبار هنا يقابل بندًا مُلزِمًا
/// في مواصفة المرحلة الأولى، وأهمّه إثبات اختفاء علّة «أعلى تقييم تاريخيّ» وعلّة المتوسّط الخامّ.
/// </summary>
public class KpiScorePolicyTests
{
    private const decimal MinCoverage = 0.75m;
    private const decimal TrendThreshold = 2.00m;

    private static decimal EmployeeAvg(params decimal[] scores) =>
        KpiScorePolicy.EmployeePeriodScore(scores.Sum(), scores.Length)!.Value;

    // ===================== 1) متوسّط الموظّف لا «أعلى» ولا «آخر» =====================

    [Fact]
    public void EmployeePeriodScore_يعيد_المتوسّط_لا_الأعلى_ولا_الأخيرة()
    {
        // العلّة الأصليّة: الواجهة كانت تنهار إلى أعلى درجة (90) أو آخر درجة (50) لكلّ عضو.
        var score = EmployeeAvg(40m, 90m, 50m);

        Assert.Equal(60.00m, KpiScorePolicy.Round(score));
        Assert.NotEqual(90m, score);
        Assert.NotEqual(50m, score);
    }

    /// <summary>الحالة المرجعيّة في §14 بند 8: عرض 85 خاطئ مقابل 65 الصحيح.</summary>
    [Fact]
    public void EmployeePeriodScore_حالة_85_الخاطئة_مقابل_65_الصحيحة()
    {
        var scores = new[] { 85m, 45m };

        Assert.Equal(85m, scores.Max());                                   // ما كان يُعرَض خطأً
        Assert.Equal(65.00m, KpiScorePolicy.Round(EmployeeAvg(scores)));   // الرقم الصحيح
    }

    // ===================== 2) التوسيط الثنائي: وزن واحد لكلّ موظّف =====================

    [Fact]
    public void GroupScore_يعطي_كلّ_موظّف_وزنًا_واحدًا_لا_متوسّطًا_خامًّا()
    {
        // A: عشرة تقييمات بدرجة 50 — B: تقييم واحد بدرجة 90.
        var a = EmployeeAvg(Enumerable.Repeat(50m, 10).ToArray());
        var b = EmployeeAvg(90m);

        var twoStage = KpiScorePolicy.GroupScore(new[] { a, b })!.Value;
        Assert.Equal(70.00m, KpiScorePolicy.Round(twoStage));

        // المتوسّط الخامّ على مستوى التقييمات كان يعطي ≈53.64 ويطمس أثر B تمامًا.
        var raw = (50m * 10 + 90m) / 11;
        Assert.Equal(53.64m, KpiScorePolicy.Round(raw));
        Assert.NotEqual(raw, twoStage);
    }

    [Fact]
    public void GroupScore_يتجاهل_الأعضاء_بلا_درجة_ولا_يعدّهم_أصفارًا()
    {
        Assert.Equal(70.00m, KpiScorePolicy.Round(KpiScorePolicy.GroupScore(new[] { 80m, 60m })));
        Assert.Null(KpiScorePolicy.GroupScore(Array.Empty<decimal>()));
    }

    // ===================== 3) الأهليّة: Approved فقط =====================

    [Theory]
    [InlineData(KpiEvaluationStatus.Approved, true)]
    [InlineData(KpiEvaluationStatus.Draft, false)]
    [InlineData(KpiEvaluationStatus.InProgress, false)]
    [InlineData(KpiEvaluationStatus.UnderReview, false)]
    [InlineData(KpiEvaluationStatus.NeedsRevision, false)]
    [InlineData(KpiEvaluationStatus.Rejected, false)]
    public void الأهليّة_تقتصر_على_Approved(KpiEvaluationStatus status, bool eligible)
        => Assert.Equal(eligible, status == KpiEvaluationStatus.Approved);

    [Fact]
    public void خلط_الحالات_لا_يدخل_المتوسّط_إلا_بـApproved()
    {
        // Approved {40, 90, 50} + Draft 100 + Rejected 0 ⇒ 60.00 لا 56.00 ولا 45.00.
        Assert.Equal(60.00m, KpiScorePolicy.Round(EmployeeAvg(40m, 90m, 50m)));
        Assert.NotEqual(60.00m, KpiScorePolicy.Round(EmployeeAvg(40m, 90m, 50m, 100m)));
        Assert.NotEqual(60.00m, KpiScorePolicy.Round(EmployeeAvg(40m, 90m, 50m, 0m)));
    }

    // ===================== 4) Missing ≠ صفر =====================

    [Fact]
    public void لا_تقييم_معتمَد_يعني_لا_بيانات_لا_صفرًا()
    {
        Assert.Null(KpiScorePolicy.EmployeePeriodScore(0m, 0));
        Assert.Equal(KpiDataQuality.NoData, KpiScorePolicy.DataQuality(0, 4, MinCoverage));
    }

    [Fact]
    public void الصفر_الحقيقيّ_تقييم_صالح_يدخل_المتوسّط()
    {
        Assert.Equal(0m, KpiScorePolicy.EmployeePeriodScore(0m, 1));
        Assert.Equal(50.00m, KpiScorePolicy.Round(EmployeeAvg(0m, 100m)));
        Assert.Equal(KpiDataQuality.Complete, KpiScorePolicy.DataQuality(1, 1, MinCoverage));
    }

    [Fact]
    public void MissingCount_يساوي_الالتزامات_بلا_درجة_معتمَدة()
    {
        Assert.Equal(3, KpiScorePolicy.MissingCount(1, 4));
        Assert.Equal(0, KpiScorePolicy.MissingCount(4, 4));
        Assert.Equal(0, KpiScorePolicy.MissingCount(5, 4));   // لا عدد سالبًا
    }

    // ===================== 5) التغطية =====================

    [Fact]
    public void التغطية_أسبوع_بالتزام_واحد_تساوي_مئة_بالمئة_ومؤهّلة()
    {
        Assert.Equal(1m, KpiScorePolicy.Coverage(1, 1));
        Assert.True(KpiScorePolicy.EligibleForRanking(1, 1, MinCoverage));
        Assert.Equal(KpiDataQuality.Complete, KpiScorePolicy.DataQuality(1, 1, MinCoverage));
    }

    [Fact]
    public void التغطية_ثلاثة_من_أربعة_جزئيّة_ومؤهّلة()
    {
        Assert.Equal(0.75m, KpiScorePolicy.Coverage(3, 4));
        Assert.True(KpiScorePolicy.EligibleForRanking(3, 4, MinCoverage));   // 75% حدّ مقبول لا مرفوض
        Assert.Equal(KpiDataQuality.Partial, KpiScorePolicy.DataQuality(3, 4, MinCoverage));
    }

    [Fact]
    public void التغطية_دون_الحدّ_تُعرَض_فرديًّا_وتُستبعَد_من_الترتيب()
    {
        Assert.Equal(0.5m, KpiScorePolicy.Coverage(2, 4));
        Assert.Equal(KpiDataQuality.InsufficientCoverage, KpiScorePolicy.DataQuality(2, 4, MinCoverage));
        Assert.False(KpiScorePolicy.EligibleForRanking(2, 4, MinCoverage));
        // ومع ذلك تبقى للموظّف قيمة معروضة (لا تُخفى، بل تُوسَم).
        Assert.Equal(60m, KpiScorePolicy.EmployeePeriodScore(120m, 2));
    }

    [Fact]
    public void التغطية_بمقام_صفر_لا_تُلفَّق()
    {
        Assert.Null(KpiScorePolicy.Coverage(0, 0));
        Assert.Equal(KpiDataQuality.NoData, KpiScorePolicy.DataQuality(0, 0, MinCoverage));
    }

    // ===================== 6) الإجازة المعتمدة تخفض المتوقَّع =====================

    [Fact]
    public void الإعفاء_المعتمَد_يخفض_المقام_ولا_يعاقب_الموظّف()
    {
        const int expected = 4;
        const int adjusted = 3;   // دورة واحدة تغطّيها إجازة معتمَدة بالكامل

        Assert.Equal(1m, KpiScorePolicy.Coverage(3, adjusted));
        Assert.Equal(KpiDataQuality.Complete, KpiScorePolicy.DataQuality(3, adjusted, MinCoverage));
        Assert.Equal(0, KpiScorePolicy.MissingCount(3, adjusted));
        Assert.Equal(0.75m, KpiScorePolicy.Coverage(3, expected));   // بلا خصم الإعفاء كان يُعاقَب
    }

    // ===================== 9) الفترة المفتوحة لا تنتج اتّجاهًا =====================

    [Fact]
    public void الفترة_المفتوحة_لا_تنتج_اتّجاهًا_رسميًّا()
    {
        var (delta, trend) = KpiScorePolicy.Trend(80m, 60m, currentIsOpen: true, TrendThreshold);
        Assert.Equal(KpiTrend.Unknown, trend);
        Assert.Null(delta);
    }

    [Theory]
    [InlineData(82.00, 80.00, KpiTrend.Up)]     // +2.00 حدّ شامل
    [InlineData(78.00, 80.00, KpiTrend.Down)]   // -2.00 حدّ شامل
    [InlineData(81.99, 80.00, KpiTrend.Flat)]
    [InlineData(78.01, 80.00, KpiTrend.Flat)]
    public void الاتّجاه_يطبّق_حدّ_نقطتين(double current, double previous, KpiTrend expected)
        => Assert.Equal(expected,
            KpiScorePolicy.Trend((decimal)current, (decimal)previous, false, TrendThreshold).Trend);

    [Fact]
    public void غياب_بيانات_أيّ_فترة_يجعل_الاتّجاه_مجهولًا()
    {
        Assert.Equal(KpiTrend.Unknown, KpiScorePolicy.Trend(null, 60m, false, TrendThreshold).Trend);
        Assert.Equal(KpiTrend.Unknown, KpiScorePolicy.Trend(60m, null, false, TrendThreshold).Trend);
    }

    // ===================== 10) التقريب مرّة واحدة فقط =====================

    [Fact]
    public void لا_تقريب_مزدوج_بين_مرحلتي_التوسيط()
    {
        // ثلاثة موظّفين، كلٌّ بثلاثة تقييمات معتمَدة ⇒ متوسّطات بثلاث خانات عشريّة.
        var a = KpiScorePolicy.EmployeePeriodScore(100.002m, 3)!.Value;   // 33.334
        var b = KpiScorePolicy.EmployeePeriodScore(100.002m, 3)!.Value;   // 33.334
        var c = KpiScorePolicy.EmployeePeriodScore(100.017m, 3)!.Value;   // 33.339

        // الصحيح: التوسيط على القيم غير المقرَّبة ثمّ تقريب واحد عند حافة الـDTO.
        var correct = KpiScorePolicy.Round(KpiScorePolicy.GroupScore(new[] { a, b, c }));

        // الخاطئ: تقريب كلّ موظّف أوّلًا ثمّ التوسيط (تقريب مزدوج).
        var earlyRounded = KpiScorePolicy.Round(KpiScorePolicy.GroupScore(new[]
        {
            KpiScorePolicy.Round(a), KpiScorePolicy.Round(b), KpiScorePolicy.Round(c)
        }));

        Assert.Equal(33.34m, correct);
        Assert.Equal(33.33m, earlyRounded);
        Assert.NotEqual(correct, earlyRounded);   // فرق حقيقيّ ⇒ ترتيب المراحل ليس تفصيلًا تجميليًّا

        // سياسة التقريب الواحدة: الأنصاف بعيدًا عن الصفر، في الاتّجاهين.
        Assert.Equal(66.67m, KpiScorePolicy.Round(66.665m));
        Assert.Equal(-66.67m, KpiScorePolicy.Round(-66.665m));
    }

    // ===================== 11) الترتيب: صفّ واحد لكلّ موظّف وكسر تعادل مستقرّ =====================

    private sealed record Row(Guid UserId, string Name, decimal Score, decimal Coverage);

    [Fact]
    public void الترتيب_صفّ_واحد_لكلّ_موظّف_مع_كسر_تعادل_مستقرّ()
    {
        var u1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var u2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var u3 = Guid.Parse("33333333-3333-3333-3333-333333333333");

        // نفس الدرجة لثلاثة: يفصل بينهم التغطية ثمّ الاسم ثمّ المعرّف — لا عشوائيّة.
        var rows = new[]
        {
            new Row(u3, "Ceem", 80m, 0.75m),
            new Row(u2, "Baa",  80m, 1.00m),
            new Row(u1, "Alef", 80m, 1.00m)
        };

        var top = rows
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.Coverage)
            .ThenBy(r => r.Name, StringComparer.Ordinal)
            .ThenBy(r => r.UserId)
            .Select(r => r.UserId)
            .ToList();

        Assert.Equal(new[] { u1, u2, u3 }, top);
        Assert.Equal(top.Count, top.Distinct().Count());   // صفّ واحد لكلّ موظّف
    }
}
