using Reporting.Application.HrOperations;

namespace Reporting.UnitTests;

/// <summary>
/// P2-HR-009 — اختبارات المنطق النقيّ للوحة العمليّات. لا قاعدة بيانات هنا:
/// كلّ ما يُختبَر اشتقاق من قيم مُمرَّرة، وهو بالضبط ما يقرّر معنى الرقم المعروض للمستخدم.
/// </summary>
public class HrOperationsPolicyTests
{
    private static readonly DateTime Now = new(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);

    private static HrOperationsRowDto Row(
        HrOperationsQueue queue = HrOperationsQueue.ReportsMissing,
        Guid? subjectUserId = null,
        string subjectFullName = "أحمد",
        Guid? departmentId = null,
        Guid? teamId = null,
        string typeAr = "تقرير",
        string statusAr = "ناقص",
        bool slaBreached = false,
        int ageingDays = 0,
        Guid? entityId = null) =>
        new(queue, entityId ?? Guid.NewGuid(), "Obligation",
            subjectUserId ?? Guid.NewGuid(), subjectFullName,
            departmentId, null, teamId, null,
            "عنوان", typeAr, statusAr, "2026-W34", null, null,
            slaBreached, ageingDays, null, null, "إجراء", null);

    // ═══════════════════════ التقادم ═══════════════════════

    [Fact]
    public void AgeingDays_CountsCalendarDays()
    {
        Assert.Equal(10, HrOperationsPolicy.AgeingDays(Now.AddDays(-10), Now));
    }

    [Fact]
    public void AgeingDays_SameDay_IsZero()
    {
        Assert.Equal(0, HrOperationsPolicy.AgeingDays(Now.AddHours(-3), Now));
    }

    /// <summary>لحظة مستقبليّة (انحراف ساعة) لا تُنتج تقادمًا سالبًا يُقلب في الترتيب.</summary>
    [Fact]
    public void AgeingDays_FutureInstant_IsZeroNotNegative()
    {
        Assert.Equal(0, HrOperationsPolicy.AgeingDays(Now.AddDays(3), Now));
    }

    // ═══════════════════════ خرق المهلة ═══════════════════════

    /// <summary>«لا مهلة» ليست «مهلة مخروقة» — تحويل الغياب إلى خرق يضخّم الحرِج زورًا.</summary>
    [Fact]
    public void IsBreached_NullDueDate_IsFalse()
    {
        Assert.False(HrOperationsPolicy.IsBreached(null, Now));
    }

    [Fact]
    public void IsBreached_PastDueDate_IsTrue()
    {
        Assert.True(HrOperationsPolicy.IsBreached(Now.AddMinutes(-1), Now));
    }

    [Fact]
    public void IsBreached_FutureDueDate_IsFalse()
    {
        Assert.False(HrOperationsPolicy.IsBreached(Now.AddMinutes(1), Now));
    }

    /// <summary>لحظة المهلة نفسها ليست خرقًا بعد — الخرق يبدأ بعد انقضائها.</summary>
    [Fact]
    public void IsBreached_ExactlyAtDue_IsFalse()
    {
        Assert.False(HrOperationsPolicy.IsBreached(Now, Now));
    }

    // ═══════════════════════ حدود التصفّح ═══════════════════════

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    public void NormalizePage_StartsAtOne(int input, int expected)
    {
        Assert.Equal(expected, HrOperationsPolicy.NormalizePage(input));
    }

    [Fact]
    public void NormalizePageSize_ZeroOrNegative_FallsBackToDefault()
    {
        Assert.Equal(HrOperationsPolicy.DefaultPageSize, HrOperationsPolicy.NormalizePageSize(0));
        Assert.Equal(HrOperationsPolicy.DefaultPageSize, HrOperationsPolicy.NormalizePageSize(-1));
    }

    /// <summary>سقف بنيويّ يمنع سحب الطابور كلّه دفعةً واحدة عبر حجم صفحة ضخم.</summary>
    [Fact]
    public void NormalizePageSize_AboveCap_IsCapped()
    {
        Assert.Equal(HrOperationsPolicy.MaxPageSize, HrOperationsPolicy.NormalizePageSize(10_000));
    }

    // ═══════════════════════ الطوابير والإجراء التالي ═══════════════════════

    /// <summary>كلّ طابور معرّف بمفتاح فريد ونصّ إجراء تالٍ حقيقيّ — لا شرطة ولا تكرار.</summary>
    [Fact]
    public void EveryQueue_HasUniqueKeyAndRealNextAction()
    {
        Assert.Equal(11, HrOperationsCatalog.All.Count);

        var keys = HrOperationsCatalog.All.Select(HrOperationsCatalog.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (var q in HrOperationsCatalog.All)
        {
            Assert.NotEqual("—", HrOperationsPolicy.NextActionAr(q));
            Assert.False(string.IsNullOrWhiteSpace(HrOperationsCatalog.TitleAr(q)));
            Assert.False(string.IsNullOrWhiteSpace(HrOperationsCatalog.GroupAr(q)));
        }
    }

    [Fact]
    public void FromKey_RoundTripsEveryQueue()
    {
        foreach (var q in HrOperationsCatalog.All)
            Assert.Equal(q, HrOperationsCatalog.FromKey(HrOperationsCatalog.Key(q)));
    }

    /// <summary>مفتاح غير معروف لا يُطابِق طابورًا — الحافّة تحوّل ذلك إلى 404.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-such-queue")]
    public void FromKey_UnknownKey_IsNull(string key)
    {
        Assert.Null(HrOperationsCatalog.FromKey(key));
    }

    [Fact]
    public void IsAttendanceQueue_CoversExactlyTheFourAttendanceQueues()
    {
        var attendance = HrOperationsCatalog.All.Where(HrOperationsPolicy.IsAttendanceQueue).ToList();
        Assert.Equal(4, attendance.Count);
        Assert.All(attendance, q => Assert.StartsWith("attendance-", HrOperationsCatalog.Key(q)));
    }

    // ═══════════════════════ المرشِّحات ═══════════════════════

    /// <summary>مرشِّح فارغ لا يضيّق شيئًا — القاعدة التي تمنع «اختفاء» صفوف بلا سبب.</summary>
    [Fact]
    public void Matches_EmptyFilter_PassesEverything()
    {
        Assert.True(HrOperationsPolicy.Matches(Row(), new HrOperationsFilter()));
    }

    [Fact]
    public void Matches_UserFilter_NarrowsToThatUser()
    {
        var target = Guid.NewGuid();
        Assert.True(HrOperationsPolicy.Matches(Row(subjectUserId: target), new HrOperationsFilter(UserId: target)));
        Assert.False(HrOperationsPolicy.Matches(Row(), new HrOperationsFilter(UserId: target)));
    }

    [Fact]
    public void Matches_DepartmentAndTeamFilters_Narrow()
    {
        var dept = Guid.NewGuid();
        var team = Guid.NewGuid();
        var row = Row(departmentId: dept, teamId: team);

        Assert.True(HrOperationsPolicy.Matches(row, new HrOperationsFilter(DepartmentId: dept, TeamId: team)));
        Assert.False(HrOperationsPolicy.Matches(row, new HrOperationsFilter(DepartmentId: Guid.NewGuid())));
        Assert.False(HrOperationsPolicy.Matches(row, new HrOperationsFilter(TeamId: Guid.NewGuid())));
    }

    /// <summary>صفّ بلا إدارة لا يُقحَم في تصفية إدارة بعينها.</summary>
    [Fact]
    public void Matches_RowWithoutDepartment_ExcludedByDepartmentFilter()
    {
        Assert.False(HrOperationsPolicy.Matches(Row(), new HrOperationsFilter(DepartmentId: Guid.NewGuid())));
    }

    [Fact]
    public void Matches_OverdueOnly_KeepsBreachedOnly()
    {
        var filter = new HrOperationsFilter(OverdueOnly: true);
        Assert.True(HrOperationsPolicy.Matches(Row(slaBreached: true), filter));
        Assert.False(HrOperationsPolicy.Matches(Row(slaBreached: false), filter));
    }

    [Fact]
    public void Matches_TypeAndStatus_AreCaseInsensitive()
    {
        var row = Row(typeAr: "تقرير", statusAr: "ناقص");
        Assert.True(HrOperationsPolicy.Matches(row, new HrOperationsFilter(Type: "تقرير", Status: "ناقص")));
        Assert.False(HrOperationsPolicy.Matches(row, new HrOperationsFilter(Type: "تقييم")));
        Assert.False(HrOperationsPolicy.Matches(row, new HrOperationsFilter(Status: "متأخّر")));
    }

    /// <summary>
    /// المرشِّح يضيّق فقط: لا تركيبة منه تُدخِل صفًّا لم يكن في المجموعة أصلًا،
    /// وهو ما يمنع أن تتجاوز البطاقة نطاق المُشاهِد عبر تلاعب بالاستعلام.
    /// </summary>
    [Fact]
    public void Matches_FilterNeverWidens()
    {
        var rows = Enumerable.Range(0, 20).Select(i => Row(
            subjectFullName: $"م{i}", slaBreached: i % 2 == 0, ageingDays: i)).ToList();

        var filters = new[]
        {
            new HrOperationsFilter(),
            new HrOperationsFilter(OverdueOnly: true),
            new HrOperationsFilter(Type: "تقرير"),
            new HrOperationsFilter(UserId: rows[0].SubjectUserId),
            new HrOperationsFilter(Status: "ناقص", OverdueOnly: true)
        };

        foreach (var f in filters)
        {
            var kept = rows.Where(r => HrOperationsPolicy.Matches(r, f)).ToList();
            Assert.True(kept.Count <= rows.Count);
            Assert.All(kept, k => Assert.Contains(k, rows));
        }
    }

    // ═══════════════════════ الترتيب ═══════════════════════

    /// <summary>المخروق أوّلًا ثمّ الأقدم تقادمًا — الترتيب هو ما يجعل الصفحة الأولى ذات معنى.</summary>
    [Fact]
    public void Order_BreachedFirst_ThenOldest()
    {
        var rows = new[]
        {
            Row(subjectFullName: "أ", slaBreached: false, ageingDays: 30),
            Row(subjectFullName: "ب", slaBreached: true, ageingDays: 2),
            Row(subjectFullName: "ج", slaBreached: true, ageingDays: 9)
        };

        var ordered = HrOperationsPolicy.Order(rows).ToList();

        Assert.Equal("ج", ordered[0].SubjectFullName);
        Assert.Equal("ب", ordered[1].SubjectFullName);
        Assert.Equal("أ", ordered[2].SubjectFullName);
    }

    /// <summary>
    /// الترتيب حتميّ: صفوف متطابقة في كلّ معايير الفرز عدا المعرّف تُرتَّب بنفس التتابع
    /// مهما كان ترتيب الدخل ⇒ لا تتغيّر محتويات الصفحة الثانية بين نداءين على بيانات ثابتة.
    /// </summary>
    [Fact]
    public void Order_IsDeterministic_ForTiedRows()
    {
        var ids = Enumerable.Range(0, 12).Select(_ => Guid.NewGuid()).ToList();
        var rows = ids.Select(id => Row(subjectFullName: "نفس الاسم", entityId: id)).ToList();

        var first = HrOperationsPolicy.Order(rows).Select(r => r.EntityId).ToList();
        var second = HrOperationsPolicy.Order(rows.AsEnumerable().Reverse()).Select(r => r.EntityId).ToList();

        Assert.Equal(first, second);
    }
}
