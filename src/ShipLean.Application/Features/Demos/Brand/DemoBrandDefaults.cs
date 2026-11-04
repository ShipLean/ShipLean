using System.Text;
using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.Brand;

// ShipLean's own brand: the embedded logo and tokens.css, and the seven names a slide uses with their light-theme values.
public static class DemoBrandDefaults
{
    private static readonly Lazy<string> LogoSvgText = new(() => Read("sprint-demo/shiplean-logo-on-light.svg"));
    private static readonly Lazy<string> TokensCssText = new(() => Read("sprint-demo/tokens.css"));

    public static IReadOnlyList<DemoDefaultTokenDto> DefaultTokens { get; } =
    [
        new("--color-bg", "#e8eef7"),
        new("--color-surface", "#f7f9fd"),
        new("--color-text", "#1c2638"),
        new("--color-muted", "#52627c"),
        new("--color-accent", "#2563c9"),
        new("--color-accent-fg", "#ffffff"),
        new("--font-sans", "\"Inter\", system-ui, -apple-system, \"Segoe UI\", sans-serif"),
    ];

    public static string LogoSvg => LogoSvgText.Value;

    public static string TokensCss => TokensCssText.Value;

    public static DemoBrand Brand => new(true, LogoSvg, null, null, null, true, TokensCss, null);

    private static string Read(string name)
    {
        using var stream = typeof(DemoBrandDefaults).Assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
