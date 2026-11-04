namespace ShipLean.Application.Features.Demos.Brand;

// What DemoTokens.Parse made of a text: the normalised lines (empty when the text was blank), or the first refusal.
// Line is the 1-based line of the refused text, null when the refusal is about the text as a whole (its size).
public sealed record DemoTokensResult(string Lines, string? Refusal, int? Line)
{
    public bool IsRefused => Refusal is not null;

    public static DemoTokensResult Accepted(string lines) => new(lines, null, null);

    public static DemoTokensResult Refused(string refusal, int? line = null) => new(string.Empty, refusal, line);
}
