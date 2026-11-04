using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.WorkItemDetails.Attachments;
using ShipLean.Application.Storage;

namespace ShipLean.UnitTest.Storage;

[TestFixture]
public class AttachmentLimitsTests
{
    [Test]
    public void FromConfiguration_WhenNothingSet_ShouldDefaultTheDemoLimitTo200MiB()
    {
        var limits = AttachmentLimits.FromConfiguration(Config([]));

        limits.MaxDemoBytes.ShouldBe(200L * 1024 * 1024);
        AttachmentLimits.DemoConfigKey.ShouldBe("SHIPLEAN_DEMO_MAX_BYTES");
    }

    [Test]
    public void FromConfiguration_WhenDemoLimitSet_ShouldUseIt()
    {
        var limits = AttachmentLimits.FromConfiguration(Config(new() { [AttachmentLimits.DemoConfigKey] = "1000" }));

        limits.MaxDemoBytes.ShouldBe(1000);
    }

    [Test]
    public void FromConfiguration_WhenDemoLimitIsAboveTheItemLimit_ShouldFailNamingTheSetting()
    {
        var config = Config(new() { [AttachmentLimits.DemoConfigKey] = "2000", [AttachmentLimits.ItemConfigKey] = "1000" });

        var error = Should.Throw<InvalidOperationException>(() => AttachmentLimits.FromConfiguration(config));

        error.Message.ShouldContain("SHIPLEAN_DEMO_MAX_BYTES");
    }

    [Test]
    public void AddServices_WhenDemoLimitIsAboveTheItemLimit_ShouldFailAtStartup()
    {
        var config = Config(new() { [AttachmentLimits.DemoConfigKey] = "2000", [AttachmentLimits.ItemConfigKey] = "1000" });

        Should.Throw<InvalidOperationException>(() => new AttachmentStoreServices().AddServices(new ServiceCollection(), config));
    }

    private static IConfiguration Config(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
