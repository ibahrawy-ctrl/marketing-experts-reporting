using Reporting.Domain.Enums;
using Reporting.Domain.Projects360;

namespace Reporting.Application.Projects360;

/// <summary>
/// **محرّك الاحتساب المركزيّ** (CPW-R3 · W4 · §9-8) — دوالّ نقيّة حتميّة لا غير.
///
/// <para>
/// **صنف واحد لكلّ الثوابت — قرار R2 §9-8 الحرفيّ**: «كلّ ثابت يعيش في صنف واحد
/// <c>Reporting.Application.Projects360.ProjectHealthPolicy</c> — لا رقم سحريّ متناثر، ولا منطق
/// مكرَّر بين الخدمة والواجهة». لذلك تُمنَع أيّ عتبة أو وزن أو معامل قصّ خارج هذا الملفّ.
/// </para>
///
/// <para>
/// **نقاء تامّ (Pure)**: لا <c>DbContext</c>، ولا <c>ICurrentUser</c>، ولا <c>DateTime.UtcNow</c>
/// داخل أيّ دالّة احتساب — «اليوم» يُمرَّر معاملًا. نفس المدخلات تعطي نفس المخرجات دائمًا،
/// وهو ما يجعل اختبارات الحدود في §19 ممكنة أصلًا.
/// </para>
///
/// <para>
/// **«غير محتسَب» ≠ «صفر»**: القاعدة الحاكمة في كلّ الدوالّ. مكوّن غائب يُعاد **استبعاده**
/// وتُعاد تسوية الأوزان على المتاح — لا يُعاقَب المشروع بصفر لم يُعلنه أحد.
/// </para>
///
/// <para>
/// **بنود تحتاج قرار مالك (Decision Needed) — سُجِّلت ولم تُخترَع لها قاعدة صامتة**:
/// <list type="bullet">
///   <item><description>
///     **DN-01 — عتبة إصدار رموز الأسباب**: R2 يعرّف الرموز الثمانية ولا يحدّد العتبة التي
///     تُطلِق <c>health.kpi.below_target</c> و<c>health.progress.below_target</c>. الافتراض
///     المُطبَّق هنا: **العتبة الخضراء نفسها (80)** — لأنّ «دون الأخضر» هو بالضبط ما يستدعي تفسيرًا.
///     الأسباب تبقى **مشتقّة ولا تُخزَّن** (§9 + W1-A بند 1).
///   </description></item>
///   <item><description>
///     **DN-02 — تقدّم الهدف لا يُخزَّن**: <c>ProjectObjective</c> بلا عمود <c>ProgressPercent</c>؛
///     تقدّم الهدف **مشتقّ** من تحقيق مؤشّراته الموزون (§9-8-ج). لا يُضاف عمود في W4.
///   </description></item>
///   <item><description>
///     **DN-03 — تسطيح مقابل مستويين — <u>حُسِم بقرار المالك DEC-W4-03 (نهائيّ)</u>**:
///     §9-8-ج كان ينصّ حرفيًّا على <c>kpiScore = Σ(achievement_i × w_i) / Σ(w_i)</c>
///     **«لكلّ مؤشّر نشط»** (تسطيح يتجاوز أوزان الأهداف)، بينما جدول حقول <c>ProjectObjective</c>
///     يصف <c>Weight</c> بأنّه «وزن الهدف في احتساب الصحّة». **قرار المالك في W5: الترجيح على
///     مستويين**، والمتوسّط المسطَّح لكلّ مؤشّرات المشروع **ممنوع**:
///     <list type="number">
///       <item><description>داخل الهدف: <c>Σ(achievement_i × w_i) ÷ Σ(w_i)</c> ⟹ تحقيق الهدف
///       (<see cref="ComputeObjectiveProgress"/>).</description></item>
///       <item><description>عبر أهداف المشروع: <c>Σ(objectiveScore_j × W_j) ÷ Σ(W_j)</c> على
///       الأهداف ذات النتيجة المحتسَبة فقط (<see cref="ComputeProjectKpiScore"/>).</description></item>
///     </list>
///     مثال الحَكَم: هدف أ (وزن 70) بمؤشّرين (50 · 100%) و(50 · 0%) ⟹ 50؛ هدف ب (وزن 30)
///     بمؤشّر واحد 100% ⟹ 100؛ **نتيجة المشروع = 65** لا 66.67. لمنع العودة للتسطيح صامتًا
///     غُيِّر **نوع** مُدخَل <see cref="ComputeProjectKpiScore"/> من قائمة مؤشّرات إلى قائمة أهداف،
///     فأيّ استدعاء مسطَّح قديم يفشل عند الترجمة لا في وقت التشغيل.
///   </description></item>
///   <item><description>
///     **DN-04 — <c>LowerIsBetter</c> عند قيمة حاليّة ≤ 0**: R2 يعرّف
///     <c>achievement = Target / Current</c> ولا يعالج القسمة على صفر. المطبَّق: قيمة ≤ 0 في
///     مؤشّر «الأقلّ أفضل» تعني بلوغ الأفضل الممكن ⟹ <see cref="AchievementMax"/> بدل استثناء.
///   </description></item>
///   <item><description>
///     **DN-05 — التدوير**: R2 لا ينصّ على منزلة تدوير. المطبَّق: منزلتان عشريّتان
///     بـ<see cref="MidpointRounding.AwayFromZero"/> اتّساقًا مع <c>numeric(9,2)</c> في السكيمة.
///   </description></item>
///   <item><description>
///     **DN-06 — <c>BaselineValue</c> خارج المعادلة**: معادلة §9-8-أ لا تستعمل خطّ الأساس
///     إطلاقًا. يبقى حقلًا **إعلاميًّا** في W4، ولا يُشتقّ منه شيء — فحالة «Target == Baseline»
///     لا أثر لها بنيويًّا.
///   </description></item>
///   <item><description>
///     **DN-07 — <c>EndDate ≤ StartDate</c>**: مدّة غير موجبة تجعل النسبة المتوقَّعة غير معرَّفة
///     ⟹ يُستبعَد مكوّن الجدول (<c>null</c>) بدل فرض 0 أو 100.
///   </description></item>
///   <item><description>
///     **DN-08 — أثر حالة المشروع على الجدول**: §10 يفرض أخذ الحالة مدخلًا، وR2 §9-8-د لا
///     يحسمها. المطبَّق: <c>Completed</c>/<c>Closed</c> ⟹ 100 (سُلِّم فلا تأخّر)،
///     <c>Paused</c> ⟹ استبعاد المكوّن (مشروع موقوف بقرار لا يُعاقَب على انزياح تقويميّ مقصود)،
///     وما عداه يمرّ بالمعادلة. **تعداد <c>ProjectStatus</c> لم يُمَسّ** (§10).
///   </description></item>
/// </list>
/// </para>
/// </summary>
public static class ProjectHealthPolicy
{
    // ======================================================================
    // الثوابت — لا رقم سحريّ خارج هذا القسم (R2 §9-8)
    // ======================================================================

    /// <summary>الحدّ الأدنى لنسبة التحقيق بعد القصّ (§9-8-أ).</summary>
    public const decimal AchievementMin = 0m;

    /// <summary>الحدّ الأعلى لنسبة التحقيق بعد القصّ — يسمح بتجاوز الهدف حتّى الضعف (§9-8-أ).</summary>
    public const decimal AchievementMax = 200m;

    /// <summary>نقطة التعادل: تحقيق 100% يعني بلوغ الهدف تمامًا ⟹ انحراف صفر.</summary>
    public const decimal AchievementNeutral = 100m;

    /// <summary>عتبة الضجيج في الاتّجاه ‎±2%‎ (§9-8-ب).</summary>
    public const decimal TrendEpsilon = 2m;

    /// <summary>وزن مكوّن المؤشّرات في الصحّة النهائيّة (§9-8-هـ).</summary>
    public const decimal KpiComponentWeight = 0.50m;

    /// <summary>وزن مكوّن التقدّم المعلَن يدويًّا في الصحّة النهائيّة (§9-8-هـ).</summary>
    public const decimal ProgressComponentWeight = 0.30m;

    /// <summary>وزن مكوّن الجدول الزمنيّ في الصحّة النهائيّة (§9-8-هـ).</summary>
    public const decimal ScheduleComponentWeight = 0.20m;

    /// <summary>عتبة اللون الأخضر (§9-8-هـ).</summary>
    public const decimal GreenThreshold = 80m;

    /// <summary>عتبة اللون الأصفر (§9-8-هـ) — دونها أحمر.</summary>
    public const decimal YellowThreshold = 55m;

    /// <summary>نتيجة الجدول حين لا تأخّر (<c>gap ≥ 0</c>).</summary>
    public const decimal ScheduleOnTrackScore = 100m;

    /// <summary>نتيجة الجدول عند تأخّر طفيف (<c>−10 ≤ gap &lt; 0</c>).</summary>
    public const decimal ScheduleSlightlyBehindScore = 75m;

    /// <summary>نتيجة الجدول عند تأخّر متوسّط (<c>−25 ≤ gap &lt; −10</c>).</summary>
    public const decimal ScheduleBehindScore = 50m;

    /// <summary>نتيجة الجدول عند تأخّر حادّ (<c>gap &lt; −25</c>).</summary>
    public const decimal ScheduleCriticalScore = 25m;

    /// <summary>حدّ التأخّر الطفيف (§9-8-د).</summary>
    public const decimal ScheduleSlightlyBehindGap = -10m;

    /// <summary>حدّ التأخّر المتوسّط (§9-8-د).</summary>
    public const decimal ScheduleBehindGap = -25m;

    /// <summary>الحدّ الأدنى لأيّ نسبة مئويّة مُعلَنة.</summary>
    public const decimal PercentMin = 0m;

    /// <summary>الحدّ الأعلى لأيّ نسبة مئويّة مُعلَنة.</summary>
    public const decimal PercentMax = 100m;

    /// <summary>منازل التدوير — مطابِقة لـ<c>numeric(9,2)</c> في السكيمة (DN-05).</summary>
    public const int ScoreDecimals = 2;

    // ======================================================================
    // مدخلات المحرّك — أنواع قيميّة نقيّة لا تلمس قاعدة البيانات
    // ======================================================================

    /// <summary>مؤشّر واحد كما يدخل التجميع الموزون: وزنه الخام ونسبة تحقيقه (أو <c>null</c>).</summary>
    /// <param name="Weight">الوزن الخام كما أُعلن — لا يُفرَض مجموع محدَّد.</param>
    /// <param name="Achievement">نسبة التحقيق المحتسَبة — <c>null</c> ⟹ يُستبعَد المؤشّر.</param>
    public readonly record struct WeightedAchievement(decimal Weight, decimal? Achievement);

    /// <summary>نتيجة تجميع موزون مع بيانات التفسير اللازمة لرموز الأسباب.</summary>
    /// <param name="Score">النتيجة الموزونة — <c>null</c> ⟹ لا عنصر محتسَب.</param>
    /// <param name="AllWeightsZero">هل عوملت العناصر بأوزان متساوية لأنّ مجموع الأوزان صفر؟</param>
    /// <param name="ComputedCount">عدد العناصر التي دخلت الاحتساب فعلًا.</param>
    /// <param name="TotalCount">إجماليّ العناصر المعروضة على المحرّك.</param>
    public readonly record struct WeightedScore(
        decimal? Score,
        bool AllWeightsZero,
        int ComputedCount,
        int TotalCount);

    /// <summary>هدف واحد كما يدخل محرّك تقدّم الأهداف.</summary>
    /// <param name="ObjectiveId">معرّف الهدف.</param>
    /// <param name="Status">الحالة المعلَنة يدويًّا (Manual-First — لا تُشتقّ).</param>
    /// <param name="Weight">وزن الهدف الخام.</param>
    /// <param name="Kpis">مؤشّرات الهدف النشطة بأوزانها ونسب تحقيقها.</param>
    public sealed record ObjectiveInput(
        Guid ObjectiveId,
        ProjectObjectiveStatus Status,
        decimal Weight,
        IReadOnlyList<WeightedAchievement> Kpis);

    // ======================================================================
    // أدوات مشتركة
    // ======================================================================

    /// <summary>قصّ قيمة ضمن مجال مغلق.</summary>
    public static decimal Clamp(decimal value, decimal min, decimal max) =>
        value < min ? min : value > max ? max : value;

    /// <summary>تدوير موحَّد لكلّ نتائج المحرّك (DN-05).</summary>
    public static decimal Round(decimal value) =>
        Math.Round(value, ScoreDecimals, MidpointRounding.AwayFromZero);

    // ======================================================================
    // §7 — محرّك تحقيق المؤشّر (R2 §9-8-أ/ب)
    // ======================================================================

    /// <summary>
    /// نسبة تحقيق مؤشّر واحد (§9-8-أ).
    ///
    /// <para>
    /// <c>HigherIsBetter : (Current / Target) × 100</c> ·
    /// <c>LowerIsBetter : (Target / Current) × 100</c> ثمّ قصّ على 0..200.
    /// </para>
    ///
    /// <para>
    /// **غير محتسَب (<c>null</c>)** حين: لا قيمة حاليّة، أو لا هدف، أو الهدف ≤ 0.
    /// خطّ الأساس لا يدخل المعادلة إطلاقًا (DN-06).
    /// </para>
    /// </summary>
    public static decimal? ComputeAchievement(
        decimal? currentValue,
        decimal? targetValue,
        ProjectKpiDirection direction)
    {
        if (currentValue is null || targetValue is null || targetValue.Value <= 0m)
            return null;

        decimal raw;
        if (direction == ProjectKpiDirection.LowerIsBetter)
        {
            // DN-04 — قيمة ≤ 0 في «الأقلّ أفضل» = الأفضل الممكن، لا قسمة على صفر.
            raw = currentValue.Value <= 0m
                ? AchievementMax
                : targetValue.Value / currentValue.Value * AchievementNeutral;
        }
        else
        {
            raw = currentValue.Value / targetValue.Value * AchievementNeutral;
        }

        return Round(Clamp(raw, AchievementMin, AchievementMax));
    }

    /// <summary>الانحراف عن الهدف = <c>achievement − 100</c> (§9-8-أ).</summary>
    public static decimal? ComputeVariance(decimal? achievement) =>
        achievement is null ? null : Round(achievement.Value - AchievementNeutral);

    /// <summary>
    /// الاتّجاه من نسبتَي تحقيق متتاليتين بعتبة ‎±<see cref="TrendEpsilon"/>‎ (§9-8-ب).
    /// أقلّ من قراءتين محتسَبتين ⟹ <see cref="ProjectKpiTrend.Unknown"/>.
    /// </summary>
    public static ProjectKpiTrend ComputeTrend(decimal? latestAchievement, decimal? previousAchievement)
    {
        if (latestAchievement is null || previousAchievement is null)
            return ProjectKpiTrend.Unknown;

        var delta = latestAchievement.Value - previousAchievement.Value;
        if (delta > TrendEpsilon) return ProjectKpiTrend.Up;
        if (delta < -TrendEpsilon) return ProjectKpiTrend.Down;
        return ProjectKpiTrend.Flat;
    }

    /// <summary>
    /// النموذج الكامل لمؤشّر واحد: التحقيق + الانحراف + الاتّجاه.
    /// <paramref name="previousValue"/> = قيمة القراءة **ما قبل الأخيرة**؛ غيابها ⟹ اتّجاه مجهول.
    /// </summary>
    public static ProjectKpiAchievement ComputeKpi(
        decimal? currentValue,
        decimal? targetValue,
        ProjectKpiDirection direction,
        decimal? previousValue = null)
    {
        var achievement = ComputeAchievement(currentValue, targetValue, direction);
        if (achievement is null)
            return ProjectKpiAchievement.NotComputed;

        var previousAchievement = ComputeAchievement(previousValue, targetValue, direction);
        return new ProjectKpiAchievement(
            achievement,
            ComputeVariance(achievement),
            ComputeTrend(achievement, previousAchievement));
    }

    // ======================================================================
    // §8 — التجميع الموزون (R2 §9-8-ج)
    // ======================================================================

    /// <summary>
    /// تجميع موزون لنسب التحقيق: <c>Σ(achievement_i × w_i) / Σ(w_i)</c> على المحتسَب فقط.
    ///
    /// <para>
    /// **مجموع الأوزان صفر ⟹ أوزان متساوية** (متوسّط حسابيّ) — وهذا ما يجعل تفعيل الترجيح
    /// لاحقًا **بلا هجرة وبلا تغيير معادلة**. الأوزان السالبة تُعامَل صفرًا (حارس مدخلات).
    /// </para>
    ///
    /// <para>**لا عنصر محتسَب ⟹ <c>Score = null</c>** (يُستبعَد المكوّن ولا يُصفَّر).</para>
    /// </summary>
    public static WeightedScore ComputeWeightedScore(IReadOnlyList<WeightedAchievement> items)
    {
        if (items is null || items.Count == 0)
            return new WeightedScore(null, AllWeightsZero: false, ComputedCount: 0, TotalCount: 0);

        var computed = new List<WeightedAchievement>(items.Count);
        foreach (var item in items)
        {
            if (item.Achievement is not null)
                computed.Add(item);
        }

        if (computed.Count == 0)
            return new WeightedScore(null, AllWeightsZero: false, ComputedCount: 0, TotalCount: items.Count);

        decimal totalWeight = 0m;
        decimal weightedSum = 0m;
        decimal plainSum = 0m;
        foreach (var item in computed)
        {
            var weight = item.Weight > 0m ? item.Weight : 0m;
            totalWeight += weight;
            weightedSum += item.Achievement!.Value * weight;
            plainSum += item.Achievement!.Value;
        }

        var allWeightsZero = totalWeight <= 0m;
        var score = allWeightsZero
            ? plainSum / computed.Count
            : weightedSum / totalWeight;

        return new WeightedScore(Round(score), allWeightsZero, computed.Count, items.Count);
    }

    /// <summary>
    /// **نتيجة مؤشّرات المشروع — ترجيح على مستويين** (DEC-W4-03، انظر DN-03).
    ///
    /// <para>
    /// المستوى الأوّل داخل كلّ هدف ثمّ المستوى الثاني عبر أوزان الأهداف. الأهداف التي لا نتيجة
    /// محتسَبة لها (بلا مؤشّرات، أو كلّ مؤشّراتها بلا قيمة/هدف) **تُستبعَد من البسط والمقام معًا**
    /// بحكم <see cref="ComputeWeightedScore"/>، فلا يُصفَّر المشروع بسبب هدف فارغ.
    /// </para>
    ///
    /// <para>
    /// <c>TotalCount</c> و<c>ComputedCount</c> في النتيجة تعدّان **الأهداف** لا المؤشّرات —
    /// لأنّ عناصر المستوى الثاني هي الأهداف. أعداد المؤشّرات تُعرَض من مصدرها في اللوحة.
    /// </para>
    /// </summary>
    public static WeightedScore ComputeProjectKpiScore(IReadOnlyList<ObjectiveInput> objectives)
    {
        if (objectives is null || objectives.Count == 0)
            return new WeightedScore(null, AllWeightsZero: false, ComputedCount: 0, TotalCount: 0);

        // ComputeObjectiveProgress يحافظ على ترتيب المدخلات عنصرًا بعنصر ⟹ الفهرسة المتوازية آمنة.
        var progress = ComputeObjectiveProgress(objectives);
        var objectiveScores = new List<WeightedAchievement>(progress.Count);
        for (var i = 0; i < progress.Count; i++)
            objectiveScores.Add(new WeightedAchievement(objectives[i].Weight, progress[i].KpiScore));

        return RollUpObjectiveScores(objectiveScores);
    }

    /// <summary>
    /// **خطوة الترجيح الخارجيّة وحدها** — لمسار اللوحة الذي حسب نتائج الأهداف مسبقًا
    /// (<c>ProjectObjectiveDto.KpiScore</c>) فلا يعيد الحساب ولا يستعلم مرّة ثانية.
    /// كلّ عنصر: <c>Weight</c> = وزن الهدف الخام، <c>Achievement</c> = نتيجة مؤشّراته أو <c>null</c>.
    /// </summary>
    public static WeightedScore RollUpObjectiveScores(IReadOnlyList<WeightedAchievement> objectiveScores) =>
        ComputeWeightedScore(objectiveScores);

    /// <summary>
    /// **تقدّم الأهداف** — لكلّ هدف: وزنه المُطبَّع (<c>w_i ÷ Σw</c>) ونتيجة مؤشّراته الموزونة.
    ///
    /// <para>
    /// مثال §8 الحاكم: مؤشّر بوزن 60 وتحقيق 80، وآخر بوزن 40 وتحقيق 50 ⟹
    /// <c>(80×60 + 50×40) ÷ 100 = 68</c>.
    /// </para>
    ///
    /// <para>
    /// **تطبيع الأوزان**: كلّ الأوزان صفر (أو سالبة) ⟹ توزيع متساوٍ <c>1 ÷ n</c>.
    /// لا قائمة أهداف ⟹ قائمة فارغة لا استثناء. التقدّم **مشتقّ لا مُخزَّن** (DN-02).
    /// </para>
    /// </summary>
    public static IReadOnlyList<ProjectObjectiveProgress> ComputeObjectiveProgress(
        IReadOnlyList<ObjectiveInput> objectives)
    {
        if (objectives is null || objectives.Count == 0)
            return Array.Empty<ProjectObjectiveProgress>();

        decimal totalWeight = 0m;
        foreach (var objective in objectives)
            totalWeight += objective.Weight > 0m ? objective.Weight : 0m;

        var equalShare = totalWeight <= 0m;
        var result = new List<ProjectObjectiveProgress>(objectives.Count);

        foreach (var objective in objectives)
        {
            var rawWeight = objective.Weight > 0m ? objective.Weight : 0m;
            var normalized = equalShare
                ? 1m / objectives.Count
                : rawWeight / totalWeight;

            var kpis = objective.Kpis ?? (IReadOnlyList<WeightedAchievement>)Array.Empty<WeightedAchievement>();
            var score = ComputeWeightedScore(kpis);

            result.Add(new ProjectObjectiveProgress(
                ObjectiveId: objective.ObjectiveId,
                Status: objective.Status,
                NormalizedWeight: Round(normalized),
                KpiScore: score.Score,
                ComputedKpiCount: score.ComputedCount,
                TotalKpiCount: kpis.Count));
        }

        return result;
    }

    // ======================================================================
    // §10 — نتيجة الجدول الزمنيّ (R2 §9-8-د)
    // ======================================================================

    /// <summary>
    /// نتيجة الجدول الزمنيّ من التواريخ والتقدّم المعلَن فقط — **بلا مهامّ ولا مراحل** (§10).
    ///
    /// <para>
    /// <c>expected = (today − Start) ÷ (End − Start) × 100</c> مقصوصة 0..100 ·
    /// <c>gap = Progress − expected</c> ·
    /// <c>gap ≥ 0 ⟶ 100</c> | <c>−10 ≤ gap &lt; 0 ⟶ 75</c> |
    /// <c>−25 ≤ gap &lt; −10 ⟶ 50</c> | <c>gap &lt; −25 ⟶ 25</c>.
    /// </para>
    ///
    /// <para>
    /// **الاستبعاد (<c>null</c>)**: تاريخ ناقص، أو تقدّم غير معلَن، أو مدّة غير موجبة (DN-07)،
    /// أو مشروع موقوف (DN-08). **مكتمل/مغلق ⟹ 100**. لم يبدأ بعد ⟹ <c>expected = 0</c> طبيعيًّا
    /// فالنتيجة 100؛ ومتأخّر عن نهايته ⟹ <c>expected = 100</c> فالفجوة سالبة تلقائيًّا.
    /// </para>
    /// </summary>
    public static decimal? ComputeScheduleScore(
        DateOnly? startDate,
        DateOnly? endDate,
        DateOnly today,
        decimal? progressPercent,
        ProjectStatus status)
    {
        // DN-08 — أثر الحالة، وتعداد ProjectStatus لم يُمَسّ.
        if (status is ProjectStatus.Completed or ProjectStatus.Closed)
            return ScheduleOnTrackScore;
        if (status == ProjectStatus.Paused)
            return null;

        if (startDate is null || endDate is null || progressPercent is null)
            return null;

        var totalDays = endDate.Value.DayNumber - startDate.Value.DayNumber;
        if (totalDays <= 0)
            return null; // DN-07

        var elapsedDays = today.DayNumber - startDate.Value.DayNumber;
        var expected = Clamp((decimal)elapsedDays / totalDays * AchievementNeutral, PercentMin, PercentMax);
        var gap = Clamp(progressPercent.Value, PercentMin, PercentMax) - expected;

        if (gap >= 0m) return ScheduleOnTrackScore;
        if (gap >= ScheduleSlightlyBehindGap) return ScheduleSlightlyBehindScore;
        if (gap >= ScheduleBehindGap) return ScheduleBehindScore;
        return ScheduleCriticalScore;
    }

    // ======================================================================
    // §8-ب — تقدّم المشروع من المخرَجات التعاقديّة (P360-WF-R2 §6-1 · GAP-02/03/21)
    // ======================================================================

    /// <summary>مخرَج تعاقديّ واحد كما يدخل محرّك تقدّم المشروع.</summary>
    /// <param name="WeightPercentage">الوزن المعلَن — سالبه يُعامَل صفرًا (حارس مدخلات).</param>
    /// <param name="ProgressPercent">نسبة الإنجاز المعلَنة يدويًّا على المخرَج.</param>
    /// <param name="Status">الحالة المعلَنة — <c>Delivered</c> تُقرأ 100 مهما كانت النسبة المسجَّلة.</param>
    public readonly record struct DeliverableProgressInput(
        decimal WeightPercentage,
        decimal ProgressPercent,
        ProjectDeliverableStatus Status);

    /// <summary>نتيجة تقدّم المشروع: النسبة + **كيف** حُسِبت + على كم مخرَج.</summary>
    public readonly record struct ProjectProgressResult(
        decimal Percent,
        ProjectProgressMode Mode,
        int SourceCount);

    /// <summary>
    /// **تقدّم المشروع = متوسّط موزون لنسب إنجاز المخرَجات التعاقديّة النشطة** (§6-1).
    ///
    /// <para>
    /// **لماذا مشتقّ لا معلَن؟** لأنّ الحقل المعلَن كان بلا مسار كتابة فبقي صفرًا في كلّ الصفوف،
    /// فأبطل 0.30 من معادلة الصحّة وجعل الأخضر مستحيلًا (GAP-02 → GAP-04). المصدر الوحيد
    /// المضبوط للتقدّم في هذه المنظومة هو **الالتزام التعاقديّ**، فمنه يُشتقّ.
    /// </para>
    ///
    /// <list type="bullet">
    /// <item><b>الملغى يخرج من البسط والمقام معًا</b>: التزام أُلغي لم يعد التزامًا، وعدّه صفرًا يعاقب المشروع على تخفيف نطاقه.</item>
    /// <item><b>المُسلَّم = 100 حتمًا</b>: مخرَج مُسلَّم بنسبة 80 مسجَّلة تناقضٌ يُحسم لصالح الحالة لا الرقم المهمَل.</item>
    /// <item><b>مجموع الأوزان صفر ⟹ توزيع متساوٍ</b> مع <see cref="ProjectProgressMode.EqualWeightFallback"/> — رقم صالح بمصدر معلن وتنبيه ظاهر، لا رفض ولا صفر.</item>
    /// <item><b>لا مخرَج واحد ⟹ 0 مع <see cref="ProjectProgressMode.NoDeliverables"/></b> — الصفر هنا **مفسَّر** لا حكم.</item>
    /// </list>
    ///
    /// <para>دالّة نقيّة: لا ساعة ولا قاعدة بيانات ولا تخويل. المرشِّح (<c>IsActive</c>) مسؤوليّة المستدعي.</para>
    /// </summary>
    public static ProjectProgressResult ComputeProjectProgress(IReadOnlyList<DeliverableProgressInput> deliverables)
    {
        var counted = new List<DeliverableProgressInput>(deliverables.Count);
        foreach (var d in deliverables)
        {
            if (d.Status == ProjectDeliverableStatus.Cancelled) continue;
            counted.Add(d);
        }

        if (counted.Count == 0)
            return new ProjectProgressResult(PercentMin, ProjectProgressMode.NoDeliverables, 0);

        // **الأوزان ناقصة = الأوزان غير مضبوطة**: يكفي مخرَج واحد بلا وزن موجب كي يسقط الاحتساب
        // كلّه إلى التوزيع المتساوي. البديل — تجاهل عديم الوزن — كان سيُخرِج التزامًا تعاقديًّا من
        // القياس **بصمت**، فيقرأ المدير نسبةً لا تصف عقده، وهو أسوأ من تنبيه صريح بأنّ الأوزان ناقصة.
        decimal weightSum = 0m;
        var equalWeights = false;
        foreach (var d in counted)
        {
            if (d.WeightPercentage <= 0m) { equalWeights = true; break; }
            weightSum += d.WeightPercentage;
        }

        decimal numerator = 0m;
        foreach (var d in counted)
        {
            var effective = d.Status == ProjectDeliverableStatus.Delivered
                ? PercentMax
                : Clamp(d.ProgressPercent, PercentMin, PercentMax);

            numerator += effective * (equalWeights ? 1m : d.WeightPercentage);
        }

        var denominator = equalWeights ? counted.Count : weightSum;
        var percent = Round(Clamp(numerator / denominator, PercentMin, PercentMax));

        return new ProjectProgressResult(
            percent,
            equalWeights ? ProjectProgressMode.EqualWeightFallback : ProjectProgressMode.Weighted,
            counted.Count);
    }

    /// <summary>
    /// مكوّن التقدّم كما يدخل معادلة الصحّة: يُستبعَد حين لا مصدر له أصلًا
    /// (<see cref="ProjectProgressMode.NoDeliverables"/>)، لأنّ «لا مخرَجات» ليست صفرًا في الأداء
    /// وإدخالها صفرًا يُبطِل 0.30 من المعادلة ويجعل الأخضر مستحيلًا (GAP-04).
    ///
    /// <para>
    /// **قاعدة واحدة لمسارَين**: مسار الكتابة (<c>ProjectHealthService</c>) ومسار القراءة
    /// (<c>ProjectOverviewService</c>) يستدعيان هذه الدالّة بعينها. حين كانت القاعدة مكتوبةً في
    /// مسار الكتابة وحده كانت اللوحة تعرض رقمًا يخالف العمود المخزَّن لنفس المشروع.
    /// </para>
    /// </summary>
    public static decimal? ToHealthProgressComponent(decimal percent, ProjectProgressMode mode) =>
        mode == ProjectProgressMode.NoDeliverables ? null : percent;

    // ======================================================================
    // §7-ب — بناء الإشارات التشغيليّة من صفوف الدومين (P360-WF-R2)
    // ======================================================================

    /// <summary>مخرَج كما يدخل إحصاء التأخّر: حالته وتاريخ استحقاقه لا غير.</summary>
    public readonly record struct DeliverableSignalInput(ProjectDeliverableStatus Status, DateOnly? DueDate);

    /// <summary>
    /// **البانِي الوحيد للإشارات التشغيليّة** (§7). يُستدعى من مسار الكتابة (احتساب الصحّة
    /// وحفظها) ومن مسار القراءة (لوحة Project 360) معًا.
    ///
    /// <para>
    /// **لماذا دالّة مشتركة لا نسختان؟** لأنّ نسختين تعنيان أن تعرض اللوحة «أخضر» بينما القائمة
    /// تعرض «محجوب» لنفس المشروع في نفس اللحظة — وهو بالضبط صنف التناقض الذي أُنشئت هذه
    /// السياسة لمنعه. القاعدة تُكتب مرّة وتُقرأ من موضعين.
    /// </para>
    ///
    /// <list type="bullet">
    /// <item><b>المتأخّر يُحصى من التاريخ لا من الحالة المعلَنة</b>: <c>Delayed</c> إعلان يدويّ قد لا يُحدَّث، أمّا تجاوز الاستحقاق بلا تسليم فحقيقة.</item>
    /// <item><b>الخطورة الحرجة = حاجب</b> والمرتفعة = خطر — التطبيق الوحيد المتاح لحالة <c>Blocked</c> بلا كيان «حاجب» مستقلّ، ويستعمل <c>Risk.ProjectId</c> القائم ⟹ صفر عمود جديد.</item>
    /// <item><b>«مفتوح» = ليس <c>Closed</c></b> بما فيه <c>Mitigating</c>/<c>Monitoring</c>: الخطر تحت المعالجة لا يزال قائمًا، وعدّه مغلقًا اطمئنانٌ سابق لأوانه. ترشيح المفتوح مسؤوليّة المستدعي.</item>
    /// </list>
    /// </summary>
    public static ProjectOperationalSignals BuildOperationalSignals(
        IReadOnlyList<DeliverableSignalInput> activeDeliverables,
        IReadOnlyList<RiskSeverity> openRiskSeverities,
        DateOnly today,
        ProjectProgressMode progressMode)
    {
        var overdue = 0;
        foreach (var d in activeDeliverables)
        {
            if (d.DueDate is not DateOnly due) continue;
            if (due >= today) continue;
            if (d.Status is ProjectDeliverableStatus.Delivered or ProjectDeliverableStatus.Cancelled) continue;
            overdue++;
        }

        var blockers = 0;
        var highRisks = 0;
        foreach (var s in openRiskSeverities)
        {
            if (s == RiskSeverity.Critical) blockers++;
            else if (s == RiskSeverity.High) highRisks++;
        }

        return new ProjectOperationalSignals(blockers, overdue, highRisks, progressMode);
    }

    // ======================================================================
    // §9 — الصحّة النهائيّة (R2 §9-8-هـ)
    // ======================================================================

    /// <summary>
    /// اللون من النتيجة الرقميّة وحدها: <c>≥ 80 أخضر</c> · <c>≥ 55 في خطر</c> · غير ذلك متأخّر (§9-8-هـ).
    ///
    /// <para>
    /// **العتبتان لم تتغيّرا** في P360-WF-R2، وإنّما تغيّرت أسماء الحالات لتصف الواقع التشغيليّ
    /// بدل الألوان. هذه الدالّة هي **الطبقة الرقميّة** فقط؛ الحالة النهائيّة الظاهرة تمرّ
    /// بـ<see cref="ResolveStatus(decimal?, ProjectOperationalSignals)"/> الذي يُعطي الحجب
    /// والتأخّر أسبقيّةً على الرقم.
    /// </para>
    /// </summary>
    public static ProjectHealthStatus ResolveStatus(decimal score) =>
        score >= GreenThreshold ? ProjectHealthStatus.Green
        : score >= YellowThreshold ? ProjectHealthStatus.AtRisk
        : ProjectHealthStatus.Delayed;

    /// <summary>
    /// **الحالة الظاهرة النهائيّة** (P360-WF-R2 §7) — حتميّة ومرتَّبة الأسبقيّة:
    ///
    /// <list type="number">
    /// <item><c>Blocked</c> — حاجب مفتوح. **يتقدّم على كلّ حساب**، فلا يصير مشروع محجوب أخضر بارتفاع مؤشّراته.</item>
    /// <item><c>Delayed</c> — مخرَج أساسيّ تجاوز استحقاقه، أو انزلاق جدوليّ جوهريّ (نتيجة جدول ≤ <see cref="ScheduleBehindScore"/>)، أو نتيجة رقميّة دون عتبة الأصفر.</item>
    /// <item><c>AtRisk</c> — خطر مرتفع مفتوح، أو نتيجة رقميّة دون العتبة الخضراء.</item>
    /// <item><c>Green</c> — لا حاجب ولا تأخّر ولا خطر مرتفع، والنتيجة ≥ 80.</item>
    /// </list>
    ///
    /// <para>
    /// <paramref name="score"/> = <c>null</c> يعني «لم تُحتسَب» ⟹ <see cref="ProjectHealthStatus.NotEvaluated"/>
    /// **إلّا** إذا وُجد حاجب: حقيقة الحجب معلومة بلا حاجة إلى معادلة، وإخفاؤها خلف «لم تُقيَّم» تضليل.
    /// </para>
    ///
    /// <para>
    /// **قابليّة الوصول إلى الأخضر مضمونة رياضيًّا**: مشروع بلا إشارات سلبيّة ومخرَجاته منجَزة
    /// يعطي <c>progress = 100</c> و<c>schedule = 100</c>، فيكفيه <c>kpiScore ≥ 60</c> لبلوغ 80.
    /// قبل هذه الجولة كان <c>progress ≡ 0</c> يفرض <c>kpiScore ≥ 130</c> — أي أخضر مستحيل (GAP-04).
    /// </para>
    /// </summary>
    /// <param name="score">النتيجة الموزونة — <c>null</c> ⟹ لم تُحتسَب.</param>
    /// <param name="signals">الإشارات التشغيليّة المُحصاة من الدومين.</param>
    /// <param name="scheduleScore">
    /// نتيجة الجدول — تُمرَّر كي يُعَدّ الانزلاق الجوهريّ (<c>≤ 50</c>، أي فجوة أشدّ من −10 يومًا
    /// نسبيًّا) تأخّرًا صريحًا لا مجرّد خفضٍ للمتوسّط.
    /// </param>
    public static ProjectHealthStatus ResolveStatus(
        decimal? score,
        ProjectOperationalSignals signals,
        decimal? scheduleScore = null)
    {
        if (signals.HasBlocker) return ProjectHealthStatus.Blocked;
        if (score is null) return ProjectHealthStatus.NotEvaluated;

        if (signals.HasOverdueDeliverable) return ProjectHealthStatus.Delayed;
        if (scheduleScore is not null && scheduleScore.Value <= ScheduleBehindScore)
            return ProjectHealthStatus.Delayed;

        var numeric = ResolveStatus(score.Value);
        if (numeric == ProjectHealthStatus.Delayed) return ProjectHealthStatus.Delayed;
        if (signals.HasHighRisk) return ProjectHealthStatus.AtRisk;
        return numeric;
    }

    /// <summary>
    /// **الصحّة النهائيّة** (§9-8-هـ):
    /// <c>0.50 × kpiScore + 0.30 × ProgressPercent + 0.20 × scheduleScore</c>.
    ///
    /// <para>
    /// **إعادة تسوية الأوزان عند غياب مكوّن**: تُقسَم أوزان المكوّنات المتاحة على مجموعها فيبقى
    /// المجموع 1.00 — «مشروع بلا مؤشّرات بعد يُقاس بتقدّمه وجدوله لا يُعاقَب بصفر».
    /// </para>
    ///
    /// <para>
    /// **لا مكوّن متاح ⟹ <see cref="ProjectHealthSnapshot.NotEvaluated"/>** بختم تقييم فارغ:
    /// «لم يُقيَّم بعد» حالة مشروعة لا صفر.
    /// </para>
    ///
    /// <para>**الأسباب مشتقّة ولا تُخزَّن** (§9 + W1-A بند 1)؛ عتبة إصدارها في DN-01.</para>
    /// </summary>
    /// <param name="kpiScore">مكوّن المؤشّرات (0..200 نظريًّا) — <c>null</c> ⟹ مُستبعَد.</param>
    /// <param name="progressPercent">التقدّم التنفيذيّ **المعلَن يدويًّا** — <c>null</c> ⟹ مُستبعَد.</param>
    /// <param name="scheduleScore">مكوّن الجدول — <c>null</c> ⟹ مُستبعَد.</param>
    /// <param name="evaluatedAtUtc">ختم التقييم — يُمرَّر ولا يُقرأ من الساعة (نقاء الدالّة).</param>
    /// <param name="kpiWeightsAllZero">هل عوملت المؤشّرات بأوزان متساوية؟ (لإصدار سبب مفسِّر).</param>
    /// <param name="signals">
    /// الإشارات التشغيليّة (P360-WF-R2 §7): الحجب والتأخّر والمخاطر المرتفعة. غيابها يعني
    /// «لا إشارة» فيبقى السلوك مطابقًا لما قبل هذه الجولة — ولذلك هي معامل اختياريّ في الذيل.
    /// </param>
    public static ProjectHealthSnapshot ComputeHealth(
        decimal? kpiScore,
        decimal? progressPercent,
        decimal? scheduleScore,
        DateTime evaluatedAtUtc,
        bool kpiWeightsAllZero = false,
        ProjectOperationalSignals? signals = null)
    {
        var sig = signals ?? ProjectOperationalSignals.None;
        var normalizedKpi = kpiScore is null ? (decimal?)null : Clamp(kpiScore.Value, PercentMin, AchievementMax);
        var normalizedProgress = progressPercent is null ? (decimal?)null : Clamp(progressPercent.Value, PercentMin, PercentMax);
        var normalizedSchedule = scheduleScore is null ? (decimal?)null : Clamp(scheduleScore.Value, PercentMin, PercentMax);

        decimal availableWeight = 0m;
        decimal weightedSum = 0m;

        if (normalizedKpi is not null)
        {
            availableWeight += KpiComponentWeight;
            weightedSum += KpiComponentWeight * normalizedKpi.Value;
        }

        if (normalizedProgress is not null)
        {
            availableWeight += ProgressComponentWeight;
            weightedSum += ProgressComponentWeight * normalizedProgress.Value;
        }

        if (normalizedSchedule is not null)
        {
            availableWeight += ScheduleComponentWeight;
            weightedSum += ScheduleComponentWeight * normalizedSchedule.Value;
        }

        // لا مكوّن رقميّ متاح ⟹ لا نتيجة (لا تُصفَّر) ولا ختم تقييم. **لكنّ الحالة الظاهرة قد لا
        // تكون NotEvaluated**: الحجب حقيقة معلومة بلا معادلة، وإخفاؤها خلف «لم تُقيَّم» تضليل.
        if (availableWeight <= 0m)
        {
            var emptyReasons = new List<ProjectHealthReason>(2)
            {
                new(ProjectHealthReasonCodes.NoComponentAvailable),
            };
            AppendSignalReasons(emptyReasons, sig);

            return new ProjectHealthSnapshot(
                Score: null,
                Status: ResolveStatus(null, sig),
                Reasons: emptyReasons,
                LastEvaluatedAtUtc: null,
                KpiScore: null,
                ProgressPercent: null,
                ScheduleScore: null);
        }

        var score = Round(Clamp(weightedSum / availableWeight, PercentMin, PercentMax));

        return new ProjectHealthSnapshot(
            Score: score,
            Status: ResolveStatus(score, sig, normalizedSchedule),
            Reasons: BuildReasons(normalizedKpi, normalizedProgress, normalizedSchedule, kpiWeightsAllZero, sig),
            LastEvaluatedAtUtc: evaluatedAtUtc,
            KpiScore: normalizedKpi,
            ProgressPercent: normalizedProgress,
            ScheduleScore: normalizedSchedule);
    }

    /// <summary>
    /// اشتقاق رموز الأسباب من نفس المكوّنات التي أنتجت النتيجة — **دالّة نقيّة، صفر تخزين**.
    /// عتبة «دون المستهدَف» = <see cref="GreenThreshold"/> (DN-01).
    /// «متأخّر عن الخطّة» = نتيجة جدول دون <see cref="ScheduleOnTrackScore"/> (فجوة سالبة بنصّ §9-8-د).
    /// </summary>
    private static IReadOnlyList<ProjectHealthReason> BuildReasons(
        decimal? kpiScore,
        decimal? progressPercent,
        decimal? scheduleScore,
        bool kpiWeightsAllZero,
        ProjectOperationalSignals signals)
    {
        var reasons = new List<ProjectHealthReason>(8);

        // الإشارات التشغيليّة تتصدّر القائمة لأنّها **سبب الحالة الظاهرة** لا مجرّد ملاحظة:
        // مَن يقرأ «محجوب» يحتاج عدد الحواجب أوّلًا، لا انحراف المؤشّرات.
        AppendSignalReasons(reasons, signals);

        if (kpiScore is null)
        {
            reasons.Add(new ProjectHealthReason(ProjectHealthReasonCodes.KpiComponentExcluded));
        }
        else
        {
            if (kpiWeightsAllZero)
                reasons.Add(new ProjectHealthReason(ProjectHealthReasonCodes.KpiWeightsAllZero));
            if (kpiScore.Value < GreenThreshold)
                reasons.Add(new ProjectHealthReason(ProjectHealthReasonCodes.KpiScoreBelowTarget, kpiScore.Value));
        }

        if (progressPercent is not null && progressPercent.Value < GreenThreshold)
            reasons.Add(new ProjectHealthReason(ProjectHealthReasonCodes.ProgressBelowTarget, progressPercent.Value));

        if (scheduleScore is null)
            reasons.Add(new ProjectHealthReason(ProjectHealthReasonCodes.ScheduleComponentExcluded));
        else if (scheduleScore.Value < ScheduleOnTrackScore)
            reasons.Add(new ProjectHealthReason(ProjectHealthReasonCodes.ScheduleBehindPlan, scheduleScore.Value));

        if (reasons.Count == 0)
            reasons.Add(new ProjectHealthReason(ProjectHealthReasonCodes.AllComponentsHealthy));

        return reasons;
    }

    /// <summary>
    /// إلحاق أسباب الإشارات التشغيليّة (P360-WF-R2 §7) — **حقائق مُحصاة لا أحكام**:
    /// <c>Detail</c> يحمل العدد كي يقرأ المستخدم «٣ حواجب» لا «محجوب» فقط.
    ///
    /// <para>
    /// <see cref="ProjectProgressMode"/> يُصدِر سببًا **مفسِّرًا** أيضًا: تقدّم بتوزيع متساوٍ رقمٌ
    /// صالح لكنّه ليس الترجيح المقصود، وغياب المخرَجات يفسّر صفرًا لا يعني تعثّرًا.
    /// </para>
    /// </summary>
    private static void AppendSignalReasons(List<ProjectHealthReason> reasons, ProjectOperationalSignals signals)
    {
        if (signals.HasBlocker)
            reasons.Add(new ProjectHealthReason(ProjectHealthReasonCodes.OpenBlocker, signals.OpenBlockerCount));

        if (signals.HasOverdueDeliverable)
            reasons.Add(new ProjectHealthReason(ProjectHealthReasonCodes.OverdueDeliverable, signals.OverdueDeliverableCount));

        if (signals.HasHighRisk)
            reasons.Add(new ProjectHealthReason(ProjectHealthReasonCodes.OpenHighRisk, signals.OpenHighRiskCount));

        switch (signals.ProgressMode)
        {
            case ProjectProgressMode.EqualWeightFallback:
                reasons.Add(new ProjectHealthReason(ProjectHealthReasonCodes.ProgressEqualWeightFallback));
                break;
            case ProjectProgressMode.NoDeliverables:
                reasons.Add(new ProjectHealthReason(ProjectHealthReasonCodes.ProgressNoDeliverables));
                break;
        }
    }
}
