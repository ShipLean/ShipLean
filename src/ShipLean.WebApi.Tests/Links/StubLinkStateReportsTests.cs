using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Links;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Links;

// The reported-state contract that holds before the real one lands: an address no link carries is not_found, because the
// MCP sweep needs that answer.
[TestFixture]
public class StubLinkStateReportsTests : ApiFixture
{
    private static readonly DateTimeOffset AsOf = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Report_WhenNoLinkHasThatAddress_ShouldBeNotFound()
    {
        using var scope = Host.Services.CreateScope();

        var error = await Should.ThrowAsync<ApiException>(
            () => scope.ServiceProvider.GetRequiredService<ILinkStateReports>().ReportAsync("https://nobody.example/none", null, LinkState.Merged, AsOf));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Report_WhenASeededLinkHasThatAddress_ShouldNotThrow()
    {
        using var scope = Host.Services.CreateScope();

        await Should.NotThrowAsync(
            () => scope.ServiceProvider.GetRequiredService<ILinkStateReports>().ReportAsync("https://github.com/acme/storefront/pull/482", null, LinkState.Merged, AsOf));
    }
}
