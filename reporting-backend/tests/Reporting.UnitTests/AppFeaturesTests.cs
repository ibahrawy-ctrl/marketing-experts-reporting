using Reporting.Application.Security;
using Xunit;

namespace Reporting.UnitTests;

/// <summary>
/// P123-R1 — اشتقاق «الميزات المفتوحة» من إعداد الخادم.
///
/// <para>
/// هذه الدالّة هي **المفصل الوحيد** بين إعداد البيئة وما تعرفه الواجهة. خطأ فيها لا يظهر
/// كخطأ: يظهر رابطًا يقود إلى 404 دائم يقرأه المستخدم «عطلًا»، أو يُخفي سطحًا مفتوحًا فعلًا.
/// لذلك تُختبَر كلّ خانة على حدة لا الحالتان الطرفيّتان وحدهما.
/// </para>
/// </summary>
public class AppFeaturesTests
{
    [Fact]
    public void All_Flags_Off_Yields_No_Features()
    {
        // الافتراضيّ الخادميّ (وهو حال الإنتاج): لا ميزة مفتوحة ⇒ لا سطح مشروط يُعرَض.
        Assert.Empty(AppFeatures.EnabledFrom(new Phase2FeatureOptions()));
    }

    [Fact]
    public void All_Flags_On_Yields_Exactly_The_Declared_Keys()
    {
        var flags = new Phase2FeatureOptions
        {
            Employee360Enabled = true,
            AttendanceEnabled = true,
            HrOperationsEnabled = true,
            EmployeeChecklistEnabled = true
        };

        Assert.Equal(AppFeatures.All.OrderBy(k => k), AppFeatures.EnabledFrom(flags).OrderBy(k => k));
    }

    [Theory]
    [InlineData(true, false, false, false, AppFeatures.Employee360)]
    [InlineData(false, true, false, false, AppFeatures.Attendance)]
    [InlineData(false, false, true, false, AppFeatures.HrOperations)]
    [InlineData(false, false, false, true, AppFeatures.EmployeeChecklist)]
    public void Each_Flag_Controls_Its_Own_Key_And_No_Other(
        bool emp360, bool attendance, bool hrOps, bool checklist, string expected)
    {
        var flags = new Phase2FeatureOptions
        {
            Employee360Enabled = emp360,
            AttendanceEnabled = attendance,
            HrOperationsEnabled = hrOps,
            EmployeeChecklistEnabled = checklist
        };

        // خانة واحدة مرفوعة ⇒ مفتاح واحد بالضبط: لا تسرّب جانبيّ بين الأعلام.
        Assert.Equal(new[] { expected }, AppFeatures.EnabledFrom(flags));
    }

    [Fact]
    public void Sla_Settings_Are_Not_Features()
    {
        // إعدادات المهل ليست أسطحًا تُعرَض؛ خلطها بالميزات كان سيُعلن ميزةً بلا سطح.
        var flags = new Phase2FeatureOptions
        {
            AttendanceEmployeeResponseHours = 72,
            AttendanceHrReviewWorkingDays = 10,
            AttendanceAutoReconcile = true
        };

        Assert.Empty(AppFeatures.EnabledFrom(flags));
    }

    [Fact]
    public void Feature_Keys_Do_Not_Collide_With_Permission_Keys()
    {
        // بُعدان مستقلّان: «هل السطح مفتوح؟» و«هل يملكه المستخدم؟». تشارُك مفتاح بينهما
        // كان سيسمح لواجهة أن تقرأ أحدهما مكان الآخر فتُظهر سطحًا يردّه الخادم.
        Assert.Empty(AppFeatures.All.Intersect(AppPermissions.All));
    }
}
