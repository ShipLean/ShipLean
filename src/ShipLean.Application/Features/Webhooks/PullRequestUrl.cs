using System.Text.RegularExpressions;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Webhooks;

// A pull or merge request address read as a person writes it: https://host/owner/repo/pull/12 (GitHub) or
// https://host/group/sub/repo/-/merge_requests/7 (GitLab). The host is not checked, since both run on instances of their own;
// the path shape is what the link cards and the close rule already go by. Reference reads like the webhook's ("GitHub acme/api#12").
internal sealed partial record PullRequestUrl(WebhookProvider Provider, string Repository, int Number)
{
    public string Reference => Provider == WebhookProvider.Github ? $"GitHub {Repository}#{Number}" : $"GitLab {Repository}!{Number}";

    public static PullRequestUrl? Parse(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            return null;
        }

        var match = Path().Match(uri.AbsolutePath);
        if (!match.Success || !int.TryParse(match.Groups["number"].Value, out var number))
        {
            return null;
        }

        var repository = Uri.UnescapeDataString(match.Groups["repository"].Value);
        return new PullRequestUrl(match.Groups["kind"].Value == "pull" ? WebhookProvider.Github : WebhookProvider.Gitlab, repository, number);
    }

    [GeneratedRegex(@"^/(?<repository>[^/]+(?:/[^/]+)+?)/(?:-/)?(?<kind>pull|merge_requests)/(?<number>\d+)/?$")]
    private static partial Regex Path();
}
