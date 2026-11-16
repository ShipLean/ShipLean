using System.Text.RegularExpressions;

namespace ShipLean.Contracts.Agents;

// Makes an error from an agent connection safe to store and show: a receiver can echo back what it was sent, and a delivery
// carries upload links and tokens. Called once where the error string is built, so every place it goes gets the same value.
public static partial class AgentErrorText
{
    public const int MaxLength = 1000;

    private const string Redacted = "[redacted]";

    // Strips URL query strings, replaces shl_ tokens, token= values and 32+ character base64 or hex runs (with a digit in them,
    // so a long readable word survives) with [redacted], then cuts to MaxLength characters.
    public static string? Scrub(string? raw)
    {
        if (raw is null)
        {
            return null;
        }

        var text = QueryString().Replace(raw, match => match.Groups["url"].Value);
        text = TokenValue().Replace(text, match => match.Groups["key"].Value + Redacted);
        text = ShipLeanToken().Replace(text, Redacted);
        text = LongRun().Replace(text, match => match.Value.Any(char.IsAsciiDigit) ? Redacted : match.Value);
        return Cut(text);
    }

    private static string Cut(string text)
    {
        if (text.Length <= MaxLength)
        {
            return text;
        }

        var end = char.IsHighSurrogate(text[MaxLength - 1]) ? MaxLength - 1 : MaxLength;
        return text[..end];
    }

    [GeneratedRegex(@"(?<url>\b[A-Za-z][A-Za-z0-9+.\-]*://[^\s?#""'<>]*)\?[^\s#""'<>]*", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex QueryString();

    [GeneratedRegex(@"(?<key>\b[A-Za-z_]*token[""']?\s*[=:]\s*[""']?)[^\s&""',;]+", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TokenValue();

    [GeneratedRegex(@"\bshl_[A-Za-z0-9_\-]+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ShipLeanToken();

    [GeneratedRegex(@"[A-Za-z0-9+/_\-]{32,}={0,2}", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LongRun();
}
