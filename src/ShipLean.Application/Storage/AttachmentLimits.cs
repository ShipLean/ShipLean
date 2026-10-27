namespace ShipLean.Application.Storage;

// The most bytes one attachment may hold (SHIPLEAN_ATTACHMENT_MAX_BYTES, 25 MiB when not set; uploads and imports share it)
// and the most live attachment bytes one work item may hold (SHIPLEAN_ATTACHMENT_MAX_ITEM_BYTES, 250 MiB when not set).
public sealed record AttachmentLimits(long MaxBytes, long MaxItemBytes = AttachmentLimits.DefaultMaxItemBytes)
{
    public const string ConfigKey = "SHIPLEAN_ATTACHMENT_MAX_BYTES";

    public const string ItemConfigKey = "SHIPLEAN_ATTACHMENT_MAX_ITEM_BYTES";

    public const long DefaultMaxBytes = 25L * 1024 * 1024;

    public const long DefaultMaxItemBytes = 250L * 1024 * 1024;

    public string ItemFullMessage => $"A work item can hold at most {MaxItemBytes / (1024 * 1024)} MB of attachments.";

    public string FileTooLargeMessage => $"An attachment can be at most {MaxBytes / (1024 * 1024)} MB.";
}
