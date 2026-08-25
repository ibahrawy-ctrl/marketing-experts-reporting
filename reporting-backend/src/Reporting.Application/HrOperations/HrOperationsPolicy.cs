namespace Reporting.Application.HrOperations;

/// <summary>
/// P2-HR-009 — المنطق النقيّ للوحة العمليّات (بلا قاعدة بيانات ⇒ قابل للاختبار الوحدويّ مباشرةً).
/// <para>
/// كلّ ما هنا اشتقاق من قيم مُمرَّرة: التقادم، وخرق المهلة، والإجراء التالي، وحدود التصفّح.
/// <b>لا يقرّر هذا الملفّ نطاقًا ولا تخويلًا</b> — النطاق يُفرَض في الخدمة قبل بناء أيّ صفّ.
/// </para>
/// </summary>
public static class HrOperationsPolicy
{
    /// <summary>أقصى حجم صفحة في التفصيل — سقف بنيويّ يمنع سحب الطابور كلّه دفعةً.</summary>
    public const int MaxPageSize = 200;

    /// <summary>حجم الصفحة الافتراضيّ.</summary>
    public const int DefaultPageSize = 25;

    /// <summary>أقصى عدد صفوف في ملفّ تصدير واحد.</summary>
    public const int MaxExportRows = 5000;

    /// <summary>
    /// تقادم البند بالأيّام التقويميّة من لحظة نشأته حتّى الآن.
    /// أيّام تقويميّة لا أيّام عمل: التقادم يقيس <b>كم بقي البند معلّقًا فعلًا</b> لا كم كان يُفترَض العمل عليه.
    /// لحظة مستقبليّة (انحراف ساعة) ⇒ صفر لا سالب.
    /// </summary>
    public static int AgeingDays(DateTime fromUtc, DateTime nowUtc)
    {
        var days = (nowUtc.Date - fromUtc.Date).TotalDays;
        return days <= 0 ? 0 : (int)days;
    }

    /// <summary>
    /// هل خُرِقت المهلة؟ غياب موعد المهلة ⇒ <c>false</c> صراحةً:
    /// <b>«لا مهلة» ليست «مهلة مخروقة»</b>، وتحويل الغياب إلى خرق يضخّم الحرِج زورًا.
    /// </summary>
    public static bool IsBreached(DateTime? slaDueAtUtc, DateTime nowUtc) =>
        slaDueAtUtc is DateTime due && due < nowUtc;

    /// <summary>تطبيع رقم الصفحة (يبدأ من 1).</summary>
    public static int NormalizePage(int page) => page < 1 ? 1 : page;

    /// <summary>تطبيع حجم الصفحة ضمن السقف البنيويّ.</summary>
    public static int NormalizePageSize(int pageSize) =>
        pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

    /// <summary>
    /// الإجراء التالي المتوقَّع لكلّ طابور — نصّ واحد يقرؤه الجدول والتصدير معًا فلا يتفرّق المعنى.
    /// </summary>
    public static string NextActionAr(HrOperationsQueue queue) => queue switch
    {
        HrOperationsQueue.ReportsMissing => "متابعة الموظّف لتقديم التقرير",
        HrOperationsQueue.ReportsLate => "متابعة التأخّر وتوثيق سببه",
        HrOperationsQueue.KpiEvaluationsMissing => "مطالبة المُقيِّم بإكمال التقييم",
        HrOperationsQueue.KpiEvaluationsAwaitingApproval => "اعتماد التقييم أو إعادته للتعديل",
        HrOperationsQueue.KpiCoverageInsufficient => "إسناد قالب تقييم مناسب للموظّف",
        HrOperationsQueue.AttendanceAwaitingEmployee => "انتظار ردّ الموظّف ضمن نافذته",
        HrOperationsQueue.AttendanceEmployeeSlaBreached => "إحالة الواقعة إلى مراجعة الموارد البشريّة",
        HrOperationsQueue.AttendanceAwaitingHr => "مراجعة الواقعة وإصدار القرار",
        HrOperationsQueue.AttendanceHrSlaBreached => "إنهاء المراجعة المتأخّرة فورًا",
        HrOperationsQueue.RequestsAwaitingAction => "البتّ في الطلب في خطوته الحاليّة",
        HrOperationsQueue.FollowUpItems => "متابعة البند حتّى إغلاقه",
        _ => "—"
    };

    /// <summary>
    /// هل يخصّ هذا الطابور وحدة الحضور؟ يُستعمل لإطفاء الطوابير الأربعة حين يكون العلم مطفأً،
    /// بدل إظهار «صفر» يوهم بعدم وجود وقائع.
    /// </summary>
    public static bool IsAttendanceQueue(HrOperationsQueue queue) => queue is
        HrOperationsQueue.AttendanceAwaitingEmployee
        or HrOperationsQueue.AttendanceEmployeeSlaBreached
        or HrOperationsQueue.AttendanceAwaitingHr
        or HrOperationsQueue.AttendanceHrSlaBreached;

    /// <summary>
    /// هل يمرّ الصفّ من مرشِّحات العرض؟ المرشِّح <b>يضيّق فقط</b>: قيمة فارغة = لا تضييق،
    /// ولا يمكن لأيّ منها أن يُدخِل صفًّا لم يكن أصلًا داخل النطاق.
    /// </summary>
    public static bool Matches(HrOperationsRowDto row, HrOperationsFilter filter)
    {
        if (filter.UserId is Guid u && row.SubjectUserId != u) return false;
        if (filter.DepartmentId is Guid d && row.DepartmentId != d) return false;
        if (filter.TeamId is Guid t && row.TeamId != t) return false;
        if (filter.OverdueOnly && !row.SlaBreached) return false;

        if (!string.IsNullOrWhiteSpace(filter.Type)
            && !string.Equals(row.TypeAr, filter.Type, StringComparison.OrdinalIgnoreCase)) return false;

        if (!string.IsNullOrWhiteSpace(filter.Status)
            && !string.Equals(row.StatusAr, filter.Status, StringComparison.OrdinalIgnoreCase)) return false;

        return true;
    }

    /// <summary>
    /// ترتيب موحَّد: المخروق أوّلًا، ثمّ الأقدم تقادمًا، ثمّ الاسم — ترتيب حتميّ كي لا يتغيّر
    /// محتوى الصفحة الثانية بين نداءين على بيانات ثابتة.
    /// </summary>
    public static IOrderedEnumerable<HrOperationsRowDto> Order(IEnumerable<HrOperationsRowDto> rows) =>
        rows.OrderByDescending(r => r.SlaBreached)
            .ThenByDescending(r => r.AgeingDays)
            .ThenBy(r => r.SubjectFullName, StringComparer.Ordinal)
            .ThenBy(r => r.EntityId);
}
