using ShipLean.Contracts.Links;

namespace ShipLean.Application.Features.Links.Rows;

// What an address says about itself, with no network: the kind of thing it names, which list it belongs in, and the
// number and location a pull request, merge request or issue carries in its path.
//   https://github.com/acme/storefront/pull/482            -> GithubPull, 482, "acme/storefront"
//   https://gitlab.example.com/payments/vault/-/issues/7   -> GitlabIssue, 7, "gitlab.example.com · payments/vault"
// GitLab is recognised by its "/-/merge_requests/" and "/-/issues/" path markers on any host; gitlab.com itself shows the
// project path alone. Anything else is a page.
public sealed record LinkAddress(LinkKind Kind, int? Number, string? Location, string Host)
{
    private const string GitLabDotCom = "gitlab.com";

    public LinkGroup Group => Kind == LinkKind.Page ? LinkGroup.Page : LinkGroup.Code;

    // The address as stored. A value that isn't an absolute http(s) address is a page with no host.
    public static LinkAddress Of(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? Of(uri) : new LinkAddress(LinkKind.Page, null, null, string.Empty);

    public static LinkAddress Of(Uri uri)
    {
        var host = uri.Host;
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (host.Equals("github.com", StringComparison.OrdinalIgnoreCase) && segments is [var owner, var repo, var what, var number, ..]
            && int.TryParse(number, out var n) && n > 0)
        {
            var kind = what switch { "pull" => LinkKind.GithubPull, "issues" => LinkKind.GithubIssue, _ => LinkKind.Page };
            if (kind != LinkKind.Page)
            {
                return new LinkAddress(kind, n, $"{owner}/{repo}", host);
            }
        }

        var marker = Array.IndexOf(segments, "-");
        if (marker >= 1 && marker + 2 < segments.Length && int.TryParse(segments[marker + 2], out var iid) && iid > 0)
        {
            var kind = segments[marker + 1] switch { "merge_requests" => LinkKind.GitlabMergeRequest, "issues" => LinkKind.GitlabIssue, _ => LinkKind.Page };
            if (kind != LinkKind.Page)
            {
                var project = string.Join('/', segments[..marker]);
                return new LinkAddress(kind, iid, host.Equals(GitLabDotCom, StringComparison.OrdinalIgnoreCase) ? project : $"{host} · {project}", host);
            }
        }

        return new LinkAddress(LinkKind.Page, null, null, host);
    }
}
