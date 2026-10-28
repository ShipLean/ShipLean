namespace ShipLean.Application.Features.LinearApi;

// A repository as Linear names it: the host and "owner/name". Parsed from the git URL the session was started with.
public sealed record LinearRepository(string Hostname, string FullName)
{
    // https://github.com/owner/name(.git), ssh://git@host/owner/name(.git) and git@host:owner/name(.git). Null for anything else.
    public static LinearRepository? Parse(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }
        var text = url.Trim();
        string host, path;
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "ssh" or "git")
        {
            host = uri.IdnHost;
            path = uri.AbsolutePath;
        }
        else if (text.Split(':', 2) is [var scp, var rest] && scp.Contains('@') && !rest.StartsWith("//", StringComparison.Ordinal))
        {
            host = scp[(scp.IndexOf('@') + 1)..];
            path = rest;
        }
        else
        {
            return null;
        }

        var name = path.Trim('/');
        if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }
        return host.Length == 0 || name.Split('/') is not [{ Length: > 0 }, { Length: > 0 }] ? null : new LinearRepository(host, name);
    }

    public bool Matches(string? hostname, string? fullName)
        => string.Equals(Hostname, hostname, StringComparison.OrdinalIgnoreCase) && string.Equals(FullName, fullName, StringComparison.OrdinalIgnoreCase);
}
