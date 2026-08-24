using Reporting.Application.Common;
using Reporting.Application.Periods;
using Xunit;

namespace Reporting.UnitTests;

/// <summary>
/// P1-KPI-002 — اختبارات نقيّة لخدمة الفترات الموحّدة (§9.1 بند 8 و9).
/// تُثبِت: حدود السبت→الجمعة بتوقيت الرياض، آخر أسبوع مكتمل، حدود الشهر/الربع/السنة،
/// السنة الكبيسة، المدى المخصّص، الفترة السابقة المقارِنة، وأنّ الفترة الجارية <c>IsOpen</c>.
/// كلّ الاختبارات بساعة ثابتة ⇒ حتميّة تمامًا بلا اعتماد على الوقت الحقيقيّ.
/// </summary>
public class CanonicalPeriodServiceTests
{
    private sealed class FixedClock : ISystemClock
    {
        public FixedClock(DateTimeOffset now) => UtcNow = now;
        public DateTimeOffset UtcNow { get; }
    }

    // «الآن» المرجعيّ: الثلاثاء 2026-08-18 09:00 UTC (= 12:00 بتوقيت الرياض).
    private static CanonicalPeriodService At(string utcIso) =>
        new(new FixedClock(DateTimeOffset.Parse(utcIso, System.Globalization.CultureInfo.InvariantCulture)));

    private static CanonicalPeriodService Default() => At("2026-08-18T09:00:00Z");

    // (1) الأسبوع: البداية سبت والنهاية جمعة، والحدود UTC مزاحة −3 ساعات عن منتصف ليل الرياض.
    [Fact]
    public void Week_BoundariesAreSaturdayToFriday_InRiyadhTime()
    {
        var r = Default().Resolve(new PeriodRequest(PeriodKinds.Week, "2026-W33"));

        Assert.True(r.Succeeded);
        var p = r.Value!;
        Assert.Equal(DayOfWeek.Saturday, p.Start.DayOfWeek);
        Assert.Equal(DayOfWeek.Friday, p.End.DayOfWeek);
        Assert.Equal(6, p.End.DayNumber - p.Start.DayNumber);
        Assert.Equal("Asia/Riyadh", p.TimeZone);

        // السبت 00:00 بالرياض = الجمعة 21:00 UTC.
        Assert.Equal(new DateTime(p.Start.Year, p.Start.Month, p.Start.Day, 0, 0, 0, DateTimeKind.Utc).AddHours(-3), p.StartUtc);
        // الجمعة 23:59:59.9999999 بالرياض = **الجمعة** 20:59:59.9999999 UTC (طرح 3 ساعات لا يعبر منتصف الليل).
        Assert.Equal(20, p.EndUtc.Hour);
        Assert.Equal(59, p.EndUtc.Minute);
        Assert.Equal(DayOfWeek.Friday, p.EndUtc.DayOfWeek);
        // النافذة تغطّي 7 أيام كاملة تمامًا بلا فجوة ولا تداخل.
        Assert.Equal(TimeSpan.FromDays(7), p.EndUtc.AddTicks(1) - p.StartUtc);
    }

    // (2) مفتاح أسبوع غير صالح يُرفَض برمز خطأ واضح ولا يسقط إلى فترة افتراضيّة صامتة.
    [Theory]
    [InlineData("2026-W99")]
    [InlineData("2026-08")]
    [InlineData("غير-صالح")]
    [InlineData(null)]
    public void Week_InvalidKey_FailsExplicitly(string? key)
    {
        var r = Default().Resolve(new PeriodRequest(PeriodKinds.Week, key));
        Assert.False(r.Succeeded);
        Assert.Equal("period.week_key_invalid", r.ErrorCode);
    }

    // (3) آخر أسبوع مكتمل = الأسبوع السابق للأسبوع الجاري، ويجب أن يكون **مغلقًا** (IsOpen=false).
    [Fact]
    public void LastCompletedWeek_IsPreviousWeek_AndClosed()
    {
        var svc = Default();
        var last = svc.LastCompletedWeek();
        var current = svc.Resolve(new PeriodRequest(
            PeriodKinds.Week, ReportingCalendarPolicy.CycleKeyFor(new DateOnly(2026, 8, 18)))).Value!;

        Assert.False(last.IsOpen);
        Assert.True(current.IsOpen);
        Assert.Equal(7, current.Start.DayNumber - last.Start.DayNumber);
    }

    // (4) LastCompletedWeek يُحلّ عبر Resolve أيضًا (اسم رمزيّ لا يحتاج مفتاحًا).
    [Fact]
    public void LastCompletedWeek_ResolvableBySymbolicName()
    {
        var r = Default().Resolve(new PeriodRequest(PeriodKinds.LastCompletedWeek));
        Assert.True(r.Succeeded);
        Assert.Equal(PeriodKinds.Week, r.Value!.Type);
        Assert.False(r.Value.IsOpen);
    }

    // (5) حدود الشهر/الربع/السنة.
    [Fact]
    public void Month_Quarter_Year_Boundaries()
    {
        var svc = Default();

        var m = svc.Resolve(new PeriodRequest(PeriodKinds.Month, "2026-06")).Value!;
        Assert.Equal(new DateOnly(2026, 6, 1), m.Start);
        Assert.Equal(new DateOnly(2026, 6, 30), m.End);

        var q = svc.Resolve(new PeriodRequest(PeriodKinds.Quarter, "2026-Q2")).Value!;
        Assert.Equal(new DateOnly(2026, 4, 1), q.Start);
        Assert.Equal(new DateOnly(2026, 6, 30), q.End);

        var y = svc.Resolve(new PeriodRequest(PeriodKinds.Year, "2026")).Value!;
        Assert.Equal(new DateOnly(2026, 1, 1), y.Start);
        Assert.Equal(new DateOnly(2026, 12, 31), y.End);
    }

    // (6) السنة الكبيسة: فبراير 2028 يجب أن ينتهي في 29 لا 28.
    [Fact]
    public void LeapYear_FebruaryHas29Days()
    {
        var m = At("2029-01-01T00:00:00Z").Resolve(new PeriodRequest(PeriodKinds.Month, "2028-02")).Value!;
        Assert.Equal(new DateOnly(2028, 2, 29), m.End);
        Assert.Equal(29, m.End.DayNumber - m.Start.DayNumber + 1);
    }

    // (7) المدى المخصّص: يُقبل الصحيح، ويُرفض المعكوس والناقص.
    [Fact]
    public void Custom_ValidatesRange()
    {
        var svc = Default();
        var ok = svc.Resolve(new PeriodRequest(
            PeriodKinds.Custom, From: new DateOnly(2026, 5, 1), To: new DateOnly(2026, 5, 31)));
        Assert.True(ok.Succeeded);
        Assert.Equal("2026-05-01..2026-05-31", ok.Value!.Key);

        var reversed = svc.Resolve(new PeriodRequest(
            PeriodKinds.Custom, From: new DateOnly(2026, 5, 31), To: new DateOnly(2026, 5, 1)));
        Assert.False(reversed.Succeeded);
        Assert.Equal("period.range_invalid", reversed.ErrorCode);

        var missing = svc.Resolve(new PeriodRequest(PeriodKinds.Custom));
        Assert.False(missing.Succeeded);
        Assert.Equal("period.range_required", missing.ErrorCode);
    }

    // (8) الفترة السابقة المقارِنة لكلّ نوع.
    [Fact]
    public void PreviousComparable_ShiftsByOneUnit()
    {
        var svc = Default();

        var week = svc.Resolve(new PeriodRequest(PeriodKinds.Week, "2026-W33")).Value!;
        Assert.Equal(7, week.Start.DayNumber - svc.PreviousComparable(week).Start.DayNumber);

        var month = svc.Resolve(new PeriodRequest(PeriodKinds.Month, "2026-01")).Value!;
        Assert.Equal("2025-12", svc.PreviousComparable(month).Key);

        var quarter = svc.Resolve(new PeriodRequest(PeriodKinds.Quarter, "2026-Q1")).Value!;
        Assert.Equal("2025-Q4", svc.PreviousComparable(quarter).Key);

        var year = svc.Resolve(new PeriodRequest(PeriodKinds.Year, "2026")).Value!;
        Assert.Equal("2025", svc.PreviousComparable(year).Key);

        // المخصّص يُزاح بطول المدى نفسه (31 يومًا) فيبقى القياس متكافئًا.
        var custom = svc.Resolve(new PeriodRequest(
            PeriodKinds.Custom, From: new DateOnly(2026, 5, 1), To: new DateOnly(2026, 5, 31))).Value!;
        var prev = svc.PreviousComparable(custom);
        Assert.Equal(new DateOnly(2026, 3, 31), prev.Start);
        Assert.Equal(new DateOnly(2026, 4, 30), prev.End);
    }

    // (9) الفترة المستقبليّة والجارية مفتوحتان؛ الماضية مغلقة (لا Trend رسميّ من مفتوحة).
    [Fact]
    public void IsOpen_DistinguishesRunningFromCompleted()
    {
        var svc = Default();
        Assert.False(svc.Resolve(new PeriodRequest(PeriodKinds.Month, "2026-07")).Value!.IsOpen);
        Assert.True(svc.Resolve(new PeriodRequest(PeriodKinds.Month, "2026-08")).Value!.IsOpen);
        Assert.True(svc.Resolve(new PeriodRequest(PeriodKinds.Month, "2026-09")).Value!.IsOpen);
    }

    // (10) مفاتيح الأسابيع داخل الشهر: لا تكرار، ولا دورة تُحتسب لشهرين (مرجع الثلاثاء يحسم الانتماء).
    [Fact]
    public void WeekKeysWithin_NoDuplicates_AndNoWeekCountedTwice()
    {
        var svc = Default();
        var june = svc.Resolve(new PeriodRequest(PeriodKinds.Month, "2026-06")).Value!;
        var july = svc.Resolve(new PeriodRequest(PeriodKinds.Month, "2026-07")).Value!;

        var juneKeys = svc.WeekKeysWithin(june);
        var julyKeys = svc.WeekKeysWithin(july);

        Assert.NotEmpty(juneKeys);
        Assert.Equal(juneKeys.Count, juneKeys.Distinct().Count());
        Assert.Empty(juneKeys.Intersect(julyKeys));

        // كل مفتاح مُعاد يجب أن يكون مرجع ثلاثائه داخل حدود الشهر فعلًا.
        foreach (var k in juneKeys)
        {
            var anchor = ReportingCalendarPolicy.TuesdayReference(ReportingCalendarPolicy.CycleRange(k).Start);
            Assert.InRange(anchor, june.Start, june.End);
        }
    }

    // (11) فترة من نوع Week تُعيد مفتاحها وحده (لا توسّع).
    [Fact]
    public void WeekKeysWithin_WeekPeriod_ReturnsItself()
    {
        var svc = Default();
        var w = svc.Resolve(new PeriodRequest(PeriodKinds.Week, "2026-W33")).Value!;
        Assert.Equal(new[] { "2026-W33" }, svc.WeekKeysWithin(w));
    }

    // (12) نوع فترة غير معروف يُرفَض صراحةً (لا سقوط صامت).
    [Fact]
    public void UnknownType_FailsExplicitly()
    {
        var r = Default().Resolve(new PeriodRequest("Fortnight", "2026-W33"));
        Assert.False(r.Succeeded);
        Assert.Equal("period.type_invalid", r.ErrorCode);
    }
}
