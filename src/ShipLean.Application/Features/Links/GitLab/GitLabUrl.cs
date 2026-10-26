using System.Text.RegularExpressions;
using ShipLean.Application.Features.Links.Rows;
using ShipLean.Contracts.Links;

namespace ShipLean.Application.Features.Links.GitLab;

// A merge request or issue address on the configured GitLab: <GitLabUrl>/<group>/<project>/-/merge_requests/<n> or /-/issues/<n>.
// The group path may nest, and GitLabUrl may carry a path prefix (a relative URL root). Segments are kept to the characters
// GitLab allows in a path, so a crafted address can't steer the API call somewhere else.
public sealed partial record GitLabUrl(LinkKind Kind, string ProjectPath, int Number, string Location)
{
    // The API path after the base address: api/v4/projects/<url-encoded project path>/merge_requests/<n> or .../issues/<n>.
    public string ApiPath => $"api/v4/projects/{Uri.EscapeDataString(ProjectPath)}/{(Kind == LinkKind.GitlabMergeRequest ? "merge_requests" : "issues")}/{Number}";

    public static bool TryParse(Uri url, string? gitLabUrl, out GitLabUrl parsed)
    {
        parsed = null!;
        if (url.Scheme is not ("http" or "https") || string.IsNullOrWhiteSpace(gitLabUrl) || !Uri.TryCreate(gitLabUrl.Trim(), UriKind.Absolute, out var root)
            || !url.Host.Equals(root.Host, StringComparison.OrdinalIgnoreCase) || url.Port != root.Port)
        {
            return false;
        }

        var prefix = root.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var segments = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length <= prefix.Length || !segments.AsSpan(0, prefix.Length).SequenceEqual(prefix))
        {
            return false;
        }

        var rest = segments[prefix.Length..];
        var marker = Array.IndexOf(rest, "-");
        if (marker < 2 || marker + 2 >= rest.Length || !int.TryParse(rest[marker + 2], out var number) || number <= 0
            || !rest[..marker].All(IsName))
        {
            return false;
        }

        var kind = rest[marker + 1] switch { "merge_requests" => LinkKind.GitlabMergeRequest, "issues" => LinkKind.GitlabIssue, _ => LinkKind.Page };
        if (kind == LinkKind.Page)
        {
            return false;
        }

        parsed = new GitLabUrl(kind, string.Join('/', rest[..marker]), number, LinkAddress.Of(url).Location ?? string.Join('/', rest[..marker]));
        return true;
    }

    private static bool IsName(string segment) => segment is not ("." or "..") && NamePattern().IsMatch(segment);

    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    private static partial Regex NamePattern();
}
