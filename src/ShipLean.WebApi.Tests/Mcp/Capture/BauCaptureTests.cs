using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Capture;

// capture_work refuses the BAU type, which ShipLean makes itself.
[TestFixture]
public class BauCaptureTests : McpFixture
{
    protected override bool WithProbeTools => false;

    [TestCase("bau")]
    [TestCase("BAU")]
    public async Task Capture_WhenTypeIsBau_ShouldBeValidationNamingIt(string type)
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "capture_work", $$"""{"project":"SHIP","items":[{"type":"{{type}}","title":"Hand-made BAU"}]}""");

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Validation);
        error.GetProperty("message").GetString().ShouldBe("A BAU item is made by ShipLean, not by hand.");
    }
}
