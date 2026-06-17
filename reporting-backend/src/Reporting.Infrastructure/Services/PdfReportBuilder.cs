using System.Reflection;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Reporting.Application.Reports;

namespace Reporting.Infrastructure.Services;

/// <summary>
/// مُولِّد ملفات PDF لتقارير المراقبة بهوية «خبراء التسويق» (Navy/Orange، خط Tajawal، اتجاه RTL).
/// يُحمِّل خط Tajawal المُضمَّن مرة واحدة ويُفعّل ترخيص QuestPDF Community.
/// </summary>
internal static class PdfReportBuilder
{
    private const string Navy = "#243763";
    private const string NavyLight = "#F1F4F9";
    private const string Orange = "#FF6E31";
    private const string Ink = "#1A2030";
    private const string Ink2 = "#52596B";
    private const string Line = "#E6E9F0";
    private const string Success = "#1E9E6A";
    private const string Alert = "#E04141";

    private const string FontFamily = "Tajawal";

    static PdfReportBuilder()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var asm = Assembly.GetExecutingAssembly();
        foreach (var name in asm.GetManifestResourceNames())
        {
            if (name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
            {
                using var s = asm.GetManifestResourceStream(name);
                if (s is not null) FontManager.RegisterFont(s);
            }
        }
    }

    public static byte[] Completeness(SubmissionCompletenessReport r)
        => Document.Create(doc =>
        {
            doc.Page(page =>
            {
                Setup(page);
                page.Header().Element(h => Header(h, "تقرير اكتمال التقارير", r.PeriodKey));
                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(14);
                    CompletenessBody(col, r);
                });
                page.Footer().Element(Footer);
            });
        }).GeneratePdf();

    public static byte[] KpiSummary(KpiSummaryReport r)
        => Document.Create(doc =>
        {
            doc.Page(page =>
            {
                Setup(page);
                page.Header().Element(h => Header(h, "ملخص مؤشرات الأداء", r.PeriodKey));
                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(14);
                    KpiBody(col, r);
                });
                page.Footer().Element(Footer);
            });
        }).GeneratePdf();

    public static byte[] ExecutiveSummary(
        SubmissionCompletenessReport completeness,
        KpiSummaryReport kpi,
        GovernanceSummaryReport? governance,
        string? periodKey)
        => Document.Create(doc =>
        {
            doc.Page(page =>
            {
                Setup(page);
                page.Header().Element(h => Header(h, "الملخص التنفيذي", periodKey));
                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(16);
                    SectionTitle(col, "اكتمال التقارير");
                    CompletenessBody(col, completeness);
                    SectionTitle(col, "مؤشرات الأداء");
                    KpiBody(col, kpi);
                    if (governance is not null)
                    {
                        SectionTitle(col, "الحوكمة");
                        GovernanceBody(col, governance);
                    }
                });
                page.Footer().Element(Footer);
            });
        }).GeneratePdf();

    // ── أقسام محتوى ──────────────────────────────────────────────────────

    private static void CompletenessBody(ColumnDescriptor col, SubmissionCompletenessReport r)
    {
        col.Item().Row(row =>
        {
            row.Spacing(8);
            Metric(row, "الإجمالي", r.Total.ToString(), Navy);
            Metric(row, "مُغلقة", r.Closed.ToString(), Success);
            Metric(row, "قيد الإنجاز", r.Pending.ToString(), Orange);
            Metric(row, "نسبة الاكتمال", $"{r.CompletionRate:0.#}٪", Navy);
        });

        if (r.ByDepartment.Count > 0)
        {
            col.Item().Element(c => Table(c,
                new[] { "الإدارة", "الإجمالي", "مُغلقة", "قيد الإنجاز", "النسبة" },
                r.ByDepartment.Select(d => new[]
                {
                    d.DepartmentName, d.Total.ToString(), d.Closed.ToString(),
                    d.Pending.ToString(), $"{d.CompletionRate:0.#}٪",
                })));
        }
        else
        {
            Empty(col, "لا توجد تسليمات في هذه الفترة.");
        }
    }

    private static void KpiBody(ColumnDescriptor col, KpiSummaryReport r)
    {
        col.Item().Row(row =>
        {
            row.Spacing(8);
            Metric(row, "المُقيَّمون", r.Evaluated.ToString(), Navy);
            Metric(row, "متوسط الدرجات", r.AverageScore is { } a ? a.ToString("0.0") : "—", Navy);
            Metric(row, "دون المستهدف", r.BelowTarget.ToString(), Alert);
        });

        if (r.Rows.Count > 0)
        {
            col.Item().Element(c => Table(c,
                new[] { "الموظف", "الدرجة", "الاتجاه", "الحالة" },
                r.Rows.Select(x => new[]
                {
                    x.SubjectName,
                    x.TotalScore is { } s ? s.ToString("0.0") : "—",
                    TrendAr(x.Trend),
                    x.IsBelowTarget ? "دون المستهدف" : "ضمن المستهدف",
                })));
        }
        else
        {
            Empty(col, "لا توجد تقييمات في هذه الفترة.");
        }
    }

    private static void GovernanceBody(ColumnDescriptor col, GovernanceSummaryReport r)
    {
        col.Item().Row(row =>
        {
            row.Spacing(8);
            Metric(row, "مخاطر مفتوحة", r.OpenRisks.ToString(), Alert);
            Metric(row, "تصعيدات", r.OpenEscalations.ToString(), Orange);
            Metric(row, "قرارات", r.OpenDecisions.ToString(), Navy);
        });
        col.Item().Row(row =>
        {
            row.Spacing(8);
            Metric(row, "احتياجات تدريب", r.OpenTrainingNeeds.ToString(), Navy);
            Metric(row, "خطط تحسين", r.OpenImprovementPlans.ToString(), Navy);
            row.RelativeItem();
        });
    }

    // ── عناصر مساعدة ────────────────────────────────────────────────────

    private static void Setup(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.Margin(32);
        page.ContentFromRightToLeft();
        page.DefaultTextStyle(t => t.FontFamily(FontFamily).FontSize(11).FontColor(Ink));
        page.PageColor(Colors.White);
    }

    private static void Header(IContainer container, string title, string? periodKey)
    {
        container.PaddingBottom(10).BorderBottom(2).BorderColor(Orange).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(title).FontSize(18).Bold().FontColor(Navy);
                col.Item().Text(periodKey is { Length: > 0 } pk ? $"الفترة: {pk}" : "كل الفترات")
                    .FontSize(10).FontColor(Ink2);
            });
            row.ConstantItem(160).AlignLeft().Column(col =>
            {
                col.Item().Text("خبراء التسويق").FontSize(15).Bold().FontColor(Navy);
                col.Item().Text("نظام تقارير الأداء والتشغيل").FontSize(9).FontColor(Ink2);
            });
        });
    }

    private static void Footer(IContainer container)
    {
        container.PaddingTop(8).BorderTop(1).BorderColor(Line).Row(row =>
        {
            row.RelativeItem().Text($"صدر بتاريخ {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC")
                .FontSize(8).FontColor(Ink2);
            row.RelativeItem().AlignLeft().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Ink2));
                t.Span("صفحة ");
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });
        });
    }

    private static void SectionTitle(ColumnDescriptor col, string title)
        => col.Item().PaddingTop(4).Text(title).FontSize(14).Bold().FontColor(Navy);

    private static void Metric(RowDescriptor row, string label, string value, string accent)
    {
        row.RelativeItem().Background(NavyLight).Padding(10).Column(col =>
        {
            col.Item().Text(value).FontSize(18).Bold().FontColor(accent);
            col.Item().Text(label).FontSize(9).FontColor(Ink2);
        });
    }

    private static void Table(IContainer container, string[] headers, IEnumerable<string[]> rows)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(2);
                for (var i = 1; i < headers.Length; i++) cols.RelativeColumn();
            });

            table.Header(header =>
            {
                foreach (var h in headers)
                    header.Cell().Background(Navy).Padding(6).Text(h).FontColor(Colors.White).Bold().FontSize(10);
            });

            var zebra = false;
            foreach (var r in rows)
            {
                var bg = zebra ? NavyLight : "#FFFFFF";
                zebra = !zebra;
                foreach (var cell in r)
                    table.Cell().Background(bg).BorderBottom(1).BorderColor(Line).Padding(6)
                        .Text(cell).FontSize(10).FontColor(Ink);
            }
        });
    }

    private static void Empty(ColumnDescriptor col, string text)
        => col.Item().Background(NavyLight).Padding(14).AlignCenter()
            .Text(text).FontSize(11).FontColor(Ink2);

    private static string TrendAr(Reporting.Domain.Enums.KpiTrend trend) => trend switch
    {
        Reporting.Domain.Enums.KpiTrend.Up => "صاعد",
        Reporting.Domain.Enums.KpiTrend.Down => "هابط",
        Reporting.Domain.Enums.KpiTrend.Flat => "ثابت",
        _ => "غير محدد",
    };
}
