namespace Reporting.Application.Common;

/// <summary>
/// REPORT-EXPECTED-SUBMISSION-STATUS-R1 — مصدر تحديد «أرضيّة الانطباق» (Applicability Floor) لدورة تقرير.
/// أرضيّة الانطباق = أوّل لحظة يصبح فيها الموظّف مطالَبًا فعلًا بتقرير هذا القالب.
/// تُحسَب كأقصى (MAX) ثلاث مكوّنات محافِظة (لا يُطالَب الموظّف قبل أن تتوفّر كلّها):
///   (1) أوّل نشر لنسخة من القالب (FirstPublishedVersionAt) — القالب لم يكن موجودًا قبله.
///   (2) تاريخ إنشاء المستخدم (UserCreatedAt) — لم يكن موظّفًا في النظام قبله.
///   (3) تاريخ إسناد المسمّى الوظيفيّ الموثَّق (AuditedJobRoleAssignedAt) — إن توفّر بثقة من سجلّ التدقيق.
/// دالّة نقيّة (Pure) بلا I/O. تعتمد <see cref="ReportingCalendarPolicy"/> لاشتقاق أوّل دورة تشغيليّة مؤهَّلة.
/// القاعدة الحاسمة: لا التزام رجعيّ داخل الدورة — دورة تبدأ قبل الأرضيّة ليست مطلوبة إطلاقًا؛
/// أوّل دورة مطلوبة = أوّل دورة تبدأ (CycleStart) في/بعد الأرضيّة.
/// </summary>
public static class ApplicabilityFloorPolicy
{
    public readonly record struct FloorInput(
        DateOnly UserCreatedAt,
        DateOnly? TemplateFirstPublishedAt,
        DateOnly? AuditedJobRoleAssignedAt);

    public readonly record struct FloorResult(
        DateOnly Floor,
        ApplicabilitySource Source,
        ApplicabilityConfidence Confidence);

    /// <summary>يشتقّ أرضيّة الانطباق + مصدرها + درجة الثقة من المكوّنات المتوفّرة.</summary>
    public static FloorResult Resolve(in FloorInput input)
    {
        // الأرضيّة تبدأ من إنشاء المستخدم (متوفّر دائمًا) ثم تُرفَع لأقصى مكوّن.
        var floor = input.UserCreatedAt;
        if (input.TemplateFirstPublishedAt is DateOnly pub && pub > floor)
            floor = pub;
        if (input.AuditedJobRoleAssignedAt is DateOnly assigned && assigned > floor)
            floor = assigned;

        // المصدر ودرجة الثقة:
        // - إسناد موثَّق متوفّر ⇒ ثقة عالية (High)، ومصدره AuditedJobRoleAssignment إن كان هو الحاكم.
        // - بلا إسناد موثَّق: إن حكمت أرضيّة نشر القالب ⇒ TemplatePublicationFloor/Medium،
        //   وإن حكم إنشاء المستخدم وحده ⇒ UserCreationFallback/ConservativeFallback.
        // - عند تساوي أكثر من مكوّن عند الأرضيّة ⇒ CombinedConservativeFloor.
        var atUser = input.UserCreatedAt == floor;
        var atPub = input.TemplateFirstPublishedAt is DateOnly p && p == floor;
        var atAssigned = input.AuditedJobRoleAssignedAt is DateOnly a && a == floor;

        var governingCount = (atUser ? 1 : 0) + (atPub ? 1 : 0) + (atAssigned ? 1 : 0);

        ApplicabilitySource source;
        ApplicabilityConfidence confidence;

        if (input.AuditedJobRoleAssignedAt is not null)
        {
            // الإسناد الموثَّق يرفع الثقة إلى عالية بغضّ النظر عن أيّ المكوّنات حَكَم.
            confidence = ApplicabilityConfidence.High;
            if (atAssigned && governingCount == 1)
                source = ApplicabilitySource.AuditedJobRoleAssignment;
            else if (atAssigned)
                source = ApplicabilitySource.CombinedConservativeFloor;
            else if (atPub)
                source = ApplicabilitySource.TemplatePublicationFloor;
            else
                source = ApplicabilitySource.UserCreationFallback;
        }
        else if (governingCount > 1)
        {
            source = ApplicabilitySource.CombinedConservativeFloor;
            confidence = ApplicabilityConfidence.Medium;
        }
        else if (atPub)
        {
            source = ApplicabilitySource.TemplatePublicationFloor;
            confidence = ApplicabilityConfidence.Medium;
        }
        else
        {
            source = ApplicabilitySource.UserCreationFallback;
            confidence = ApplicabilityConfidence.ConservativeFallback;
        }

        return new FloorResult(floor, source, confidence);
    }

    /// <summary>
    /// هل الدورة (المعرَّفة ببداية السبت الخاصّة بها) مطلوبة على أرضيّة الانطباق؟
    /// مطلوبة ⟺ بداية الدورة (السبت) في/بعد الأرضيّة (لا التزام رجعيّ داخل دورة بدأت قبل الأرضيّة).
    /// </summary>
    public static bool IsCycleApplicable(DateOnly cycleStart, DateOnly floor) =>
        cycleStart >= floor;

    /// <summary>
    /// أوّل دورة تشغيليّة مؤهَّلة (CycleKey) على أرضيّة الانطباق:
    /// إن كانت الدورة المحتوية للأرضيّة تبدأ قبلها ⇒ الدورة التالية؛ وإلّا الدورة نفسها.
    /// </summary>
    public static string FirstEligibleCycleKey(DateOnly floor)
    {
        var cycleStart = ReportingCalendarPolicy.CycleStart(floor);
        if (cycleStart < floor)
            cycleStart = cycleStart.AddDays(7);
        return ReportingCalendarPolicy.CycleKeyFor(cycleStart);
    }
}

/// <summary>مصدر أرضيّة الانطباق (لتفسير سبب التاريخ في القراءة والتقارير).</summary>
public enum ApplicabilitySource
{
    /// <summary>حَكَم تاريخ إسناد المسمّى الموثَّق من سجلّ التدقيق.</summary>
    AuditedJobRoleAssignment = 0,

    /// <summary>حَكَم تاريخ إنشاء المستخدم (لا إسناد موثَّق، ولا نشر قالب أحدث).</summary>
    UserCreationFallback = 1,

    /// <summary>حَكَم تاريخ أوّل نشر لنسخة القالب (القالب أحدث من إنشاء المستخدم).</summary>
    TemplatePublicationFloor = 2,

    /// <summary>تساوى أكثر من مكوّن عند الأرضيّة (أرضيّة محافِظة مركّبة).</summary>
    CombinedConservativeFloor = 3
}

/// <summary>درجة الثقة في أرضيّة الانطباق.</summary>
public enum ApplicabilityConfidence
{
    /// <summary>إسناد مسمّى موثَّق متوفّر — ثقة عالية.</summary>
    High = 0,

    /// <summary>أرضيّة نشر القالب أو أرضيّة مركّبة بلا إسناد موثَّق — ثقة متوسطة.</summary>
    Medium = 1,

    /// <summary>الرجوع المحافِظ إلى تاريخ إنشاء المستخدم وحده.</summary>
    ConservativeFallback = 2
}
