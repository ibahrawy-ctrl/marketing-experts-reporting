using Reporting.Application.Projects360;
using Reporting.Domain.Enums;
using Reporting.Domain.Projects360;
using Xunit;

namespace Reporting.UnitTests;

/// <summary>
/// عقد محرّك الاحتساب المركزيّ (CPW-R3 · W4 · §19) — دوالّ **نقيّة حتميّة** بلا قاعدة بيانات ولا ساعة.
///
/// <para>
/// القاعدة الحاكمة المُختبَرة في كلّ حالة: **«غير محتسَب» ≠ «صفر»**. المكوّن الغائب يُستبعَد
/// وتُعاد تسوية الأوزان على المتاح؛ ولا يُعاقَب مشروع بصفر لم يُعلنه أحد.
/// </para>
///
/// <para>الاختبارات تغطّي الحدود لا الوسط: عتبات 80/55، فجوات −10/−25، القصّ على 0..200، والتدوير بمنزلتين.</para>
/// </summary>
public class ProjectHealthPolicyTests
{
    private static readonly DateTime Stamp = new(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc);

    // ==================================================================
    // §7 — تحقيق المؤشّر
    // ==================================================================

    [Theory]
    [InlineData(50, 100, 50)]    // نصف المستهدَف
    [InlineData(100, 100, 100)]  // المستهدَف بالضبط
    [InlineData(150, 100, 150)]  // تجاوز المستهدَف — لا يُقصّ عند 100
    [InlineData(0, 100, 0)]      // صفر معلَن ≠ غير محتسَب
    public void ComputeAchievement_HigherIsBetter_UsesCurrentOverTarget(decimal current, decimal target, decimal expected)
        => Assert.Equal(expected, ProjectHealthPolicy.ComputeAchievement(current, target, ProjectKpiDirection.HigherIsBetter));

    [Theory]
    [InlineData(50, 100, 200)]   // أقلّ من المستهدَف في «الأقلّ أفضل» ⟹ تجاوز
    [InlineData(100, 100, 100)]  // المستهدَف بالضبط
    [InlineData(200, 100, 50)]   // ضِعف المستهدَف ⟹ نصف التحقيق
    public void ComputeAchievement_LowerIsBetter_UsesTargetOverCurrent(decimal current, decimal target, decimal expected)
        => Assert.Equal(expected, ProjectHealthPolicy.ComputeAchievement(current, target, ProjectKpiDirection.LowerIsBetter));

    [Fact]
    public void ComputeAchievement_ClampsToTwoHundred()
        => Assert.Equal(ProjectHealthPolicy.AchievementMax,
            ProjectHealthPolicy.ComputeAchievement(1000m, 100m, ProjectKpiDirection.HigherIsBetter));

    /// <summary>DN-04: «الأقلّ أفضل» بقيمة ≤ 0 تعني بلوغ الأفضل الممكن — لا قسمة على صفر.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ComputeAchievement_LowerIsBetter_NonPositiveCurrent_ReturnsMax(decimal current)
        => Assert.Equal(ProjectHealthPolicy.AchievementMax,
            ProjectHealthPolicy.ComputeAchievement(current, 100m, ProjectKpiDirection.LowerIsBetter));

    /// <summary>غياب القراءة أو هدف غير موجب ⟹ **غير محتسَب** لا صفر.</summary>
    [Fact]
    public void ComputeAchievement_MissingCurrentOrNonPositiveTarget_IsNotComputed()
    {
        Assert.Null(ProjectHealthPolicy.ComputeAchievement(null, 100m, ProjectKpiDirection.HigherIsBetter));
        Assert.Null(ProjectHealthPolicy.ComputeAchievement(50m, 0m, ProjectKpiDirection.HigherIsBetter));
        Assert.Null(ProjectHealthPolicy.ComputeAchievement(50m, -10m, ProjectKpiDirection.HigherIsBetter));
    }

    /// <summary>DN-05: منزلتان عشريّتان بـAwayFromZero (اتّساقًا مع numeric(9,2)).</summary>
    [Fact]
    public void ComputeAchievement_RoundsToTwoDecimals()
        => Assert.Equal(33.33m, ProjectHealthPolicy.ComputeAchievement(1m, 3m, ProjectKpiDirection.HigherIsBetter));

    [Fact]
    public void ComputeVariance_IsAchievementMinusHundred()
    {
        Assert.Equal(20m, ProjectHealthPolicy.ComputeVariance(120m));
        Assert.Equal(-40m, ProjectHealthPolicy.ComputeVariance(60m));
        Assert.Null(ProjectHealthPolicy.ComputeVariance(null));
    }

    /// <summary>الاتّجاه بهامش ε = ±2: داخل الهامش «مستقرّ» لا «صاعد/هابط».</summary>
    [Theory]
    [InlineData(100, 98, ProjectKpiTrend.Flat)]
    [InlineData(100, 102, ProjectKpiTrend.Flat)]
    [InlineData(103, 100, ProjectKpiTrend.Up)]
    [InlineData(97, 100, ProjectKpiTrend.Down)]
    public void ComputeTrend_RespectsEpsilonBand(decimal latest, decimal previous, ProjectKpiTrend expected)
        => Assert.Equal(expected, ProjectHealthPolicy.ComputeTrend(latest, previous));

    [Fact]
    public void ComputeTrend_WithoutBaselineComparison_IsUnknown()
    {
        Assert.Equal(ProjectKpiTrend.Unknown, ProjectHealthPolicy.ComputeTrend(100m, null));
        Assert.Equal(ProjectKpiTrend.Unknown, ProjectHealthPolicy.ComputeTrend(null, 100m));
    }

    // ==================================================================
    // §8 — التجميع الموزون
    // ==================================================================

    [Fact]
    public void ComputeWeightedScore_AppliesDeclaredWeights()
    {
        var result = ProjectHealthPolicy.ComputeWeightedScore(new[]
        {
            new ProjectHealthPolicy.WeightedAchievement(3m, 100m),
            new ProjectHealthPolicy.WeightedAchievement(1m, 60m),
        });

        Assert.Equal(90m, result.Score);          // (3×100 + 1×60) / 4
        Assert.False(result.AllWeightsZero);
        Assert.Equal(2, result.ComputedCount);
        Assert.Equal(2, result.TotalCount);
    }

    /// <summary>مجموع أوزان صفر ⟹ أوزان متساوية + علَم مفسِّر، لا قسمة على صفر ولا نتيجة صفريّة.</summary>
    [Fact]
    public void ComputeWeightedScore_AllWeightsZero_FallsBackToEqualWeights()
    {
        var result = ProjectHealthPolicy.ComputeWeightedScore(new[]
        {
            new ProjectHealthPolicy.WeightedAchievement(0m, 100m),
            new ProjectHealthPolicy.WeightedAchievement(0m, 50m),
        });

        Assert.Equal(75m, result.Score);
        Assert.True(result.AllWeightsZero);
    }

    /// <summary>العناصر غير المحتسَبة تُستبعَد من البسط **والمقام** معًا.</summary>
    [Fact]
    public void ComputeWeightedScore_ExcludesUncomputedItemsFromBothSides()
    {
        var result = ProjectHealthPolicy.ComputeWeightedScore(new[]
        {
            new ProjectHealthPolicy.WeightedAchievement(1m, 80m),
            new ProjectHealthPolicy.WeightedAchievement(9m, null),
        });

        Assert.Equal(80m, result.Score);
        Assert.Equal(1, result.ComputedCount);
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public void ComputeWeightedScore_NoItemsOrNoneComputed_IsNotComputed()
    {
        Assert.Null(ProjectHealthPolicy.ComputeWeightedScore(Array.Empty<ProjectHealthPolicy.WeightedAchievement>()).Score);
        Assert.Null(ProjectHealthPolicy.ComputeWeightedScore(new[]
        {
            new ProjectHealthPolicy.WeightedAchievement(5m, null),
        }).Score);
    }

    // ==================================================================
    // §8 — تقدّم الأهداف (مشتقّ لا مخزَّن — DN-02)
    // ==================================================================

    [Fact]
    public void ComputeObjectiveProgress_DerivesScoreAndNormalizesWeights()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var result = ProjectHealthPolicy.ComputeObjectiveProgress(new[]
        {
            new ProjectHealthPolicy.ObjectiveInput(a, ProjectObjectiveStatus.InProgress, 30m, new[]
            {
                new ProjectHealthPolicy.WeightedAchievement(1m, 80m),
            }),
            new ProjectHealthPolicy.ObjectiveInput(b, ProjectObjectiveStatus.NotStarted, 10m,
                Array.Empty<ProjectHealthPolicy.WeightedAchievement>()),
        }).ToDictionary(p => p.ObjectiveId);

        Assert.Equal(80m, result[a].KpiScore);
        Assert.Equal(0.75m, result[a].NormalizedWeight);  // 30 / 40 — كسر 0..1 لا نسبة مئويّة
        Assert.Equal(0.25m, result[b].NormalizedWeight);  // 10 / 40
        Assert.Null(result[b].KpiScore);                  // هدف بلا مؤشّرات ⟹ غير محتسَب لا صفر
        Assert.Equal(0, result[b].TotalKpiCount);
    }

    // ==================================================================
    // DEC-W4-03 — الترجيح على مستويين (مثال المالك الحاكم)
    // ==================================================================

    /// <summary>
    /// **مثال المالك الحاكم**: هدف أ (وزن 70) بمؤشّرين (وزن 50 · 100%) و(وزن 50 · 0%) ⟹ 50،
    /// وهدف ب (وزن 30) بمؤشّر واحد 100% ⟹ 100. نتيجة المشروع **65** لا 66.67
    /// (66.67 هو المتوسّط المسطَّح الممنوع لثلاثة مؤشّرات: 100 و0 و100).
    /// </summary>
    [Fact]
    public void ComputeProjectKpiScore_TwoLevelWeighting_MatchesOwnerExample()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var objectives = new[]
        {
            new ProjectHealthPolicy.ObjectiveInput(a, ProjectObjectiveStatus.InProgress, 70m, new[]
            {
                new ProjectHealthPolicy.WeightedAchievement(50m, 100m),
                new ProjectHealthPolicy.WeightedAchievement(50m, 0m),
            }),
            new ProjectHealthPolicy.ObjectiveInput(b, ProjectObjectiveStatus.InProgress, 30m, new[]
            {
                new ProjectHealthPolicy.WeightedAchievement(50m, 100m),
            }),
        };

        var inner = ProjectHealthPolicy.ComputeObjectiveProgress(objectives).ToDictionary(p => p.ObjectiveId);
        Assert.Equal(50m, inner[a].KpiScore);
        Assert.Equal(100m, inner[b].KpiScore);

        var project = ProjectHealthPolicy.ComputeProjectKpiScore(objectives);

        Assert.Equal(65m, project.Score);
        Assert.NotEqual(66.67m, project.Score);   // المتوسّط المسطَّح الممنوع صراحةً
        Assert.False(project.AllWeightsZero);
        Assert.Equal(2, project.ComputedCount);   // عناصر المستوى الثاني = الأهداف
        Assert.Equal(2, project.TotalCount);
    }

    /// <summary>هدف بلا نتيجة محتسَبة يُستبعَد من البسط والمقام ⟹ لا يجرّ المشروع إلى الصفر.</summary>
    [Fact]
    public void ComputeProjectKpiScore_ObjectiveWithoutComputableScore_IsExcludedNotZeroed()
    {
        var result = ProjectHealthPolicy.ComputeProjectKpiScore(new[]
        {
            new ProjectHealthPolicy.ObjectiveInput(Guid.NewGuid(), ProjectObjectiveStatus.InProgress, 80m, new[]
            {
                new ProjectHealthPolicy.WeightedAchievement(10m, 90m),
            }),
            new ProjectHealthPolicy.ObjectiveInput(Guid.NewGuid(), ProjectObjectiveStatus.NotStarted, 20m,
                Array.Empty<ProjectHealthPolicy.WeightedAchievement>()),
        });

        Assert.Equal(90m, result.Score);
        Assert.Equal(1, result.ComputedCount);
        Assert.Equal(2, result.TotalCount);
    }

    /// <summary>كلّ أوزان الأهداف صفر ⟹ توزيع متساوٍ لا قسمة على صفر، مع رفع العلم للصحّة.</summary>
    [Fact]
    public void ComputeProjectKpiScore_AllObjectiveWeightsZero_FallsBackToEqualShare()
    {
        var result = ProjectHealthPolicy.ComputeProjectKpiScore(new[]
        {
            new ProjectHealthPolicy.ObjectiveInput(Guid.NewGuid(), ProjectObjectiveStatus.InProgress, 0m, new[]
            {
                new ProjectHealthPolicy.WeightedAchievement(1m, 40m),
            }),
            new ProjectHealthPolicy.ObjectiveInput(Guid.NewGuid(), ProjectObjectiveStatus.InProgress, 0m, new[]
            {
                new ProjectHealthPolicy.WeightedAchievement(1m, 60m),
            }),
        });

        Assert.Equal(50m, result.Score);
        Assert.True(result.AllWeightsZero);
    }

    /// <summary>لا أهداف إطلاقًا ⟹ نتيجة غير محتسَبة (<c>null</c>) لا صفر.</summary>
    [Fact]
    public void ComputeProjectKpiScore_NoObjectives_IsNotComputed()
    {
        var result = ProjectHealthPolicy.ComputeProjectKpiScore(Array.Empty<ProjectHealthPolicy.ObjectiveInput>());

        Assert.Null(result.Score);
        Assert.Equal(0, result.TotalCount);
    }

    // ==================================================================
    // §10 — صحّة الجدول الزمنيّ
    // ==================================================================

    private static readonly DateOnly Start = new(2026, 1, 1);
    private static readonly DateOnly End = new(2026, 4, 11);      // 100 يومًا بالضبط من البداية
    private static readonly DateOnly HalfWay = new(2026, 2, 20);  // 50 يومًا مضت ⟹ المتوقَّع 50%

    [Theory]
    [InlineData(60, 100)]  // متقدّم ⟹ في المسار
    [InlineData(50, 100)]  // مطابق تمامًا (فجوة 0) ⟹ في المسار
    [InlineData(45, 75)]   // فجوة −5  (ضمن −10)
    [InlineData(40, 75)]   // فجوة −10 حدّ الشمول
    [InlineData(30, 50)]   // فجوة −20 (ضمن −25)
    [InlineData(25, 50)]   // فجوة −25 حدّ الشمول
    [InlineData(10, 25)]   // فجوة −40 ⟹ حرِج
    public void ComputeScheduleScore_MapsGapToBands(decimal progress, decimal expected)
        => Assert.Equal(expected, ProjectHealthPolicy.ComputeScheduleScore(Start, End, HalfWay, progress, ProjectStatus.Active));

    /// <summary>تاريخ ناقص أو تقدّم غير معلَن ⟹ استبعاد المكوّن (null) لا صفر.</summary>
    [Fact]
    public void ComputeScheduleScore_MissingInputs_IsExcluded()
    {
        Assert.Null(ProjectHealthPolicy.ComputeScheduleScore(null, End, HalfWay, 50m, ProjectStatus.Active));
        Assert.Null(ProjectHealthPolicy.ComputeScheduleScore(Start, null, HalfWay, 50m, ProjectStatus.Active));
        Assert.Null(ProjectHealthPolicy.ComputeScheduleScore(Start, End, HalfWay, null, ProjectStatus.Active));
    }

    /// <summary>DN-07: مدّة غير موجبة ⟹ النسبة المتوقَّعة غير معرَّفة ⟹ استبعاد.</summary>
    [Fact]
    public void ComputeScheduleScore_NonPositiveDuration_IsExcluded()
        => Assert.Null(ProjectHealthPolicy.ComputeScheduleScore(Start, Start, HalfWay, 50m, ProjectStatus.Active));

    /// <summary>DN-08: مكتمل/مغلق ⟹ 100، وموقوف ⟹ استبعاد.</summary>
    [Fact]
    public void ComputeScheduleScore_HonorsProjectStatus()
    {
        Assert.Equal(100m, ProjectHealthPolicy.ComputeScheduleScore(Start, End, HalfWay, 0m, ProjectStatus.Completed));
        Assert.Equal(100m, ProjectHealthPolicy.ComputeScheduleScore(Start, End, HalfWay, 0m, ProjectStatus.Closed));
        Assert.Null(ProjectHealthPolicy.ComputeScheduleScore(Start, End, HalfWay, 0m, ProjectStatus.Paused));
    }

    // ==================================================================
    // §9 — الصحّة النهائيّة
    // ==================================================================

    [Fact]
    public void ComputeHealth_AllComponents_AppliesFiftyThirtyTwenty()
    {
        var snapshot = ProjectHealthPolicy.ComputeHealth(80m, 60m, 100m, Stamp);

        Assert.Equal(78m, snapshot.Score); // 0.50×80 + 0.30×60 + 0.20×100
        Assert.Equal(ProjectHealthStatus.Yellow, snapshot.Status);
        Assert.True(snapshot.IsEvaluated);
    }

    /// <summary>غياب مكوّن ⟹ إعادة تسوية على المتاح، لا معاقبة بصفر.</summary>
    [Fact]
    public void ComputeHealth_MissingKpiComponent_RenormalizesRemainingWeights()
    {
        var snapshot = ProjectHealthPolicy.ComputeHealth(null, 60m, 100m, Stamp);

        Assert.Equal(76m, snapshot.Score); // (0.30×60 + 0.20×100) / 0.50
        Assert.Contains(ProjectHealthReasonCodes.KpiComponentExcluded, snapshot.Reasons.Select(r => r.Code));
    }

    [Fact]
    public void ComputeHealth_NoComponentAvailable_IsNotEvaluated()
    {
        var snapshot = ProjectHealthPolicy.ComputeHealth(null, null, null, Stamp);

        Assert.Null(snapshot.Score);
        Assert.Null(snapshot.LastEvaluatedAtUtc);
        Assert.False(snapshot.IsEvaluated);
        Assert.Equal(ProjectHealthReasonCodes.NoComponentAvailable, Assert.Single(snapshot.Reasons).Code);
    }

    [Theory]
    [InlineData(80, ProjectHealthStatus.Green)]   // حدّ الأخضر بالضبط
    [InlineData(79.99, ProjectHealthStatus.Yellow)]
    [InlineData(55, ProjectHealthStatus.Yellow)]  // حدّ الأصفر بالضبط
    [InlineData(54.99, ProjectHealthStatus.Red)]
    public void ResolveStatus_UsesInclusiveThresholds(decimal score, ProjectHealthStatus expected)
        => Assert.Equal(expected, ProjectHealthPolicy.ResolveStatus(score));

    /// <summary>مكوّن المؤشّرات قد يبلغ 200 لكن النتيجة النهائيّة مقصوصة على 100.</summary>
    [Fact]
    public void ComputeHealth_ClampsFinalScoreToHundred()
        => Assert.Equal(100m, ProjectHealthPolicy.ComputeHealth(200m, 100m, 100m, Stamp).Score);

    /// <summary>الأسباب مشتقّة لا مخزَّنة — وعتبة إصدارها هي العتبة الخضراء (DN-01).</summary>
    [Fact]
    public void ComputeHealth_DerivesReasonCodes()
    {
        var unhealthy = ProjectHealthPolicy.ComputeHealth(70m, 50m, 75m, Stamp, kpiWeightsAllZero: true);
        var codes = unhealthy.Reasons.Select(r => r.Code).ToList();

        Assert.Contains(ProjectHealthReasonCodes.KpiWeightsAllZero, codes);
        Assert.Contains(ProjectHealthReasonCodes.KpiScoreBelowTarget, codes);
        Assert.Contains(ProjectHealthReasonCodes.ProgressBelowTarget, codes);
        Assert.Contains(ProjectHealthReasonCodes.ScheduleBehindPlan, codes);

        var healthy = ProjectHealthPolicy.ComputeHealth(90m, 90m, 100m, Stamp);
        Assert.Equal(ProjectHealthReasonCodes.AllComponentsHealthy, Assert.Single(healthy.Reasons).Code);
    }
}
