using Reporting.Domain.Common;
using Xunit;

namespace Reporting.UnitTests;

public class SmokeTests
{
    private class SampleEntity : BaseEntity { }

    [Fact]
    public void BaseEntity_GeneratesGuidId_ByDefault()
    {
        var a = new SampleEntity();
        var b = new SampleEntity();
        Assert.NotEqual(Guid.Empty, a.Id);
        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void BaseEntity_SetsCreatedAtUtc()
    {
        var e = new SampleEntity();
        Assert.True(e.CreatedAtUtc <= DateTime.UtcNow);
        Assert.Null(e.UpdatedAtUtc);
    }
}
