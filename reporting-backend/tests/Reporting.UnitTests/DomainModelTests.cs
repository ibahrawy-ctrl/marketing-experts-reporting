using Reporting.Domain.Entities.Kpi;
using Reporting.Domain.Entities.Submissions;
using Reporting.Domain.Entities.Templates;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.UnitTests;

public class DomainModelTests
{
    [Fact]
    public void Submission_DefaultsToDraft_WithGuidId()
    {
        var s = new ReportSubmission();
        Assert.Equal(SubmissionStatus.Draft, s.Status);
        Assert.NotEqual(Guid.Empty, s.Id);
    }

    [Fact]
    public void SubmissionLifecycle_HasEightStates()
    {
        Assert.Equal(8, Enum.GetValues<SubmissionStatus>().Length);
    }

    [Fact]
    public void FieldBuilder_ExposesTwentyPlusFieldTypes()
    {
        // 20 أنواع بيانات + فاصل قسم = ≥ 20.
        Assert.True(Enum.GetValues<FieldType>().Length >= 20);
    }

    [Fact]
    public void KpiTemplate_DefaultsToWeeklyPulse_DraftStatus()
    {
        var k = new KpiTemplate();
        Assert.Equal(KpiCadence.WeeklyPulse, k.Cadence);
        Assert.Equal(TemplateStatus.Draft, k.Status);
    }

    [Fact]
    public void TemplateVersion_StartsAtVersionOne_Unpublished()
    {
        var v = new ReportTemplateVersion();
        Assert.Equal(1, v.VersionNumber);
        Assert.False(v.IsPublished);
        Assert.Empty(v.Fields);
    }
}
