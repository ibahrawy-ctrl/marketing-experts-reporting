using Reporting.Domain.Enums;

namespace Reporting.Application.Projects360;

/// <summary>
/// أكواد أخطاء Project 360 (CPW-R3 · W4) — **مصدر واحد لا سلاسل متناثرة**.
///
/// <para>
/// الربط بحالة HTTP يتمّ آليًّا في <c>ApiControllerBase</c> باللاحقة: <c>.not_found</c> ⟶ 404،
/// <c>.conflict</c> ⟶ 409، وما عداها ⟶ 400. لذلك اللاحقة جزء من العقد لا زينة تسمية.
/// </para>
/// </summary>
public static class Project360ErrorCodes
{
    // ===== المشروع =====
    public const string ProjectNotFound = "project.not_found";

    // ===== الاستراتيجيّة =====
    public const string StrategyFieldCodeInvalid = "project_strategy.field_code_invalid";
    public const string StrategySectionCodeInvalid = "project_strategy.section_code_invalid";
    public const string StrategyDuplicateField = "project_strategy.duplicate_field.conflict";

    /// <summary>رمز حقل صحيح في الكتالوج لكنّه لا ينطبق على نوع خدمة هذا المشروع (R2 §6-5).</summary>
    public const string StrategyFieldNotApplicable = "project_strategy.field_not_applicable";

    // ===== الأهداف =====
    public const string ObjectiveNotFound = "project_objective.not_found";
    public const string ObjectiveHasKpis = "project_objective.has_kpis.conflict";

    // ===== المؤشّرات =====
    public const string KpiNotFound = "project_kpi.not_found";
    public const string KpiObjectiveRequired = "project_kpi.objective_required";
    public const string KpiObjectiveMismatch = "project_kpi.objective_mismatch.conflict";
    public const string KpiSourceNotManual = "project_kpi.source_not_manual.conflict";
    public const string KpiTargetInvalid = "project_kpi.target_invalid";

    // ===== القراءات =====
    public const string ReadingNotFound = "project_kpi_reading.not_found";
    public const string ReadingDuplicateDate = "project_kpi_reading.duplicate_date.conflict";

    // ===== المخرَج التعاقديّ =====
    public const string DeliverableNotFound = "project_deliverable.not_found";
    public const string DeliverableTypeInvalid = "project_deliverable.type_invalid";
    public const string DeliverableTypeImmutable = "project_deliverable.type_immutable.conflict";
    public const string DeliverableObjectiveMismatch = "project_deliverable.objective_mismatch.conflict";
    public const string DeliverableWorkstreamMismatch = "project_deliverable.workstream_mismatch.conflict";
    public const string DeliverableQuantityInvalid = "project_deliverable.quantity_invalid";

    /// <summary>وزن المخرَج خارج المجال 0..100 (P360-WF-R2 §6-1).</summary>
    public const string DeliverableWeightInvalid = "project_deliverable.weight_invalid";

    // ===== تحقّق عامّ مشترك (W4 — ميكانيكيّ، لا يخالف عقد R2) =====
    public const string PercentInvalid = "project_360.percent_invalid";
    public const string WeightInvalid = "project_360.weight_invalid";
    public const string SortOrderInvalid = "project_360.sort_order_invalid";
    public const string DateRangeInvalid = "project_360.date_range_invalid";
    public const string NameRequired = "project_360.name_required";
}

/// <summary>
/// مجالات كتالوج التصنيفات التي يستعملها Project 360 (CPW-R3 · §12).
///
/// <para>
/// **W5 (DEC-W4-01)**: تُبذَر قيم هذه المجالات الثلاثة في <c>ExecutionTaxonomySeeder</c> بالرموز
/// المأخوذة **حرفيًّا من R2 وملحق W1-A** — بلا اختراع رمز واحد. البذر عند الإقلاع فقط،
/// وإعادة تشغيله آمنة (يتخطّى الموجود ولا يحدّثه). لا كتابة يدويّة على قاعدة حيّة في W5.
/// </para>
/// </summary>
public static class Project360CatalogDomains
{
    /// <summary>رموز الحقول الاستراتيجيّة المشروطة بنوع الخدمة.</summary>
    public const string StrategyField = "strategy_field";

    /// <summary>رموز أقسام عرض الحقول الاستراتيجيّة.</summary>
    public const string StrategySection = "strategy_section";

    /// <summary>رموز أنواع المخرَجات التعاقديّة.</summary>
    public const string ContractDeliverable = "contract_deliverable";
}

/// <summary>
/// رموز حقول النواة الثابتة الإحدى عشرة (CPW-R3 · §5-3-أ) — تُستعمل في مخطَّط الاستراتيجيّة
/// (<c>GET strategy schema</c>) لتبني الواجهة نموذجها من بيانات لا من كود.
/// **الترتيب هنا هو ترتيب العرض المعتمد.**
/// </summary>
public static class ProjectStrategyCoreFields
{
    public const string Vision = "vision";
    public const string StrategySummary = "strategy_summary";
    public const string TargetAudience = "target_audience";
    public const string CustomerPersona = "customer_persona";
    public const string Positioning = "positioning";
    public const string ValueProposition = "value_proposition";
    public const string Competitors = "competitors";
    public const string ToneOfVoice = "tone_of_voice";
    public const string Messaging = "messaging";
    public const string MarketingApproach = "marketing_approach";
    public const string SuccessFactors = "success_factors";

    /// <summary>الرموز مقرونة بتسمياتها العربيّة بترتيب العرض المعتمد.</summary>
    public static readonly IReadOnlyList<(string Code, string NameAr)> Ordered = new[]
    {
        (Vision, "الرؤية"),
        (StrategySummary, "ملخّص الاستراتيجيّة"),
        (TargetAudience, "الجمهور المستهدَف"),
        (CustomerPersona, "شخصيّة العميل النموذجيّة"),
        (Positioning, "التموضع"),
        (ValueProposition, "القيمة المقدَّمة"),
        (Competitors, "المنافسون"),
        (ToneOfVoice, "نبرة الصوت"),
        (Messaging, "الرسائل الأساسيّة"),
        (MarketingApproach, "التوجّه التسويقيّ"),
        (SuccessFactors, "عوامل النجاح"),
    };

    /// <summary>عدد حقول النواة الثابتة — يُستعمل في مؤشّر اكتمال الاستراتيجيّة بلوحة النظرة العامّة.</summary>
    public const int Count = 11;
}

/// <summary>
/// **قابليّة تطبيق الحقول الاستراتيجيّة — مُعطاة بالبيانات لا بالتفريع** (قرار المالك DEC-W4-02).
///
/// <para>
/// **القاعدة الوحيدة**: رمز الحقل الشرطيّ يُكتب <c>{sectionCode}.{fieldCode}</c>، والقسم هو
/// البادئة قبل أوّل نقطة. الحقل ينطبق على المشروع إذا كان قسمه ضمن أقسامه المسموحة، وهي:
/// **الأقسام العامّة** (كلّ قسم غير مرتبط بنوع خدمة) **+** القسم المرتبط بنوع خدمة المشروع.
/// </para>
///
/// <para>
/// **لا يوجد <c>if seo</c> ولا <c>if ads</c> ولا صنف استراتيجيّة لكلّ خدمة في أيّ مكان**:
/// إضافة حقل جديد أو قسم جديد = **صفّ كتالوج** (<c>strategy_field</c>/<c>strategy_section</c>)
/// بلا كود ولا هجرة ولا تعديل واجهة. الاستثناء الوحيد الذي يحتاج سطرًا واحدًا هنا هو ربط
/// **قيمة تعداد <c>ServiceType</c> جديدة** بقسمها — وتعداد لغة C# لا يمكن توسيعه ببيانات أصلًا،
/// فالسطر تبعيّة للتعداد لا لمنطق الاستراتيجيّة (R2 §5-4 يقرّ بذلك حرفيًّا: «سطر في الخريطة»).
/// </para>
/// </summary>
public static class ProjectStrategyApplicability
{
    /// <summary>الفاصل بين رمز القسم ورمز الحقل داخل الرمز الكامل.</summary>
    public const char SectionSeparator = '.';

    /// <summary>
    /// نوع الخدمة ⟶ رمز القسم المشروط (R2 §5-4 حرفيًّا). الأنواع غير المذكورة
    /// (<c>Website</c> · <c>Video</c> · <c>Branding</c> · <c>Other</c>) لا قسم مشروط لها،
    /// فترى الأقسام العامّة وحدها — وهو سلوك مقصود لا نقص.
    /// </summary>
    public static readonly IReadOnlyDictionary<ServiceType, string> ServiceSection =
        new Dictionary<ServiceType, string>
        {
            [ServiceType.Seo] = "seo",
            [ServiceType.MediaBuying] = "ads",
            [ServiceType.Social] = "social",
        };

    /// <summary>الأقسام المحجوزة لخدمة بعينها — يُشتقّ من الخريطة نفسها فلا يُكرَّر تعريفه.</summary>
    public static readonly IReadOnlySet<string> ServiceScopedSections =
        ServiceSection.Values.ToHashSet(StringComparer.Ordinal);

    /// <summary>رمز قسم الحقل = ما قبل أوّل نقطة، أو <c>null</c> لرمز بلا بادئة (حقل عامّ).</summary>
    public static string? SectionOf(string fieldCode)
    {
        if (string.IsNullOrWhiteSpace(fieldCode)) return null;
        var i = fieldCode.IndexOf(SectionSeparator);
        return i <= 0 ? null : fieldCode[..i];
    }

    /// <summary>
    /// هل يظهر قسم بهذا الرمز لمشروع بهذا النوع؟ قسم عامّ ⟹ نعم دائمًا؛
    /// وقسم محجوز لخدمة ⟹ فقط إن طابق قسم نوع الخدمة.
    /// </summary>
    public static bool SectionAppliesTo(string? sectionCode, ServiceType serviceType)
    {
        if (sectionCode is null || !ServiceScopedSections.Contains(sectionCode)) return true;
        return ServiceSection.TryGetValue(serviceType, out var allowed) && allowed == sectionCode;
    }

    /// <summary>
    /// هل ينطبق حقل بهذا الرمز على مشروع بهذا النوع؟ حقل بلا بادئة، أو بادئته قسم عامّ ⟹ نعم دائمًا؛
    /// وبادئته قسم خدمة ⟹ فقط إن طابقت قسم نوع الخدمة.
    /// </summary>
    public static bool AppliesTo(string fieldCode, ServiceType serviceType) =>
        SectionAppliesTo(SectionOf(fieldCode), serviceType);
}
