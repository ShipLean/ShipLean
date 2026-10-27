using ShipLean.Application.Storage;
using ShipLean.Application.Features.WorkItemDetails.Attachments;
using ShipLean.WebApi.Features.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Transport;

// The cap is derived, not a fixed 1 MB: base64 of the file limit, plus the envelope, and above Kestrel's own default.
[TestFixture]
public class McpBodyLimitTests
{
    [Test]
    public void BytesFor_WhenDefaultFileLimit_ShouldFitTheFileAndExceedKestrel()
    {
        var file = AttachmentLimits.DefaultMaxBytes;
        var cap = McpBodyLimit.BytesFor(file);

        cap.ShouldBe((4 * ((file + 2) / 3)) + McpBodyLimit.EnvelopeBytes);
        cap.ShouldBeGreaterThan(file);
        cap.ShouldBeGreaterThan(McpBodyLimit.KestrelDefaultMaxBodyBytes);
        McpBodyLimit.Size(cap).ShouldBe("33.4 MB");
    }

    [Test]
    public void Size_WhenExactMegabyte_ShouldNotShowADecimal()
    {
        McpBodyLimit.Size(1024 * 1024).ShouldBe("1 MB");
    }
}
