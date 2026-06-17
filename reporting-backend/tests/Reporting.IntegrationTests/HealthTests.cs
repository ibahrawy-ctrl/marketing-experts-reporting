using System.Net;
using Xunit;

namespace Reporting.IntegrationTests;

[Collection("Integration")]
public class HealthTests
{
    private readonly CustomWebApplicationFactory _factory;
    public HealthTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_Returns200_Ok()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"ok\"", body);
    }
}
