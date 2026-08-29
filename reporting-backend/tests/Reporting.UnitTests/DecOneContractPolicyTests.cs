using Reporting.Application.Kpi;
using Reporting.Application.Periods;
using Reporting.Application.Common;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.UnitTests;

/// <summary>
/// R5 — إثبات حسابيّ مباشر لبنود عقد المنتج المعتمَد <c>DEC-01</c> (الثمانية عشر بندًا).
/// كلّ اختبار هنا مربوط برقم بند صريح في العقد، ولا يُعدَّل إلّا بتعديل العقد نفسه.
/// النطاق هنا **سياسات خالصة** بلا قاعدة بيانات؛ إثبات النطاق والصلاحيّات في اختبارات التكامل.
/// </summary>
public class DecOneContractPolicyTests
{
    /// <summary>DEC-01/13 — الحدّ الأدنى المعتمَد لاعتماد النتيجة الربعيّة.</summary>
    private const decimal MinCoverage = 0.80m;

    private sealed class FixedClock : ISystemClock
    {
        public FixedClock(DateTimeOffset now) => UtcNow = now;
        public DateTimeOffset UtcNow { get; }
    }

    private static CanonicalPeriodService PeriodsAt(int year, int month, int day) =>
        new(new FixedClock(new DateTimeOffset(year, month, day, 9, 0, 0, TimeSpan.FromHours(3))));

    // ===================== البند 15 — المثال الحاكم (مرجع القبول الأوّل) =====================

    /// <summary>
    /// DEC-01/15 — موظّف أكمل تقييمًا واحدًا ممتازًا من تسعة متوقَّعة:
    /// <c>Score = 100</c> · <c>Coverage = 11.11%</c> · <c>Missing = 8</c> · <c>Insufficient Coverage</c>،
    /// ولا تُعتمد له نتيجة ربعيّة نهائيّة. هذا الاختبار هو **الترجمة الحرفيّة** للمثال، لا تقريبٌ له.
    /// </summary>
    [Fact]
    public void المثال_الحاكم_تقييم_واحد_من_تسعة_ينتج_درجة_مئة_وتغطية_11_11_ونقصًا_ثمانية()
    {
        const int completed = 1;
        const int adjustedExpected = 9;

        var score = KpiScorePolicy.EmployeePeriodScore(100m, completed);

        Assert.Equal(100m, score);
        Assert.Equal(11.11m, KpiScorePolicy.CoveragePercent(completed, adjustedExpected));
        Assert.Equal(8, KpiScorePolicy.MissingCount(completed, adjustedExpected));

        // الفترة مقفلة ⇒ الحالة النهائيّة «ناقص التغطية» لا «قيد الاستكمال».
        Assert.Equal(
            KpiJourneyState.InsufficientCoverage,
            KpiScorePolicy.JourneyState(true, adjustedExpected, adjustedExpected, completed, false, MinCoverage));

        // الدرجة موجودة لكنّها **مؤقّتة** ولا تدخل الترتيب ولا المتوسّط الرسميّ (DEC-01/11 + 14).
        Assert.True(KpiScorePolicy.IsProvisional(score, completed, adjustedExpected, MinCoverage));
        Assert.False(KpiScorePolicy.EligibleForRanking(completed, adjustedExpected, MinCoverage));
    }

    /// <summary>
    /// DEC-01/16 — الموظّف ناقص التغطية **لا يدخل** المتوسّط الرسميّ للمجموعة.
    /// لو دخل بدرجته المؤقّتة (100) لرفع متوسّط فريقٍ حقيقيّ من 60 إلى 80 — وهذا التزييف ما يمنعه العقد.
    /// </summary>
    [Fact]
    public void المتوسّط_الرسميّ_يستبعد_ناقص_التغطية_ولا_يرفع_نتيجة_الفريق()
    {
        var qualifiedA = KpiScorePolicy.EmployeePeriodScore(120m, 2)!.Value; // 60 بتغطية كاملة
        var provisional = KpiScorePolicy.EmployeePeriodScore(100m, 1)!.Value; // 100 بتغطية 11.11%

        var official = KpiScorePolicy.GroupScore(new[] { qualifiedA });
        var contaminated = KpiScorePolicy.GroupScore(new[] { qualifiedA, provisional });

        Assert.Equal(60.00m, KpiScorePolicy.Round(official!.Value));
        Assert.Equal(80.00m, KpiScorePolicy.Round(contaminated!.Value));
        Assert.NotEqual(contaminated, official);
    }

    // ===================== البند 1 — الربع الجاري تلقائيًّا بتوقيت الرياض =====================

    /// <summary>DEC-01/1 — في أغسطس 2026 يكون الافتراضيّ <c>2026-Q3</c> بلا اختيار من المستخدم.</summary>
    [Fact]
    public void CurrentQuarter_في_أغسطس_2026_يعيد_الربع_الثالث_مفتوحًا()
    {
        var period = PeriodsAt(2026, 8, 30).CurrentQuarter();

        Assert.Equal("2026-Q3", period.Key);
        Assert.Equal(PeriodKinds.Quarter, period.Type);
        Assert.Equal(new DateOnly(2026, 7, 1), period.Start);
        Assert.Equal(new DateOnly(2026, 9, 30), period.End);
        Assert.True(period.IsOpen);
    }

    /// <summary>
    /// DEC-01/1 — المرجع هو «اليوم في الرياض» لا UTC: لحظة 2026-04-01T00:30+03:00 هي
    /// 2026-03-31T21:30Z؛ الاشتقاق بـUTC كان سيعطي الربع الأوّل خطأً.
    /// </summary>
    [Fact]
    public void CurrentQuarter_يعتمد_يوم_الرياض_لا_UTC_عند_حدّ_الربع()
    {
        var riyadhMidnight = new DateTimeOffset(2026, 4, 1, 0, 30, 0, TimeSpan.FromHours(3));
        var period = new CanonicalPeriodService(new FixedClock(riyadhMidnight)).CurrentQuarter();

        Assert.Equal("2026-Q2", period.Key);
        Assert.Equal(3, riyadhMidnight.UtcDateTime.Month); // إثبات أنّ UTC ما زال في مارس
    }

    /// <summary>DEC-01/2 — الربع الجاري متاح كنوع فترة صريح، فلا تحتاج الواجهة إلى اشتقاقه.</summary>
    [Fact]
    public void Resolve_بنوع_CurrentQuarter_يطابق_CurrentQuarter_تمامًا()
    {
        var svc = PeriodsAt(2026, 8, 30);

        var viaResolve = svc.Resolve(new PeriodRequest(PeriodKinds.CurrentQuarter, null, null, null));
        var direct = svc.CurrentQuarter();

        Assert.True(viaResolve.Succeeded);
        Assert.Equal(direct.Key, viaResolve.Value!.Key);
        Assert.Equal(direct.Start, viaResolve.Value.Start);
        Assert.Equal(direct.End, viaResolve.Value.End);
    }

    /// <summary>
    /// DEC-01 «قواعد تنفيذ حاكمة» — التنقّل إلى ربع تاريخيّ متاح، والربع الجاري يبقى الافتراضيّ.
    /// الربع المقفل ليس مفتوحًا ⇒ نتيجته نهائيّة لا جارية.
    /// </summary>
    [Fact]
    public void التنقّل_إلى_ربع_تاريخيّ_متاح_ويعيد_فترة_مقفلة()
    {
        var resolved = PeriodsAt(2026, 8, 30).Resolve(new PeriodRequest(PeriodKinds.Quarter, "2025-Q4", null, null));

        Assert.True(resolved.Succeeded);
        Assert.Equal("2025-Q4", resolved.Value!.Key);
        Assert.False(resolved.Value.IsOpen);
    }

    // ===================== البند 4 — النافذة الربعيّة تحتوي دورات أصغر =====================

    /// <summary>
    /// DEC-01/4 — نافذة العرض ربعيّة حتّى لو كانت التقييمات المكوِّنة أسبوعيّة:
    /// الربع الواحد يحوي 12–14 أسبوع عمل، وكلّها داخل حدوده.
    /// </summary>
    [Fact]
    public void الربع_يحوي_أسابيعه_المكوِّنة_داخل_حدوده()
    {
        var svc = PeriodsAt(2026, 8, 30);
        var quarter = svc.CurrentQuarter();

        var weeks = svc.WeekKeysWithin(quarter);

        Assert.InRange(weeks.Count, 12, 14);
        Assert.Equal(weeks.Count, weeks.Distinct().Count());
    }

    // ===================== البند 9 — «مكتمل» = المعتمَد فقط =====================

    /// <summary>DEC-01/9 — المسودة وما دون الاعتماد لا تُحتسَب مكتملة مهما بلغت درجتها.</summary>
    [Theory]
    [InlineData(KpiEvaluationStatus.Approved, true)]
    [InlineData(KpiEvaluationStatus.Closed, true)]
    [InlineData(KpiEvaluationStatus.Draft, false)]
    [InlineData(KpiEvaluationStatus.Submitted, false)]
    [InlineData(KpiEvaluationStatus.Rejected, false)]
    public void المكتمل_هو_المعتمَد_أو_المقفل_فقط(KpiEvaluationStatus status, bool expected)
    {
        Assert.Equal(expected, KpiScorePolicy.IsCompleted(status));
        Assert.Equal(expected, KpiScorePolicy.CompletedStatuses.Contains(status));
    }

    // ===================== البند 10 — «مفقود» ليس «صفرًا» =====================

    /// <summary>
    /// DEC-01/10 — الفترة المفقودة تظهر مفقودة ولا تتحوّل إلى درجة صفر:
    /// المفقود يظهر في <c>Missing</c> بينما الدرجة تبقى متوسّط المكتمل وحده.
    /// </summary>
    [Fact]
    public void المفقود_يُعَدّ_نقصًا_ولا_يُحوَّل_إلى_صفر_في_الدرجة()
    {
        var score = KpiScorePolicy.EmployeePeriodScore(80m + 90m, 2)!.Value;

        Assert.Equal(85.00m, KpiScorePolicy.Round(score));
        Assert.Equal(2, KpiScorePolicy.MissingCount(2, 4));

        // لو حُوِّل المفقودان إلى صفرين لانهارت الدرجة إلى 42.5 — وهذا ما يمنعه البند.
        Assert.NotEqual(42.50m, KpiScorePolicy.Round(score));
    }

    /// <summary>DEC-01/10 — غياب أيّ تقييم يعطي <c>null</c> لا <c>0</c>.</summary>
    [Fact]
    public void غياب_التقييمات_يعطي_لا_قيمة_لا_صفرًا()
    {
        Assert.Null(KpiScorePolicy.EmployeePeriodScore(0m, 0));
        Assert.Null(KpiScorePolicy.GroupScore(Array.Empty<decimal>()));
        Assert.Null(KpiScorePolicy.CoveragePercent(0, 0));
    }

    // ===================== البند 11 — الدرجة مستقلّة عن التغطية =====================

    /// <summary>
    /// DEC-01/11 — الدرجة تُحسَب من المكتمل المعتمَد وحده وتُعرَض مستقلّة عن نسبة التغطية:
    /// نفس الدرجة (100) مع تغطيتين مختلفتين تمامًا.
    /// </summary>
    [Fact]
    public void الدرجة_لا_تتأثّر_بالتغطية_وتُعرَض_مستقلّة_عنها()
    {
        var lowCoverage = KpiScorePolicy.EmployeePeriodScore(100m, 1);
        var fullCoverage = KpiScorePolicy.EmployeePeriodScore(100m * 9, 9);

        Assert.Equal(lowCoverage, fullCoverage);
        Assert.Equal(11.11m, KpiScorePolicy.CoveragePercent(1, 9));
        Assert.Equal(100.00m, KpiScorePolicy.CoveragePercent(9, 9));
    }

    // ===================== البند 12 — معادلة التغطية =====================

    /// <summary>DEC-01/12 — <c>Coverage = Completed ÷ AdjustedExpected × 100</c> مقرَّبة إلى منزلتين.</summary>
    [Theory]
    [InlineData(1, 9, 11.11)]
    [InlineData(2, 3, 66.67)]
    [InlineData(7, 9, 77.78)]
    [InlineData(9, 9, 100.00)]
    [InlineData(0, 5, 0.00)]
    public void معادلة_التغطية_المئويّة(int completed, int adjustedExpected, double expected)
        => Assert.Equal((decimal)expected, KpiScorePolicy.CoveragePercent(completed, adjustedExpected));

    /// <summary>DEC-01/12 — التغطية تُحسَب على <c>AdjustedExpected</c> لا على <c>Expected</c> الخامّ.</summary>
    [Fact]
    public void التغطية_تُحسَب_على_المتوقَّع_المعدَّل_لا_الخامّ()
    {
        // متوقَّع خامّ 13 أسبوعًا، أربعة منها إجازة معتمَدة ⇒ المعدَّل 9.
        Assert.Equal(66.67m, KpiScorePolicy.CoveragePercent(6, 9));
        Assert.Equal(46.15m, KpiScorePolicy.CoveragePercent(6, 13));
    }

    // ===================== البند 13 — عتبة 80% =====================

    /// <summary>DEC-01/13 — الحدّ الأدنى المعتمَد 80%: 79.99% غير مؤهّل و80% مؤهّل.</summary>
    [Theory]
    [InlineData(7, 9, false)]   // 77.78%
    [InlineData(4, 5, true)]    // 80.00% — الحدّ نفسه مؤهّل
    [InlineData(8, 10, true)]   // 80.00%
    [InlineData(79, 100, false)]
    [InlineData(80, 100, true)]
    public void عتبة_الثمانين_بالمئة_هي_الفاصل(int completed, int adjustedExpected, bool eligible)
        => Assert.Equal(eligible, KpiScorePolicy.EligibleForRanking(completed, adjustedExpected, MinCoverage));

    /// <summary>DEC-01/13 — العتبة الافتراضيّة في الإعدادات هي 0.80 لا 0.75.</summary>
    [Fact]
    public void العتبة_الافتراضيّة_في_الإعدادات_ثمانون_بالمئة()
        => Assert.Equal(0.80m, new KpiFeatureOptions().MinimumCoverageForRanking);

    /// <summary>
    /// DEC-01/13+14 — ما كان مؤهّلًا تحت عتبة 0.75 السابقة (77.78%) صار غير مؤهّل تحت العقد.
    /// هذا تغيير سلوك **مقصود ومعتمَد**، ومثبَت هنا كي لا يُعاد بالخطأ.
    /// </summary>
    [Fact]
    public void رفع_العتبة_من_75_إلى_80_يغيّر_الأهليّة_عند_77_78()
    {
        Assert.True(KpiScorePolicy.EligibleForRanking(7, 9, 0.75m));
        Assert.False(KpiScorePolicy.EligibleForRanking(7, 9, MinCoverage));
    }

    // ===================== البند 14 — الدرجة المؤقّتة =====================

    /// <summary>DEC-01/14 — الدرجة «مؤقّتة» متى وُجدت وكانت التغطية دون العتبة؛ ونهائيّة عند بلوغها.</summary>
    [Theory]
    [InlineData(1, 9, true)]
    [InlineData(7, 9, true)]
    [InlineData(8, 10, false)]
    [InlineData(9, 9, false)]
    public void الدرجة_مؤقّتة_دون_العتبة_ونهائيّة_عندها(int completed, int adjustedExpected, bool provisional)
        => Assert.Equal(provisional, KpiScorePolicy.IsProvisional(75m, completed, adjustedExpected, MinCoverage));

    /// <summary>DEC-01/14 — لا درجة ⇒ لا وصف «مؤقّتة» أصلًا (لا تُلفَّق درجة كي توصَف).</summary>
    [Fact]
    public void لا_درجة_يعني_لا_وصف_مؤقّتة()
        => Assert.False(KpiScorePolicy.IsProvisional(null, 0, 9, MinCoverage));

    // ===================== البند 18 — الحالات الستّ الصريحة =====================

    /// <summary>
    /// DEC-01/18 — مصفوفة الحالات الستّ. الترتيب مقصود: انعدام التواتر يسبق الإعفاء،
    /// والإعفاء يسبق «لم يبدأ»، وإلّا اختفى سبب انعدام المقام خلف حالة عامّة.
    /// </summary>
    [Theory]
    // (تواتر مُهيّأ، متوقَّع، معدَّل، مكتمل، الفترة مفتوحة) ⇒ الحالة
    [InlineData(false, 9, 9, 0, true, KpiJourneyState.CadenceNotConfigured)]
    [InlineData(false, 9, 9, 5, false, KpiJourneyState.CadenceNotConfigured)]
    [InlineData(true, 9, 0, 0, false, KpiJourneyState.Exempt)]
    [InlineData(true, 9, 9, 0, true, KpiJourneyState.NotStarted)]
    [InlineData(true, 9, 9, 0, false, KpiJourneyState.NotStarted)]
    [InlineData(true, 9, 9, 1, true, KpiJourneyState.InProgress)]
    [InlineData(true, 9, 9, 1, false, KpiJourneyState.InsufficientCoverage)]
    [InlineData(true, 9, 9, 8, true, KpiJourneyState.CompleteEligible)]
    [InlineData(true, 9, 9, 9, false, KpiJourneyState.CompleteEligible)]
    public void مصفوفة_حالات_الرحلة_الستّ(
        bool cadenceConfigured, int expected, int adjustedExpected,
        int completed, bool periodIsOpen, KpiJourneyState state)
        => Assert.Equal(state, KpiScorePolicy.JourneyState(
            cadenceConfigured, expected, adjustedExpected, completed, periodIsOpen, MinCoverage));

    /// <summary>
    /// DEC-01/14+18 — الربع الجاري لا يُوصَم «ناقص التغطية»، لكنّ درجته تبقى مؤقّتة وغير مؤهّلة
    /// للمتوسّط الرسميّ. الحالتان مستقلّتان: الوصف يتغيّر بانفتاح الفترة، والأثر المالي لا يتغيّر.
    /// </summary>
    [Fact]
    public void الفترة_المفتوحة_تغيّر_الوصف_لا_الأثر_على_المتوسّط_الرسميّ()
    {
        Assert.Equal(
            KpiJourneyState.InProgress,
            KpiScorePolicy.JourneyState(true, 9, 9, 2, true, MinCoverage));

        Assert.False(KpiScorePolicy.EligibleForRanking(2, 9, MinCoverage));
        Assert.True(KpiScorePolicy.IsProvisional(90m, 2, 9, MinCoverage));
    }

    /// <summary>DEC-01/18 — «مُعفى» يعني إعفاء كلّ الالتزامات لا مجرّد غياب متوقَّع أصلًا.</summary>
    [Fact]
    public void الإعفاء_يقتضي_وجود_متوقَّع_خامّ_أُسقِط_بالكامل()
    {
        Assert.Equal(
            KpiJourneyState.Exempt,
            KpiScorePolicy.JourneyState(true, 13, 0, 0, false, MinCoverage));

        // لا متوقَّع خامّ ولا معدَّل ⇒ ليست إعفاءً بل «لم يبدأ» (لا سبب إعفاء يُعرَض).
        Assert.Equal(
            KpiJourneyState.NotStarted,
            KpiScorePolicy.JourneyState(true, 0, 0, 0, false, MinCoverage));
    }

    // ===================== البند 5 — مصادر التواتر مسمّاة لا صامتة =====================

    /// <summary>
    /// DEC-01/5 — مصدر التواتر مُعلَن دائمًا، وغياب التهيئة له اسم صريح
    /// (<c>notConfigured</c>) لا سقوط صامت إلى تواتر افتراضيّ.
    /// </summary>
    [Fact]
    public void مصدر_التواتر_مُعلَن_وغيابه_مسمّى_لا_صامت()
    {
        var notConfigured = new KpiEffectiveCadence(
            Guid.NewGuid(), null, KpiCadenceSources.NotConfigured, Array.Empty<Guid>());

        Assert.Null(notConfigured.Cadence);
        Assert.Equal("notConfigured", notConfigured.Source);
        Assert.Equal(
            KpiJourneyState.CadenceNotConfigured,
            KpiScorePolicy.JourneyState(notConfigured.Cadence is not null, 9, 9, 0, true, MinCoverage));
    }

    /// <summary>DEC-01/5 — أسماء المصادر الخمسة ثابتة ومتمايزة (تُعرَض للمستخدم ولا تتغيّر ضمنًا).</summary>
    [Fact]
    public void أسماء_مصادر_التواتر_متمايزة()
    {
        var sources = new[]
        {
            KpiCadenceSources.EmployeeAssignment,
            KpiCadenceSources.TeamAssignment,
            KpiCadenceSources.JobRole,
            KpiCadenceSources.DepartmentAssignment,
            KpiCadenceSources.GeneralTemplate,
            KpiCadenceSources.NotConfigured,
            KpiCadenceSources.ExplicitRequest
        };

        Assert.Equal(sources.Length, sources.Distinct().Count());
        Assert.All(sources, s => Assert.False(string.IsNullOrWhiteSpace(s)));
    }
}
