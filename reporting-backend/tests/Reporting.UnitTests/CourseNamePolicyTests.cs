using Reporting.Application.Common;
using Xunit;

namespace Reporting.UnitTests;

/// <summary>
/// COURSE-DUPLICATE-MERGE-R1 — اختبارات نقيّة لسياسة توحيد اسم الدورة (Strategy B، وقت القراءة/العرض/التجميع).
/// Test A: تطبيع الأسماء البديلة المعتمَدة إلى مفتاح تجميع واحد + اسم عرض موحّد رسميّ.
/// Test B: سلامة الدورات غير المعنيّة (لا دمج، لا تغيير جوهريّ للاسم).
/// </summary>
public class CourseNamePolicyTests
{
    // القيمة الرسمية النهائية المعتمدة (بلا «ال» بادئة).
    private const string Canonical = "دبلوم التسويق الرقمي والنمو";

    public static IEnumerable<object[]> AliasVariants => new List<object[]>
    {
        new object[] { "الدبلوم الشامل" },
        new object[] { "الدبلوم التسويق الرقمي والنمو" },
        new object[] { "دبلوم التسويق الرقمي والنمو" },
        new object[] { "  الدبلوم الشامل  " },                 // مسافات طرفية
        new object[] { "دبلوم   التسويق   الرقمي   والنمو" },   // مسافات داخلية متكرّرة
        new object[] { "\tالدبلوم الشامل\t" },                // tabs
    };

    // ===== Test A — كل الأسماء البديلة المعتمَدة تُوحَّد =====

    [Theory]
    [MemberData(nameof(AliasVariants))]
    public void A_AllAliases_AreRecognizedAsCanonicalAlias(string name)
    {
        Assert.True(CourseNamePolicy.IsAliasOfCanonicalCourse(name));
    }

    [Theory]
    [MemberData(nameof(AliasVariants))]
    public void A_AllAliases_ShareSameGroupingKey(string name)
    {
        var canonicalKey = CourseNamePolicy.NormalizeForGrouping(Canonical);
        Assert.Equal(canonicalKey, CourseNamePolicy.NormalizeForGrouping(name));
    }

    [Theory]
    [MemberData(nameof(AliasVariants))]
    public void A_AllAliases_MapToCanonicalDisplayName(string name)
    {
        Assert.Equal(Canonical, CourseNamePolicy.GetCanonicalDisplayName(name));
    }

    [Fact]
    public void A_CanonicalConstant_IsExactApprovedName_NoLeadingAl()
    {
        Assert.Equal("دبلوم التسويق الرقمي والنمو", CourseNamePolicy.CanonicalDigitalDiploma);
        Assert.DoesNotContain("الدبلوم الشامل", CourseNamePolicy.CanonicalDigitalDiploma);
    }

    // ===== Test B — الدورات غير المعنيّة تبقى كما هي =====

    [Theory]
    [InlineData("الحملات المتقدمة")]
    [InlineData("Google Ads")]
    [InlineData("SEO")]
    public void B_UnrelatedCourses_AreNotAliases(string name)
    {
        Assert.False(CourseNamePolicy.IsAliasOfCanonicalCourse(name));
    }

    [Theory]
    [InlineData("الحملات المتقدمة")]
    [InlineData("Google Ads")]
    [InlineData("SEO")]
    public void B_UnrelatedCourses_KeepDistinctGroupingKey(string name)
    {
        var canonicalKey = CourseNamePolicy.NormalizeForGrouping(Canonical);
        Assert.NotEqual(canonicalKey, CourseNamePolicy.NormalizeForGrouping(name));
    }

    [Theory]
    [InlineData("الحملات المتقدمة")]
    [InlineData("SEO")]
    public void B_UnrelatedCourses_PreserveTrimmedDisplayName(string name)
    {
        Assert.Equal(name, CourseNamePolicy.GetCanonicalDisplayName(name));
    }

    [Fact]
    public void B_UnrelatedCourses_TwoDistinctNames_StayDistinct()
    {
        Assert.NotEqual(
            CourseNamePolicy.NormalizeForGrouping("Google Ads"),
            CourseNamePolicy.NormalizeForGrouping("SEO"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void B_NullOrBlank_IsNotAlias_AndEmptyDisplay(string? name)
    {
        Assert.False(CourseNamePolicy.IsAliasOfCanonicalCourse(name));
        Assert.Equal(string.Empty, CourseNamePolicy.GetCanonicalDisplayName(name));
    }

    [Fact]
    public void B_UnrelatedCourse_CaseAndWhitespaceFolded_ButNotMerged()
    {
        // «Google Ads» بمسافات/حالة مختلفة يبقى نفس المفتاح لكن ليس مفتاح الدورة الموحّدة.
        var k1 = CourseNamePolicy.NormalizeForGrouping("Google   Ads");
        var k2 = CourseNamePolicy.NormalizeForGrouping("  google ads ");
        Assert.Equal(k1, k2);
        Assert.NotEqual(CourseNamePolicy.NormalizeForGrouping(Canonical), k1);
    }
}
