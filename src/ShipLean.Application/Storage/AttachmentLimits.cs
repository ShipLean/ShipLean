using Microsoft.Extensions.Configuration;

namespace ShipLean.Application.Storage;

// The most bytes one attachment may hold (SHIPLEAN_ATTACHMENT_MAX_BYTES, 25 MiB when not set; uploads and imports share it)
// the most live attachment bytes one work item may hold (SHIPLEAN_ATTACHMENT_MAX_ITEM_BYTES, 250 MiB when not set)
// and the most bytes one sprint demo video may hold (SHIPLEAN_DEMO_MAX_BYTES, 200 MiB when not set; never above the item limit).
public sealed record AttachmentLimits(long MaxBytes, long MaxItemBytes = AttachmentLimits.DefaultMaxItemBytes, long MaxDemoBytes = AttachmentLimits.DefaultMaxDemoBytes)
{
    public const string ConfigKey = "SHIPLEAN_ATTACHMENT_MAX_BYTES";

    public const string ItemConfigKey = "SHIPLEAN_ATTACHMENT_MAX_ITEM_BYTES";

    public const string DemoConfigKey = "SHIPLEAN_DEMO_MAX_BYTES";

    public const long DefaultMaxBytes = 25L * 1024 * 1024;

    public const long DefaultMaxItemBytes = 250L * 1024 * 1024;

    public const long DefaultMaxDemoBytes = 200L * 1024 * 1024;

    // Reads the three settings (a missing or non-positive value is the default) and refuses a demo limit above the item limit, naming the setting.
    public static AttachmentLimits FromConfiguration(IConfiguration configuration)
    {
        var limits = new AttachmentLimits(
            long.TryParse(configuration[ConfigKey], out var max) && max > 0 ? max : DefaultMaxBytes,
            long.TryParse(configuration[ItemConfigKey], out var itemMax) && itemMax > 0 ? itemMax : DefaultMaxItemBytes,
            long.TryParse(configuration[DemoConfigKey], out var demoMax) && demoMax > 0 ? demoMax : DefaultMaxDemoBytes);
        if (limits.MaxDemoBytes > limits.MaxItemBytes)
        {
            throw new InvalidOperationException($"{DemoConfigKey} ({limits.MaxDemoBytes}) cannot be above {ItemConfigKey} ({limits.MaxItemBytes}).");
        }

        return limits;
    }

    public string ItemFullMessage => $"A work item can hold at most {MaxItemBytes / (1024 * 1024)} MB of attachments.";

    public string FileTooLargeMessage => $"An attachment can be at most {MaxBytes / (1024 * 1024)} MB.";
}
