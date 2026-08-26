using Reporting.Application.Checklist;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.UnitTests;

/// <summary>
/// P2-HR-010 — القواعد الخالصة لقائمة الالتزام.
///
/// <para>ما يُثبَت هنا سببه أنّ كلّ خطأ منه يُنتج قرارًا إداريًّا خاطئًا بلا رسالة خطأ:</para>
/// <list type="number">
/// <item><b>«غير منطبق» ليس «صفر»</b> — الصفر يقول «مطلوب ومُنجَز»، وغير المنطبق يقول «لا مطلوب أصلًا».</item>
/// <item><b>غير المنطبق خارج مقام النسبة</b> — وإلّا انخفضت نسبة الالتزام بسبب بنود لا تخصّ الموظّف.</item>
/// <item><b>الكتالوج مقفل ومنفصل</b> — لا مفتاح محسوب قابل للكتابة، ولا تكرار في المفاتيح.</item>
/// </list>
/// </summary>
public class ChecklistPolicyTests
{
    // ═══════════════ ① المحسوب: الحالة تتبع العدّ، والانطباق يسبق العدّ ═══════════════

    [Theory]
    [InlineData(0, EmployeeChecklistStatus.Completed)]
    [InlineData(1, EmployeeChecklistStatus.NotStarted)]
    [InlineData(9, EmployeeChecklistStatus.NotStarted)]
    public void Computed_Status_Follows_Open_Count(int open, EmployeeChecklistStatus expected) =>
        Assert.Equal(expected, ChecklistPolicy.ComputedStatus(open, applicable: true));

    /// <summary>غياب الانطباق يطغى على العدّ: صفرٌ غير منطبق لا يُحسَب إنجازًا.</summary>
    [Fact]
    public void Not_Applicable_Is_Never_Reported_As_Completed()
    {
        Assert.Equal(EmployeeChecklistStatus.NotApplicable,
            ChecklistPolicy.ComputedStatus(0, applicable: false));
        Assert.NotEqual(ChecklistPolicy.ComputedStatus(0, applicable: true),
            ChecklistPolicy.ComputedStatus(0, applicable: false));
    }

    /// <summary>التسمية تفرّق أيضًا — لا يقرأ المستخدم «لا بنود مفتوحة» عن بند لا ينطبق عليه.</summary>
    [Fact]
    public void Computed_Label_Distinguishes_Not_Applicable_From_Zero()
    {
        var zero = ChecklistPolicy.ComputedStatusLabelAr(EmployeeChecklistStatus.Completed, 0);
        var na = ChecklistPolicy.ComputedStatusLabelAr(EmployeeChecklistStatus.NotApplicable, 0);

        Assert.NotEqual(zero, na);
        Assert.Equal("غير منطبق", na);
    }

    // ═══════════════ ② الملخّص: مقام النسبة لا يحمل ما لا ينطبق ═══════════════

    private static ChecklistItemDto Item(string key, EmployeeChecklistStatus status,
        int open = 0, bool mine = false) =>
        new(key, key, "مجموعة", ChecklistItemSource.Computed.ToString(), status,
            ChecklistPolicy.StatusLabelAr(status), open, null, null, null, null, null, null, null, mine);

    [Fact]
    public void Summary_Excludes_Not_Applicable_From_The_Denominator()
    {
        var items = new[]
        {
            Item("a", EmployeeChecklistStatus.Completed),
            Item("b", EmployeeChecklistStatus.NotStarted, open: 2),
            Item("c", EmployeeChecklistStatus.NotApplicable),
            Item("d", EmployeeChecklistStatus.NotApplicable)
        };

        var summary = ChecklistPolicy.Summarize(items);

        Assert.Equal(2, summary.Applicable);     // ليس 4
        Assert.Equal(1, summary.Completed);
        Assert.Equal(1, summary.Open);
        Assert.Equal(2, summary.NotApplicable);
        Assert.Equal(0.5m, summary.CompletionRatio, 3);
    }

    /// <summary>قائمة كلّها «غير منطبق» ⟹ لا قسمة على صفر ولا نسبة كاذبة.</summary>
    [Fact]
    public void Summary_Of_Entirely_Inapplicable_List_Is_Not_A_Division_By_Zero()
    {
        var summary = ChecklistPolicy.Summarize(new[]
        {
            Item("a", EmployeeChecklistStatus.NotApplicable),
            Item("b", EmployeeChecklistStatus.NotApplicable)
        });

        Assert.Equal(0, summary.Applicable);
        Assert.Equal(0m, summary.CompletionRatio);
    }

    [Fact]
    public void Summary_Counts_Items_Requiring_My_Action()
    {
        var summary = ChecklistPolicy.Summarize(new[]
        {
            Item("a", EmployeeChecklistStatus.NotStarted, open: 1, mine: true),
            Item("b", EmployeeChecklistStatus.NotStarted, open: 3, mine: true),
            Item("c", EmployeeChecklistStatus.NotStarted, open: 1)
        });

        Assert.Equal(2, summary.RequiresMyAction);
    }

    // ═══════════════ ③ الترتيب: ما يلزمني فعله أوّلًا ═══════════════

    [Fact]
    public void Ordering_Puts_My_Actions_First_Then_Open_Items()
    {
        var ordered = ChecklistPolicy.Order(new[]
        {
            Item("z-done", EmployeeChecklistStatus.Completed),
            Item("m-open", EmployeeChecklistStatus.NotStarted, open: 1),
            Item("a-mine", EmployeeChecklistStatus.NotStarted, open: 1, mine: true)
        }).Select(i => i.Key).ToList();

        Assert.Equal("a-mine", ordered[0]);
        Assert.Equal("m-open", ordered[1]);
        Assert.Equal("z-done", ordered[2]);
    }

    // ═══════════════ ④ الكتالوج: مقفل، بلا تكرار، وبفصل صارم بين المصدرين ═══════════════

    [Fact]
    public void Catalog_Keys_Are_Unique()
    {
        var keys = ChecklistCatalog.All.Select(d => d.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Catalog_Splits_Cleanly_Into_Computed_And_Manual()
    {
        Assert.Equal(ChecklistCatalog.All.Count,
            ChecklistCatalog.Computed.Count + ChecklistCatalog.Manual.Count);
        Assert.All(ChecklistCatalog.Computed, d => Assert.Equal(ChecklistItemSource.Computed, d.Source));
        Assert.All(ChecklistCatalog.Manual, d => Assert.Equal(ChecklistItemSource.Manual, d.Source));
        Assert.Empty(ChecklistCatalog.Computed.Select(c => c.Key)
            .Intersect(ChecklistCatalog.Manual.Select(m => m.Key), StringComparer.Ordinal));
    }

    /// <summary>
    /// حارس **بنيويّ** ضدّ ازدواج البيانات: لا مفتاح محسوب يُقبَل للكتابة إطلاقًا.
    /// لو صار محسوبٌ قابلًا للكتابة لصار له صفٌّ يناقض مصدره.
    /// </summary>
    [Fact]
    public void No_Computed_Key_Is_Ever_Writable()
    {
        Assert.All(ChecklistCatalog.Computed, d => Assert.False(ChecklistCatalog.IsWritableManualKey(d.Key)));
        Assert.All(ChecklistCatalog.Manual, d => Assert.True(ChecklistCatalog.IsWritableManualKey(d.Key)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("لا-يوجد")]
    [InlineData("Reports-Obligations")] // حسّاس لحالة الأحرف عمدًا: لا مطابقة تقريبيّة لمفتاح أمنيّ
    public void Unknown_Keys_Are_Neither_Found_Nor_Writable(string? key)
    {
        Assert.Null(ChecklistCatalog.Find(key));
        Assert.False(ChecklistCatalog.IsWritableManualKey(key));
    }

    // ═══════════════ ⑤ حالات الكتابة اليدويّة المقبولة ═══════════════

    [Theory]
    [InlineData(EmployeeChecklistStatus.NotStarted, true)]
    [InlineData(EmployeeChecklistStatus.InProgress, true)]
    [InlineData(EmployeeChecklistStatus.Completed, true)]
    [InlineData(EmployeeChecklistStatus.NotApplicable, true)]
    [InlineData((EmployeeChecklistStatus)99, false)]
    public void Manual_Status_Validation_Rejects_Undefined_Values(
        EmployeeChecklistStatus status, bool valid) =>
        Assert.Equal(valid, ChecklistPolicy.IsValidManualStatus(status));
}
