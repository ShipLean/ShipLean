namespace ShipLean.Contracts.Usage;

// The values of the viewport usage property, sent by the SPA in ViewportHeader. Anything else, a blank or a missing header
// is no viewport: the property is left out, never defaulted.
public static class UsageViewports
{
    public const string Phone = "phone";
    public const string Tablet = "tablet";
    public const string Desktop = "desktop";

    // Exact lowercase match only.
    public static string? Parse(string? header)
        => header switch
        {
            Phone => Phone,
            Tablet => Tablet,
            Desktop => Desktop,
            _ => null,
        };
}
