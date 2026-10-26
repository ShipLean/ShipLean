namespace ShipLean.Application.Features.Links.PublicFetch;

// One line of the preview allow-list. "host" allows that host on its scheme's default port only (80 for http, 443 for https);
// "host:port" allows exactly that port, on either scheme. Hosts compare case-insensitively in their IDN (punycode) form, so
// allow-listing an internal host never opens its other ports (Redis, SSH, admin consoles).
public sealed record PreviewAllowEntry(string Host, int? Port)
{
    // Null for a line that isn't a host or host:port (it then allows nothing).
    public static PreviewAllowEntry? Parse(string line)
    {
        var text = line.Trim();
        if (text.Length == 0 || text.Contains('/') || text.Contains('@') || text.EndsWith(':')
            || !Uri.TryCreate($"{Uri.UriSchemeHttp}{Uri.SchemeDelimiter}{text}/", UriKind.Absolute, out var uri)
            || uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0)
        {
            return null;
        }

        // A colon after any IPv6 bracket is the port separator; Uri hides whether ":80" was written.
        var hasPort = text.LastIndexOf(':') > text.LastIndexOf(']');
        return new PreviewAllowEntry(uri.IdnHost, hasPort ? uri.Port : null);
    }

    public bool Allows(Uri address)
        => string.Equals(Host, address.IdnHost, StringComparison.OrdinalIgnoreCase)
           && address.Port == (Port ?? DefaultPort(address.Scheme));

    private static int DefaultPort(string scheme) => scheme == Uri.UriSchemeHttps ? 443 : 80;
}
