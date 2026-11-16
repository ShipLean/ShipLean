using System.Text.RegularExpressions;

namespace ShipLean.Contracts.Agents;

// Makes an error from an agent connection safe to store and show: a receiver can echo back what it was sent, and a delivery
// carries upload links and tokens. Called once where the error string is built, so every place it goes gets the same value.
public static partial class AgentErrorText
{
    public const int MaxLength = 1000;

    // What Scrub returns when a pattern times out: the raw text can't be shown unchecked, and Scrub must never throw to the sender.
    public const string Withheld = "The error text could not be checked and was withheld.";

    private const string Redacted = "[redacted]";

    // Strips URL userinfo, query strings and fragments (and the query of a bare /path), replaces Bearer values, shl_ tokens,
    // token= values (also inside escaped JSON), 32+ character base64 or hex runs with a digit in them, and 40+ character
    // base64url runs with [redacted], then cuts to MaxLength characters. A pattern timeout gives Withheld instead.
    public static string? Scrub(string? raw)
    {
        if (raw is null)
        {
            return null;
        }

        try
        {
            return Cut(Redact(raw));
        }
        catch (RegexMatchTimeoutException)
        {
            return Withheld;
        }
    }

    private static string Redact(string raw)
    {
        var text = Url().Replace(raw, match => match.Groups["scheme"].Value + match.Groups["rest"].Value);
        text = PathQuery().Replace(text, match => match.Groups["path"].Value);
        text = BearerValue().Replace(text, match => match.Groups["key"].Value + Redacted);
        text = TokenValue().Replace(text, match => match.Groups["key"].Value + Redacted);
        text = ShipLeanToken().Replace(text, Redacted);
        text = LongRun().Replace(text, match => match.Value.Any(char.IsAsciiDigit) ? Redacted : match.Value);
        return LongTokenRunPattern().Replace(text, Redacted);
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

    // scheme://[userinfo@]host/path[?query][#fragment]: keeps the scheme, host and path.
    [GeneratedRegex(@"(?<scheme>\b[A-Za-z][A-Za-z0-9+.\-]*://)(?:[^\s/?#@""'<>]*@)?(?<rest>[^\s?#""'<>]*)(?:\?[^\s#""'<>]*)?(?:#[^\s""'<>]*)?", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Url();

    // A path with no scheme, such as /hook?sig=abc, at the start of a word: keeps the path.
    [GeneratedRegex(@"(?<![^\s""'(<=:,;])(?<path>/[^\s?#""'<>]*)\?[^\s#""'<>]*", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PathQuery();

    [GeneratedRegex(@"(?<key>\bBearer\s+)[^\s""',;\\]+", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BearerValue();

    [GeneratedRegex(@"(?<key>\b[A-Za-z_]*token(?:\\?[""'])?\s*[=:]\s*(?:\\?[""'])?)[^\s&""',;\\]+", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TokenValue();

    [GeneratedRegex(@"\bshl_[A-Za-z0-9_\-]+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ShipLeanToken();

    [GeneratedRegex(@"[A-Za-z0-9+/_\-]{32,}={0,2}", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LongRun();

    // A 40+ character base64url run is redacted even with no digit in it: a random 43-character token has none about 0.07% of the time.
    [GeneratedRegex(@"[A-Za-z0-9_\-]{40,}", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LongTokenRunPattern();
}
