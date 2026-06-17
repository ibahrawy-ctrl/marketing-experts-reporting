using Reporting.Application.Kpi;
using Reporting.Application.Templates;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.IntegrationTests;

[Collection("Integration")]
public class SeedTests
{
    private readonly CustomWebApplicationFactory _factory;

    public SeedTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task ReportTemplates_AreSeeded_PublishedWithFields()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var list = await (await admin.GetAsync("/api/report-templates?status=Published&isActive=true"))
            .ReadAsync<List<ReportTemplateDto>>();

        Assert.NotNull(list);
        var seeded = list!.Where(t => t.FieldCount > 0).ToList();
        Assert.True(seeded.Count >= 24, $"expected ≥24 seeded published report templates, got {seeded.Count}");
        Assert.All(seeded, t => Assert.Equal(TemplateStatus.Published, t.Status));
        Assert.Contains(list!, t => t.Title == "تقرير المدير العام");
        Assert.Contains(list!, t => t.Title == Reporting.Application.Common.B2cReportSchema.TemplateTitle);
    }

    [Fact]
    public async Task KpiTemplates_AreSeeded_PublishedWithMetrics()
    {
        var admin = await TestAuth.LoginAsAdminAsync(_factory);
        var list = await (await admin.GetAsync("/api/kpi-templates?status=Published&isActive=true"))
            .ReadAsync<List<KpiTemplateDto>>();

        Assert.NotNull(list);
        var seeded = list!.Where(t => t.MetricCount > 0).ToList();
        Assert.True(seeded.Count >= 3, $"expected ≥3 seeded published KPI templates, got {seeded.Count}");
        Assert.All(seeded, t => Assert.Equal(TemplateStatus.Published, t.Status));
        Assert.Contains(list!, t => t.Title == "النبض الأسبوعي العام");
    }
}
