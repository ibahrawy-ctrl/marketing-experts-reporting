namespace Reporting.Application.Common;

/// <summary>
/// PROJECT-FIRST-EXECUTION-AGGREGATION-CONTRACT-R1 — العقد القياسي الموحّد لمحرّك التجميع Project-First (قراءة فقط).
/// مصدر الحقيقة الوحيد لعناوين قوالب التنفيذ الأربعة، وخرائط المفاتيح الحقيقية (v5) داخل قسم المشاريع المتكرّر
/// (ProjectRepeatableSection)، وتطبيع حالة المشروع (RAG ثلاثيّ). كل الأرقام التشغيلية تُخزَّن <b>داخل كل مشروع</b>
/// في answers (لا top-level). هذا الملف يصف <b>العقد فقط</b> ولا يمسّ بذر القوالب ولا أيّ مسار اعتماد.
///
/// قرار جوهري (Phase 3): القوالب الإنتاجية (محتوى/تصميم/فيديو) تملأ عائلة الإنتاج
/// (Planned/Completed/Approved/Revisions/Delayed) فقط؛ وقالب المديرشن يملأ عائلة المديرشن
/// (MessagesIn/Responses/IssueComments/Escalations) فقط. العائلتان منفصلتان قصدًا كي يبقى المقياس الرئيسي
/// TotalOutput = Completed + Responses بلا ازدواج حسابيّ (الإنتاج يقيس المُنجَز، والمديرشن يقيس الردود).
/// مقياس Published لا مصدر له في v5 ⇒ يبقى في الـDTO لثبات الشكل لكنه دائمًا صفر (موثَّق).
/// </summary>
public static class ProjectFirstExecutionSchema
{
    public const string ContentTitle = "تقرير كاتب المحتوى الأسبوعي";
    public const string DesignTitle = "تقرير فريق التصميم";
    public const string VideoTitle = "تقرير فريق الفيديو";
    public const string ModerationTitle = "تقرير المديرشن الأسبوعي";

    /// <summary>عناوين قوالب التنفيذ الأربعة التي نُقلت أرقامها داخل المشاريع (Path A).</summary>
    public static readonly string[] ExecutionTemplateTitles = { ContentTitle, DesignTitle, VideoTitle, ModerationTitle };

    // ===== تطبيع حالة المشروع (Phase 4) — حقل Select مشترك داخل كل مشروع =====

    /// <summary>مفتاح حقل حالة المشروع (Select) داخل قسم المشاريع المتكرّر.</summary>
    public const string StatusKey = "project_status";

    // القيم الآليّة القياسية لحالة المشروع (RAG ثلاثيّ + غير محدَّد).
    public const string StatusHealthy = "healthy";
    public const string StatusStable = "stable";
    public const string StatusNeedsIntervention = "needs_intervention";
    public const string StatusUnspecified = "unspecified";

    /// <summary>
    /// يطبّع قيمة حقل حالة المشروع (Select) إلى قيمة آليّة قياسية. خيارات v5 (RAG):
    /// «🟢 ممتاز» ⇒ healthy، «🟡 مستقر» ⇒ stable، «🔴 يحتاج تدخل» ⇒ needs_intervention،
    /// وأيّ فراغ/قيمة قديمة غير معروفة ⇒ unspecified. المطابقة على كلمة الحالة (متسامحة مع الرمز اللوني/الفراغات).
    /// </summary>
    public static string NormalizeStatus(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return StatusUnspecified;
        var v = raw.Trim();
        if (v.Contains("ممتاز")) return StatusHealthy;
        if (v.Contains("مستقر")) return StatusStable;
        if (v.Contains("تدخل")) return StatusNeedsIntervention;
        return StatusUnspecified;
    }

    // ===== خرائط المفاتيح الرقمية الحقيقية (v5) لكل قالب =====

    /// <summary>
    /// خريطة مفاتيح مقياس ← مفاتيح مصدر حقيقية (تُجمَع). مصفوفة فارغة ⇒ لا مصدر لهذا المقياس في هذا القالب (⇒ 0).
    /// Published مقصود غيابه (لا مصدر في v5 ⇒ دائمًا صفر).
    /// </summary>
    public sealed record MetricKeyMap(
        string[] Planned,
        string[] Completed,
        string[] Approved,
        string[] Revisions,
        string[] Delayed,
        string[] MessagesIn,
        string[] Responses,
        string[] IssueComments,
        string[] Escalations);

    // محتوى (القطع): planned←required_pieces، completed←delivered_pieces، approved←approved_first_time،
    // revisions←returned_once+returned_more، delayed←late_pieces. عائلة المديرشن فارغة.
    private static readonly MetricKeyMap ContentMap = new(
        Planned: new[] { "required_pieces" },
        Completed: new[] { "delivered_pieces" },
        Approved: new[] { "approved_first_time" },
        Revisions: new[] { "returned_once", "returned_more" },
        Delayed: new[] { "late_pieces" },
        MessagesIn: System.Array.Empty<string>(),
        Responses: System.Array.Empty<string>(),
        IssueComments: System.Array.Empty<string>(),
        Escalations: System.Array.Empty<string>());

    // تصميم: planned←requested_designs، completed←delivered_designs، approved←approved_first_time،
    // revisions←revised_designs، delayed←late_designs.
    private static readonly MetricKeyMap DesignMap = new(
        Planned: new[] { "requested_designs" },
        Completed: new[] { "delivered_designs" },
        Approved: new[] { "approved_first_time" },
        Revisions: new[] { "revised_designs" },
        Delayed: new[] { "late_designs" },
        MessagesIn: System.Array.Empty<string>(),
        Responses: System.Array.Empty<string>(),
        IssueComments: System.Array.Empty<string>(),
        Escalations: System.Array.Empty<string>());

    // فيديو: planned←requested_videos، completed←delivered_videos، approved←approved_first_time،
    // revisions←revised_videos، delayed←late_videos.
    private static readonly MetricKeyMap VideoMap = new(
        Planned: new[] { "requested_videos" },
        Completed: new[] { "delivered_videos" },
        Approved: new[] { "approved_first_time" },
        Revisions: new[] { "revised_videos" },
        Delayed: new[] { "late_videos" },
        MessagesIn: System.Array.Empty<string>(),
        Responses: System.Array.Empty<string>(),
        IssueComments: System.Array.Empty<string>(),
        Escalations: System.Array.Empty<string>());

    // مديرشن: عائلة الإنتاج فارغة قصدًا (لا تُملأ Planned/Completed كي لا يزدوج TotalOutput).
    // messagesIn←incoming_messages، responses←answered_messages، issueComments←complaints، escalations←escalations.
    private static readonly MetricKeyMap ModerationMap = new(
        Planned: System.Array.Empty<string>(),
        Completed: System.Array.Empty<string>(),
        Approved: System.Array.Empty<string>(),
        Revisions: System.Array.Empty<string>(),
        Delayed: System.Array.Empty<string>(),
        MessagesIn: new[] { "incoming_messages" },
        Responses: new[] { "answered_messages" },
        IssueComments: new[] { "complaints" },
        Escalations: new[] { "escalations" });

    /// <summary>خريطة المفاتيح لعنوان قالب تنفيذ؛ null لأيّ عنوان خارج القوالب الأربعة.</summary>
    public static MetricKeyMap? MapFor(string? templateTitle) => templateTitle switch
    {
        ContentTitle => ContentMap,
        DesignTitle => DesignMap,
        VideoTitle => VideoMap,
        ModerationTitle => ModerationMap,
        _ => null,
    };
}
