using Reporting.Application.Common;

namespace Reporting.Application.Reports;

/// <summary>تقارير المراقبة والحوكمة على مستوى الإدارة — مقصورة على الأدوار الإدارية/دعم الرئيس.</summary>
public interface IReportingService
{
    Task<Result<SubmissionCompletenessReport>> SubmissionCompletenessAsync(ReportFilter filter, CancellationToken ct = default);
    Task<Result<KpiSummaryReport>> KpiSummaryAsync(ReportFilter filter, CancellationToken ct = default);
    Task<Result<GovernanceSummaryReport>> GovernanceSummaryAsync(CancellationToken ct = default);

    /// <summary>
    /// تجميع (Rollup) أرقام تقارير مندوبي B2C الفردية ضمن نطاق رؤية المستخدم الحالي.
    /// متاح لأي مستخدم مصادَق؛ النطاق وحده يحدد ما يظهر (الموظف يرى أرقامه فقط، القائد فريقه… إلخ).
    /// </summary>
    Task<Result<B2cRollupReport>> B2cSalesRollupAsync(ReportFilter filter, CancellationToken ct = default);

    /// <summary>
    /// تجميع (Rollup) أرقام تقارير مشتري الإعلانات (Media Buyer) الفردية ضمن نطاق رؤية المستخدم الحالي. Business-1B.
    /// متاح لأي مستخدم مصادَق؛ النطاق وحده يحدد ما يظهر (المشتري يرى أرقامه فقط، مدير الأداء فريقه، الإدارة العليا ملخّص فقط).
    /// </summary>
    Task<Result<MediaBuyerRollupReport>> MediaBuyerRollupAsync(ReportFilter filter, CancellationToken ct = default);

    /// <summary>
    /// تجميع (Rollup) أرقام تقارير SEO الفردية (الكلمات/المهام/المشاكل/المقالات) ضمن نطاق رؤية المستخدم الحالي. Business-1C.
    /// يدمج «🔍 تقرير فريق SEO» و«متابعة مقالات SEO الأسبوعي». متاح لأي مستخدم مصادَق؛ النطاق وحده يحدد ما يظهر
    /// (الأخصائي يرى أرقامه، قائد SEO فريقه، مدير التخطيط إدارته، الإدارة العليا ملخّص فقط بلا صفوف). لا تكامل خارجي (GSC/GA).
    /// </summary>
    Task<Result<SeoRollupReport>> SeoRollupAsync(ReportFilter filter, CancellationToken ct = default);

    /// <summary>
    /// تجميع (Rollup) أرقام تقارير كاتب المحتوى (المطلوبة/المسلَّمة/المعتمدة من أول مرة/المتأخرة) ضمن نطاق المستخدم الحالي. Business-1D-1.
    /// يعتمد على «تقرير كاتب المحتوى الأسبوعي». متاح لأي مستخدم مصادَق؛ النطاق وحده يحدد ما يظهر
    /// (الكاتب يرى أرقامه، قائد السوشيال فريقه، مدير التخطيط إدارته، الإدارة العليا ملخّص فقط بلا صفوف).
    /// </summary>
    Task<Result<ContentWriterRollupReport>> ContentWriterRollupAsync(ReportFilter filter, CancellationToken ct = default);

    /// <summary>
    /// تجميع (Rollup) أرقام تقارير فريق التصميم (المطلوبة/المسلَّمة/المعتمدة من أول مرة/المتأخرة/المعادة) ضمن نطاق المستخدم الحالي. Business-1D-2.
    /// يعتمد على «تقرير فريق التصميم». متاح لأي مستخدم مصادَق؛ النطاق وحده يحدد ما يظهر
    /// (المصمّم يرى أرقامه، قائد السوشيال فريقه، مدير التخطيط إدارته، الإدارة العليا ملخّص فقط بلا صفوف).
    /// </summary>
    Task<Result<DesignerRollupReport>> DesignerRollupAsync(ReportFilter filter, CancellationToken ct = default);

    /// <summary>
    /// تجميع (Rollup) أداء فريق الفيديو من «تقرير فريق الفيديو» حسب النطاق. Business-1D-3.
    /// متاح لأي مستخدم مصادَق؛ النطاق وحده يحدد ما يظهر
    /// (عضو الفيديو يرى أرقامه، قائد السوشيال فريقه، مدير التخطيط إدارته، الإدارة العليا ملخّص فقط بلا صفوف).
    /// </summary>
    Task<Result<VideoRollupReport>> VideoRollupAsync(ReportFilter filter, CancellationToken ct = default);

    /// <summary>
    /// تجميع (Rollup) أداء المودريشن من «تقرير المديرشن الأسبوعي» حسب النطاق. Business-1D-4.
    /// المودريشن يقيس المتابعة وسرعة وجودة الاستجابة والتصعيد والشكاوى لا إنتاج قطع.
    /// متاح لأي مستخدم مصادَق؛ النطاق وحده يحدد ما يظهر
    /// (المودريتر يرى أرقامه، قائد السوشيال فريقه، مدير التخطيط إدارته، الإدارة العليا ملخّص فقط بلا صفوف).
    /// </summary>
    Task<Result<ModerationRollupReport>> ModerationRollupAsync(ReportFilter filter, CancellationToken ct = default);

    /// <summary>
    /// ملخّص تشغيل السوشيال ميديا الموحّد — يعيد استخدام تجميعات المحتوى/التصميم/الفيديو/المودريشن ويبني صورة تشغيلية واحدة. Business-1D-5.
    /// لا قالب/مؤشر جديد؛ لا تكرار للحسابات. متاح لأي مستخدم مصادَق؛ النطاق وحده يحدد ما يظهر
    /// (قائد السوشيال فريقه، مدير التخطيط إدارته، المدير العام والرئيس التنفيذي ملخّصًا تنفيذيًا فقط بلا تفاصيل أعضاء).
    /// </summary>
    Task<Result<SocialOpsRollupReport>> SocialOpsRollupAsync(ReportFilter filter, CancellationToken ct = default);

    /// <summary>تصدير تفصيل التسليمات بصيغة CSV (UTF-8 مع BOM لدعم العربية في Excel).</summary>
    Task<Result<byte[]>> ExportSubmissionsCsvAsync(ReportFilter filter, CancellationToken ct = default);

    /// <summary>تصدير تقرير اكتمال التقارير بصيغة PDF (هوية خبراء التسويق، RTL).</summary>
    Task<Result<byte[]>> ExportCompletenessPdfAsync(ReportFilter filter, CancellationToken ct = default);

    /// <summary>تصدير ملخص مؤشرات الأداء بصيغة PDF.</summary>
    Task<Result<byte[]>> ExportKpiSummaryPdfAsync(ReportFilter filter, CancellationToken ct = default);

    /// <summary>تصدير الملخص التنفيذي (اكتمال + مؤشرات + حوكمة) بصيغة PDF — لتقارير المدير العام/الرئيس التنفيذي.</summary>
    Task<Result<byte[]>> ExportExecutiveSummaryPdfAsync(ReportFilter filter, CancellationToken ct = default);
}
