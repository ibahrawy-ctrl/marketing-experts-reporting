namespace Reporting.Application.Security;

/// <summary>
/// P123-R1 — مفاتيح **توفّر الميزة** كما يقرّرها إعداد الخادم فعلًا (<see cref="Phase2FeatureOptions"/>).
/// <para>
/// **ليست تفويضًا ولا تمنح شيئًا**: وجود المفتاح يعني أنّ السطح مفتوح في هذه البيئة لا أنّ هذا
/// المستخدم يملكه؛ التفويض يبقى بالسياسات وطبقة الرؤية والنطاق خادميًّا. غياب المفتاح يعني أنّ
/// المسار سيردّ 404 حتمًا (إخفاء ميزة)، فلا معنى لعرض رابطه للمستخدم.
/// </para>
/// <para>
/// الغرض الوحيد: أن تعرف الواجهة «ما هو مفتوح» و«ما يملكه المستخدم» من **مصدر واحد** هو عقد
/// المستخدم (<c>/auth/me</c>)، بدل أن تخمّن أحدهما أو تخلط بين «ميزة مغلقة» و«خطأ».
/// </para>
/// </summary>
public static class AppFeatures
{
    /// <summary>ملفّ الموظّف 360 الموحّد ووضع «ملفي».</summary>
    public const string Employee360 = "Employee360";

    /// <summary>وقائع الحضور والالتزام.</summary>
    public const string Attendance = "Attendance";

    /// <summary>لوحة عمليّات الموارد البشريّة وطوابير الإجراءات.</summary>
    public const string HrOperations = "HrOperations";

    /// <summary>قائمة تحقّق خدمة الموظّف والالتزام.</summary>
    public const string EmployeeChecklist = "EmployeeChecklist";

    /// <summary>كلّ المفاتيح المعرّفة — للتحقّق والاختبارات فقط.</summary>
    public static readonly string[] All =
    {
        Employee360, Attendance, HrOperations, EmployeeChecklist
    };

    /// <summary>
    /// المفاتيح **المفتوحة فعلًا** في هذه البيئة، مشتقّة من الأعلام المقروءة من الإعداد لا من قائمة ثابتة.
    /// دالّة نقيّة بلا آثار جانبيّة كي تُختبَر وحدويًّا.
    /// </summary>
    public static string[] EnabledFrom(Phase2FeatureOptions flags)
    {
        var enabled = new List<string>(All.Length);
        if (flags.Employee360Enabled) enabled.Add(Employee360);
        if (flags.AttendanceEnabled) enabled.Add(Attendance);
        if (flags.HrOperationsEnabled) enabled.Add(HrOperations);
        if (flags.EmployeeChecklistEnabled) enabled.Add(EmployeeChecklist);
        return enabled.ToArray();
    }
}
