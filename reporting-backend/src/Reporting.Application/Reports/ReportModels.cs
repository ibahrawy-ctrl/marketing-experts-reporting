using Reporting.Domain.Enums;

namespace Reporting.Application.Reports;

/// <summary>مرشّح موحّد لتقارير المراقبة (فترة/إدارة/فريق).</summary>
public record ReportFilter(
    PeriodType? PeriodType = null,
    string? PeriodKey = null,
    Guid? DepartmentId = null,
    Guid? TeamId = null);

public record SubmissionStatusCount(SubmissionStatus Status, int Count);

/// <summary>صف اكتمال التسليمات على مستوى إدارة.</summary>
public record DepartmentCompletenessRow(
    Guid? DepartmentId,
    string DepartmentName,
    int Total,
    int Closed,
    int Pending,
    decimal CompletionRate);

/// <summary>تقرير اكتمال التسليمات (متابعة دعم الرئيس التنفيذي).</summary>
public record SubmissionCompletenessReport(
    string? PeriodKey,
    int Total,
    int Closed,
    int Pending,
    decimal CompletionRate,
    IReadOnlyList<SubmissionStatusCount> ByStatus,
    IReadOnlyList<DepartmentCompletenessRow> ByDepartment);

/// <summary>
/// صف متابعة التزام التسليم لموظف واحد ضمن أسبوع — <b>بيانات التزام فقط، بلا أيّ محتوى للتقرير</b>.
/// لا إجابات، لا ملاحظات اعتماد، لا تعليقات مدير، لا تقييم، لا إجراءات. يُستخدم في شاشة متابعة HR.
/// </summary>
public record SubmissionComplianceRow(
    Guid UserId,
    string FullName,
    string? DepartmentName,
    string? TeamName,
    string? JobRoleName,
    // هل سلّم التقرير المتوقَّع لهذا الأسبوع (أيّ حالة بعد المسودّة)؟
    bool Submitted,
    // حالة التسليم نصًّا (لم يسلّم / مُرسَل / قيد المراجعة / معتمد / … ) — وصف التزام لا محتوى.
    string StatusLabel,
    // متأخر = (سلّم بعد موعد دوره) أو (لم يسلّم وانقضى موعد دوره). موحّد عبر الخدمتين.
    bool Late,
    // تاريخ التسليم إن وُجد (UTC).
    DateTime? SubmittedAtUtc,
    string PeriodKey,
    // سلّم لكن بعد موعد دوره (جزء من Late، يُحتسب ضمن الالتزام لكن ليس ضمن «في الموعد»).
    bool LateSubmitted = false);

/// <summary>
/// تقرير متابعة التزام التسليم (per-person) لأسبوع — متاح للأدوار المراقِبة + HR.
/// يعكس المتوقَّع مقابل الفعلي (من سلّم/من تأخّر) دون كشف أيّ محتوى تقرير.
/// </summary>
public record SubmissionComplianceReport(
    string PeriodKey,
    string PeriodLabel,
    int Expected,
    int Submitted,
    int NotSubmitted,
    int Late,
    decimal CompletionRate,
    IReadOnlyList<SubmissionComplianceRow> Rows,
    // تفصيل التأخّر: سلّم متأخرًا + لم يسلّم وانقضى موعده = إجمالي Late. + من سلّم في الموعد ونسبته.
    int LateSubmitted = 0,
    int MissingOverdue = 0,
    int OnTime = 0,
    decimal OnTimePercent = 0m);

/// <summary>
/// ملخّص التزام أسبوع واحد (أرقام مجمّعة فقط، بلا صفوف per-person) — لبطاقات اللوحة والصفحة.
/// Compliance% = Submitted/Expected (يشمل المتأخر-المُسلَّم)؛ OnTime% = OnTime/Expected.
/// Late = LateSubmitted + MissingOverdue.
/// </summary>
public record ComplianceSummaryReport(
    string PeriodKey,
    string PeriodLabel,
    int Expected,
    int Submitted,
    int Missing,
    int Late,
    int LateSubmitted,
    int MissingOverdue,
    int OnTime,
    decimal CompliancePercent,
    decimal OnTimePercent);

/// <summary>نقطة اتجاه أسبوعي للالتزام (للرسم الزمني المبسّط).</summary>
public record ComplianceTrendPoint(
    string PeriodKey,
    string PeriodLabel,
    int Expected,
    int Submitted,
    int Late,
    decimal CompliancePercent,
    decimal OnTimePercent);

/// <summary>اتجاه الالتزام عبر آخر N أسابيع (الأقدم → الأحدث) ضمن نطاق المستخدم.</summary>
public record ComplianceTrendReport(
    int Weeks,
    IReadOnlyList<ComplianceTrendPoint> Points);

/// <summary>صفّ «الأكثر تأخّرًا» لقالب/مسمّى وظيفي ضمن أسبوع.</summary>
public record LateByTemplateRow(
    Guid JobRoleId,
    string TemplateTitle,
    string JobRoleName,
    int Expected,
    int Late,
    int Missing,
    decimal LatePercent);

/// <summary>القوالب الأكثر تأخّرًا ضمن أسبوع ونطاق المستخدم (مرتّبة تنازليًّا حسب نسبة التأخّر).</summary>
public record LateByTemplateReport(
    string PeriodKey,
    string PeriodLabel,
    IReadOnlyList<LateByTemplateRow> Rows);

/// <summary>صفّ تجميع الالتزام حسب فريق/إدارة ضمن أسبوع.</summary>
public record ComplianceBreakdownRow(
    Guid? GroupId,
    string GroupName,
    int Expected,
    int Submitted,
    int Late,
    int Missing,
    decimal CompliancePercent,
    decimal OnTimePercent);

/// <summary>تجميع الالتزام حسب البُعد (Team أو Department) ضمن أسبوع ونطاق المستخدم.</summary>
public record ComplianceBreakdownReport(
    string PeriodKey,
    string PeriodLabel,
    string GroupBy,
    IReadOnlyList<ComplianceBreakdownRow> Rows);

/// <summary>صف ملخّص مؤشرات أداء موظف.</summary>
public record KpiSummaryRow(
    Guid SubjectUserId,
    string SubjectName,
    decimal? TotalScore,
    KpiTrend Trend,
    bool IsBelowTarget,
    string PeriodKey);

/// <summary>ملخّص مؤشرات الأداء لفترة.</summary>
public record KpiSummaryReport(
    string? PeriodKey,
    int Evaluated,
    decimal? AverageScore,
    int BelowTarget,
    IReadOnlyList<KpiSummaryRow> Rows);

/// <summary>صف تجميع (Rollup) لأرقام مندوب B2C واحد ضمن الفترة.</summary>
public record B2cRollupRow(
    Guid SubmitterId,
    string Name,
    decimal Leads,
    decimal Calls,
    decimal FollowUps,
    decimal Registrations,
    decimal ClosedDeals,
    decimal TargetRegistrations,
    decimal ConversionRate,
    decimal TargetAchievement,
    bool NeedsFollowUp);

/// <summary>تقرير تجميع مبيعات B2C — يحوّل التقارير الفردية إلى أرقام مجمّعة حسب النطاق.</summary>
public record B2cRollupReport(
    string? PeriodKey,
    int Reporters,
    decimal TotalLeads,
    decimal TotalCalls,
    decimal TotalFollowUps,
    decimal TotalRegistrations,
    decimal TotalClosedDeals,
    decimal TotalTarget,
    decimal OverallConversionRate,
    decimal OverallTargetAchievement,
    B2cRollupRow? Best,
    B2cRollupRow? Worst,
    IReadOnlyList<B2cRollupRow> Rows,
    IReadOnlyList<string> CommonLostReasons,
    // مستوى الرؤية المسموح حسب دور الطالب: self / team / department / summary.
    // وُضِع في النهاية ليبقى التغيير متوافقًا مع المستهلكين الحاليين (إضافة فقط).
    string ViewLevel = "summary",
    // هل يُسمح للدور برؤية صفوف المندوبين التفصيلية؟ (تقليل البيانات خادميًا)
    bool CanViewRows = false);

/// <summary>صف تجميع (Rollup) لأرقام مشتري إعلانات واحد ضمن الفترة. Business-1B.</summary>
public record MediaBuyerRollupRow(
    Guid SubmitterId,
    string Name,
    decimal Spend,
    decimal Leads,
    // CPL يُحتسب آليًا = الإنفاق/الليدز (Auto)؛ أدق من جمع قيم CPL المُبلَّغة.
    decimal Cpl,
    // CTR ومعدل التحويل نِسَب — متوسط القيم المُبلَّغة لهذا المشتري.
    decimal Ctr,
    decimal ConversionRate,
    bool NeedsIntervention);

/// <summary>تقرير تجميع أداء الإعلانات (Media Buyer) — يحوّل تقارير المشترين الفردية إلى أرقام مجمّعة حسب النطاق.</summary>
public record MediaBuyerRollupReport(
    string? PeriodKey,
    int Reporters,
    decimal TotalSpend,
    decimal TotalLeads,
    // CPL الإجمالي يُحتسب آليًا = إجمالي الإنفاق / إجمالي الليدز.
    decimal OverallCpl,
    // متوسط CTR ومعدل التحويل عبر المشترين.
    decimal AverageCtr,
    decimal AverageConversionRate,
    MediaBuyerRollupRow? Best,
    MediaBuyerRollupRow? Worst,
    IReadOnlyList<MediaBuyerRollupRow> Rows,
    IReadOnlyList<string> CommonIssueCauses,
    IReadOnlyList<string> DecisionsNeeded,
    // مستوى الرؤية المسموح حسب دور الطالب: self / team / department / summary.
    string ViewLevel = "summary",
    // هل يُسمح للدور برؤية صفوف المشترين التفصيلية؟ (تقليل البيانات خادميًا)
    bool CanViewRows = false);

/// <summary>صف تجميع (Rollup) لأرقام أخصائي SEO واحد ضمن الفترة. Business-1C.</summary>
public record SeoRollupRow(
    Guid SubmitterId,
    string Name,
    decimal ImprovedKeywords,
    decimal DeclinedKeywords,
    // صافي تحسّن الكلمات يُحتسب آليًا = تحسّنت − تراجعت (Auto من حقول يدوية).
    decimal NetKeywords,
    decimal TasksDone,
    decimal TechnicalIssues,
    decimal IndexedPages,
    // Organic Traffic حقل يدوي — يُعرض ويُجمَّع لكنه ليس مصدرًا آليًا دقيقًا (يحتاج GSC/GA مستقبلًا).
    decimal OrganicTraffic,
    decimal ArticlesPlanned,
    decimal ArticlesPublished,
    decimal ArticlesLate,
    // معدّل إنجاز المحتوى لهذا العضو = منشورة/مخطّط لها (٪).
    decimal ContentDeliveryRate,
    // يحتاج متابعة عندما صافي الكلمات سالب (تراجع أكثر من التحسّن).
    bool NeedsFollowup);

/// <summary>تقرير تجميع أداء SEO — يدمج «🔍 تقرير فريق SEO» و«متابعة مقالات SEO» حسب النطاق. Business-1C.</summary>
public record SeoRollupReport(
    string? PeriodKey,
    int Reporters,
    decimal TotalImprovedKeywords,
    decimal TotalDeclinedKeywords,
    // صافي حركة الكلمات إجمالًا = إجمالي تحسّنت − إجمالي تراجعت.
    decimal NetKeywordMovement,
    decimal TotalTasksDone,
    decimal TotalTechnicalIssues,
    decimal TotalIndexedPages,
    decimal TotalOrganicTraffic,
    decimal TotalArticlesPlanned,
    decimal TotalArticlesPublished,
    decimal TotalArticlesLate,
    // معدّل تسليم المحتوى الإجمالي = إجمالي المنشورة / إجمالي المخطّط لها (٪).
    decimal ContentDeliveryRate,
    // أفضل عضو = أعلى صافي كلمات؛ الأحوج للمتابعة = أدنى صافي كلمات.
    SeoRollupRow? Best,
    SeoRollupRow? Worst,
    IReadOnlyList<SeoRollupRow> Rows,
    IReadOnlyList<string> DecisionsNeeded,
    IReadOnlyList<string> Recommendations,
    // مستوى الرؤية المسموح حسب دور الطالب: self / team / department / summary.
    string ViewLevel = "summary",
    // هل يُسمح للدور برؤية صفوف الأعضاء التفصيلية؟ (تقليل البيانات خادميًا)
    bool CanViewRows = false);

public record ContentWriterRollupRow(
    Guid SubmitterId,
    string Name,
    decimal RequiredPieces,
    decimal DeliveredPieces,
    decimal ApprovedFirstTime,
    decimal LatePieces,
    // المعادة للتعديل تُحتسب آليًا = المسلَّمة − المعتمدة من أول مرة.
    decimal RevisedPieces,
    // نسبة الاعتماد من أول مرة = المعتمدة من أول مرة / المسلَّمة (٪).
    decimal FirstApprovalRate,
    // نسبة التعديلات = المعادة / المسلَّمة (٪).
    decimal RevisionRate,
    // الالتزام بالخطة = متوسط «نسبة تحقيق المخرجات» المُدخلة (٪).
    decimal PlanAdherence,
    // يحتاج متابعة عندما تنخفض نسبة الاعتماد من أول مرة أو يوجد محتوى متأخر.
    bool NeedsFollowup);

/// <summary>تقرير تجميع أداء كاتب المحتوى — من «تقرير كاتب المحتوى الأسبوعي» حسب النطاق. Business-1D-1.</summary>
public record ContentWriterRollupReport(
    string? PeriodKey,
    int Reporters,
    decimal TotalRequired,
    decimal TotalDelivered,
    decimal TotalApprovedFirstTime,
    decimal TotalLate,
    // إجمالي المعادة للتعديل = إجمالي المسلَّمة − إجمالي المعتمدة من أول مرة.
    decimal TotalRevised,
    // معدّل تسليم المحتوى الإجمالي = إجمالي المسلَّمة / إجمالي المطلوبة (٪).
    decimal ContentDeliveryRate,
    // نسبة الاعتماد من أول مرة إجمالًا = إجمالي المعتمدة من أول مرة / إجمالي المسلَّمة (٪).
    decimal FirstApprovalRate,
    // نسبة التعديلات إجمالًا = إجمالي المعادة / إجمالي المسلَّمة (٪).
    decimal RevisionRate,
    // متوسط الالتزام بالخطة عبر المبلّغين (٪).
    decimal AvgPlanAdherence,
    // أفضل كاتب = أعلى نسبة اعتماد من أول مرة؛ الأحوج للمتابعة = أدناها.
    ContentWriterRollupRow? Best,
    ContentWriterRollupRow? Worst,
    IReadOnlyList<ContentWriterRollupRow> Rows,
    // أسباب التأخير المتكررة (نصّي مجمّع).
    IReadOnlyList<string> DelayReasons,
    // التحديات/المخاطر — قرارات مطلوبة (نصّي مجمّع).
    IReadOnlyList<string> DecisionsNeeded,
    // مستوى الرؤية المسموح حسب دور الطالب: self / team / department / summary.
    string ViewLevel = "summary",
    // هل يُسمح للدور برؤية صفوف الأعضاء التفصيلية؟ (تقليل البيانات خادميًا)
    bool CanViewRows = false);

public record DesignerRollupRow(
    Guid SubmitterId,
    string Name,
    decimal RequestedDesigns,
    decimal DeliveredDesigns,
    decimal ApprovedFirstTime,
    decimal LateDesigns,
    decimal PendingReview,
    // المعادة للتعديل تُقرأ مباشرة من حقل «أعيدت للتعديل» (لا تُشتق — القالب أغنى من كاتب المحتوى).
    decimal RevisedDesigns,
    // نسبة الاعتماد من أول مرة = المعتمدة من أول مرة / المسلَّمة (٪).
    decimal FirstApprovalRate,
    // نسبة التعديلات = المعادة / المسلَّمة (٪).
    decimal RevisionRate,
    // نسبة الالتزام بالمواعيد = (المسلَّمة − المتأخرة) / المسلَّمة (٪).
    decimal OnTimeRate,
    // الالتزام بالخطة = متوسط «نسبة تحقيق المخرجات» المُدخلة (٪).
    decimal PlanAdherence,
    // يحتاج متابعة عندما تنخفض نسبة الاعتماد من أول مرة أو يوجد تصاميم متأخرة.
    bool NeedsFollowup);

/// <summary>تقرير تجميع أداء فريق التصميم — من «تقرير فريق التصميم» حسب النطاق. Business-1D-2.</summary>
public record DesignerRollupReport(
    string? PeriodKey,
    int Reporters,
    decimal TotalRequested,
    decimal TotalDelivered,
    decimal TotalApprovedFirstTime,
    decimal TotalLate,
    decimal TotalPendingReview,
    // إجمالي المعادة للتعديل (مجموع حقل «أعيدت للتعديل»).
    decimal TotalRevised,
    // معدّل التسليم الإجمالي = إجمالي المسلَّمة / إجمالي المطلوبة (٪).
    decimal DeliveryRate,
    // نسبة الاعتماد من أول مرة إجمالًا = إجمالي المعتمدة من أول مرة / إجمالي المسلَّمة (٪).
    decimal FirstApprovalRate,
    // نسبة التعديلات إجمالًا = إجمالي المعادة / إجمالي المسلَّمة (٪).
    decimal RevisionRate,
    // نسبة الالتزام بالمواعيد إجمالًا = (إجمالي المسلَّمة − إجمالي المتأخرة) / إجمالي المسلَّمة (٪).
    decimal OnTimeRate,
    // متوسط الالتزام بالخطة عبر المبلّغين (٪).
    decimal AvgPlanAdherence,
    // أفضل مصمّم = أعلى نسبة اعتماد من أول مرة؛ الأحوج للمتابعة = أدناها.
    DesignerRollupRow? Best,
    DesignerRollupRow? Worst,
    IReadOnlyList<DesignerRollupRow> Rows,
    // أسباب التأخير المتكررة (نصّي مجمّع).
    IReadOnlyList<string> DelayReasons,
    // التحديات/المخاطر — مشاكل الهوية ونقص البريف وقرارات مطلوبة (نصّي مجمّع).
    IReadOnlyList<string> DecisionsNeeded,
    // مستوى الرؤية المسموح حسب دور الطالب: self / team / department / summary.
    string ViewLevel = "summary",
    // هل يُسمح للدور برؤية صفوف الأعضاء التفصيلية؟ (تقليل البيانات خادميًا)
    bool CanViewRows = false);

public record VideoRollupRow(
    Guid SubmitterId,
    string Name,
    decimal RequestedVideos,
    decimal DeliveredVideos,
    decimal ApprovedFirstTime,
    decimal LateVideos,
    decimal PendingReview,
    // المعادة للتعديل تُقرأ مباشرة من حقل «أعيدت للتعديل» (لا تُشتق — القالب أغنى من كاتب المحتوى).
    decimal RevisedVideos,
    // نسبة الاعتماد من أول مرة = المعتمدة من أول مرة / المسلَّمة (٪).
    decimal FirstApprovalRate,
    // نسبة التعديلات = المعادة / المسلَّمة (٪).
    decimal RevisionRate,
    // نسبة الالتزام بالمواعيد = (المسلَّمة − المتأخرة) / المسلَّمة (٪).
    decimal OnTimeRate,
    // الالتزام بالخطة = متوسط «نسبة تحقيق المخرجات» المُدخلة (٪).
    decimal PlanAdherence,
    // يحتاج متابعة عندما تنخفض نسبة الاعتماد من أول مرة أو يوجد فيديوهات متأخرة.
    bool NeedsFollowup);

/// <summary>تقرير تجميع أداء فريق الفيديو — من «تقرير فريق الفيديو» حسب النطاق. Business-1D-3.</summary>
public record VideoRollupReport(
    string? PeriodKey,
    int Reporters,
    decimal TotalRequested,
    decimal TotalDelivered,
    decimal TotalApprovedFirstTime,
    decimal TotalLate,
    decimal TotalPendingReview,
    // إجمالي المعادة للتعديل (مجموع حقل «أعيدت للتعديل»).
    decimal TotalRevised,
    // معدّل التسليم الإجمالي = إجمالي المسلَّمة / إجمالي المطلوبة (٪).
    decimal DeliveryRate,
    // نسبة الاعتماد من أول مرة إجمالًا = إجمالي المعتمدة من أول مرة / إجمالي المسلَّمة (٪).
    decimal FirstApprovalRate,
    // نسبة التعديلات إجمالًا = إجمالي المعادة / إجمالي المسلَّمة (٪).
    decimal RevisionRate,
    // نسبة الالتزام بالمواعيد إجمالًا = (إجمالي المسلَّمة − إجمالي المتأخرة) / إجمالي المسلَّمة (٪).
    decimal OnTimeRate,
    // متوسط الالتزام بالخطة عبر المبلّغين (٪).
    decimal AvgPlanAdherence,
    // أفضل عضو فيديو = أعلى نسبة اعتماد من أول مرة؛ الأحوج للمتابعة = أدناها.
    VideoRollupRow? Best,
    VideoRollupRow? Worst,
    IReadOnlyList<VideoRollupRow> Rows,
    // أسباب التأخير المتكررة (نصّي مجمّع).
    IReadOnlyList<string> DelayReasons,
    // التحديات/المخاطر — نقص المواد والبريف ومشاكل التصوير والمونتاج وقرارات مطلوبة (نصّي مجمّع).
    IReadOnlyList<string> DecisionsNeeded,
    // مستوى الرؤية المسموح حسب دور الطالب: self / team / department / summary.
    string ViewLevel = "summary",
    // هل يُسمح للدور برؤية صفوف الأعضاء التفصيلية؟ (تقليل البيانات خادميًا)
    bool CanViewRows = false);

public record ModerationRollupRow(
    Guid SubmitterId,
    string Name,
    decimal IncomingMessages,
    decimal AnsweredMessages,
    // الرسائل غير المعالجة = max(0, الواردة − المُجاب عليها).
    decimal UnhandledMessages,
    // متوسط زمن الرد بالدقائق (الأقل أفضل) — متوسط القيم المُدخلة.
    decimal AvgResponseMinutes,
    decimal ProblematicComments,
    decimal Escalations,
    decimal Complaints,
    decimal ConvertedOpportunities,
    // نسبة الرد = المُجاب عليها / الواردة (٪).
    decimal ResponseRate,
    // يحتاج متابعة عندما تنخفض نسبة الرد أو توجد شكاوى.
    bool NeedsFollowup);

/// <summary>تقرير تجميع أداء المودريشن — من «تقرير المديرشن الأسبوعي» حسب النطاق. Business-1D-4.</summary>
public record ModerationRollupReport(
    string? PeriodKey,
    int Reporters,
    decimal TotalIncoming,
    decimal TotalAnswered,
    decimal TotalUnhandled,
    decimal TotalProblematic,
    decimal TotalEscalations,
    decimal TotalComplaints,
    decimal TotalConverted,
    // نسبة الرد الإجمالية = إجمالي المُجاب عليها / إجمالي الواردة (٪).
    decimal ResponseRate,
    // متوسط سرعة الرد عبر المبلّغين (دقيقة) — الأقل أفضل.
    decimal AvgResponseMinutes,
    // أفضل مودريتر = أعلى نسبة رد؛ الأحوج للمتابعة = أدناها.
    ModerationRollupRow? Best,
    ModerationRollupRow? Worst,
    IReadOnlyList<ModerationRollupRow> Rows,
    // المشكلات/الأسئلة المتكررة (نصّي مجمّع).
    IReadOnlyList<string> RecurringIssues,
    // التوصيات / قرارات مطلوبة / أسباب التصعيد (نصّي مجمّع).
    IReadOnlyList<string> DecisionsNeeded,
    // مستوى الرؤية المسموح حسب دور الطالب: self / team / department / summary.
    string ViewLevel = "summary",
    // هل يُسمح للدور برؤية صفوف الأعضاء التفصيلية؟ (تقليل البيانات خادميًا)
    bool CanViewRows = false);

// ===== Business-1D-5: ملخّص تشغيل السوشيال ميديا الموحّد (Social Media Operations Summary) =====
// لا يقرأ من القوالب مباشرة — يعيد استخدام التجميعات الأربعة (محتوى/تصميم/فيديو/مودريشن) ويبني صورة موحّدة.

/// <summary>ملخّص مسار المحتوى ضمن تجميع عمليات السوشيال.</summary>
public record SocialContentSummary(
    int Reporters,
    decimal Required,
    decimal Delivered,
    decimal FirstApprovalRate,
    int NeedsFollowup);

/// <summary>ملخّص مسار التصميم ضمن تجميع عمليات السوشيال.</summary>
public record SocialDesignSummary(
    int Reporters,
    decimal Requested,
    decimal Delivered,
    decimal FirstApprovalRate,
    decimal Late,
    int NeedsFollowup);

/// <summary>ملخّص مسار الفيديو ضمن تجميع عمليات السوشيال.</summary>
public record SocialVideoSummary(
    int Reporters,
    decimal Requested,
    decimal Delivered,
    decimal FirstApprovalRate,
    decimal Late,
    int NeedsFollowup);

/// <summary>ملخّص مسار المودريشن ضمن تجميع عمليات السوشيال.</summary>
public record SocialModerationSummary(
    int Reporters,
    decimal Incoming,
    decimal Answered,
    decimal ResponseRate,
    decimal AvgResponseMinutes,
    decimal Complaints,
    decimal Escalations);

/// <summary>
/// ملخّص تشغيل السوشيال ميديا الموحّد — يجمع نتائج المحتوى/التصميم/الفيديو/المودريشن في صورة تشغيلية واحدة. Business-1D-5.
/// مؤشرات الخطر والصحة تُحتسب من الإجماليات (المتاحة لكل الأدوار) لا من الصفوف، لذا تعمل للـGM/CEO أيضًا.
/// </summary>
public record SocialOpsRollupReport(
    string? PeriodKey,
    int TotalReporters,
    SocialContentSummary Content,
    SocialDesignSummary Design,
    SocialVideoSummary Video,
    SocialModerationSummary Moderation,
    // مؤشر صحة عام لعمليات السوشيال (0–100) + وصفه النصّي.
    decimal HealthScore,
    string HealthLabel,
    // أكبر خطر حالي (وصف نصّي مختصر).
    string TopRisk,
    // أكثر مسار يحتاج متابعة (أدنى مؤشر صحة).
    string MostNeedsFollowupTrack,
    // أكثر مسار فيه تأخير.
    string MostDelayedTrack,
    // أكثر مسار فيه تعديلات.
    string MostRevisedTrack,
    // أكثر مسار فيه شكاوى أو تصعيد.
    string MostComplaintsTrack,
    // توصية تشغيلية مختصرة.
    string Recommendation,
    // قرار مطلوب من الإدارة العليا إن وُجد (قد يكون فارغًا).
    string? DecisionNeeded,
    // مستوى الرؤية المسموح حسب دور الطالب: self / team / department / summary.
    string ViewLevel = "summary",
    // هل يُسمح للدور برؤية تفاصيل المسارات؟ (تقليل البيانات خادميًا — GM/CEO ملخّص فقط)
    bool CanViewRows = false);

public record SeverityCount(RiskSeverity Severity, int Count);

/// <summary>ملخّص الحوكمة: مخاطر/تصعيدات/احتياجات تدريب/خطط تحسين مفتوحة.</summary>
public record GovernanceSummaryReport(
    int OpenRisks,
    IReadOnlyList<SeverityCount> RisksBySeverity,
    int OpenEscalations,
    int OpenTrainingNeeds,
    int OpenImprovementPlans,
    int OpenDecisions);

// ===== RPT-WORKFLOW-BOTTLENECKS-R1: اختناقات مسار الاعتماد (قراءة فقط، ضمن نطاق المستخدم) =====
// تقرير عالق = حالته انتظار اعتماد (Submitted/ApprovedByDirectManager/ApprovedByNextLevel/Escalated)
// و CurrentApproverId محدَّد و توجد خطوة اعتماد Pending قائمة. عمر المرحلة = الآن − ApprovalStep.CreatedAtUtc
// (أعلى Level Pending). تصنيف المرحلة من دور المعتمِد الحالي: قائد فريق(team_leader,SLA 24h) / مدير(manager,48h)
// / الإدارة العليا(senior_management: GM/CEO/Admin/CeoSupport, 72h). متأخر = العمر > SLA. النطاق عبر ScopeResolver
// (الموظف يرى تقاريره العالقة فقط، القائد فريقه، المدير إدارته، الإدارة العليا الكل). لا توسيع صلاحيات، بلا migration.

/// <summary>ملخّص اختناقات مسار الاعتماد ضمن نطاق المستخدم (أرقام مجمّعة + أبرز مرحلة/معتمِد).</summary>
public record WorkflowBottlenecksSummaryReport(
    int TotalPending,
    int OverduePending,
    double OldestPendingAgeHours,
    double AverageStageAgeHours,
    // أكثر مرحلة بها تقارير عالقة (المفتاح + التسمية)؛ null إن لا يوجد عالق.
    string? StageWithMostPending,
    string? StageWithMostPendingLabel,
    int StageWithMostPendingCount,
    // أكثر معتمِد لديه تقارير عالقة ضمن النطاق؛ null إن لا يوجد عالق.
    Guid? ReviewerWithMostPending,
    string? ReviewerWithMostPendingName,
    int ReviewerWithMostPendingCount);

/// <summary>صفّ توزيع الاختناقات حسب المرحلة (قائد فريق/مدير/الإدارة العليا).</summary>
public record WorkflowBottleneckStageRow(
    string StageKey,
    string StageLabel,
    int PendingCount,
    int OverdueCount,
    double AverageAgeHours,
    double OldestAgeHours,
    int SlaHours);

/// <summary>توزيع الاختناقات حسب المرحلة ضمن نطاق المستخدم.</summary>
public record WorkflowBottlenecksByStageReport(
    IReadOnlyList<WorkflowBottleneckStageRow> Rows);

/// <summary>صفّ توزيع الاختناقات حسب المعتمِد الحالي ضمن النطاق.</summary>
public record WorkflowBottleneckApproverRow(
    Guid ApproverId,
    string ApproverName,
    string ApproverRole,
    string ApproverRoleLabel,
    string StageKey,
    string StageLabel,
    int PendingCount,
    int OverdueCount,
    double AverageAgeHours,
    double OldestAgeHours);

/// <summary>توزيع الاختناقات حسب المعتمِد الحالي ضمن نطاق المستخدم.</summary>
public record WorkflowBottlenecksByApproverReport(
    IReadOnlyList<WorkflowBottleneckApproverRow> Rows);

/// <summary>صفّ تفصيلي لتقرير عالق واحد — وصف موضع التقرير في المسار وعمره مقابل SLA (بلا أيّ محتوى للتقرير).</summary>
public record WorkflowBottleneckDetailRow(
    Guid SubmissionId,
    string TemplateTitle,
    string SubmitterName,
    string? TeamName,
    string? DepartmentName,
    Guid? CurrentApproverId,
    string? CurrentApproverName,
    string? CurrentApproverRole,
    string StageKey,
    string StageLabel,
    SubmissionStatus Status,
    string StatusLabel,
    DateTime? SubmittedAtUtc,
    DateTime StageEnteredAtUtc,
    double AgeHours,
    int SlaHours,
    bool IsOverdue);

/// <summary>تفاصيل التقارير العالقة ضمن النطاق + الفلاتر الاختيارية (stage/teamId/departmentId/approverId/overdueOnly).</summary>
public record WorkflowBottlenecksDetailsReport(
    int Total,
    int Overdue,
    IReadOnlyList<WorkflowBottleneckDetailRow> Rows);
