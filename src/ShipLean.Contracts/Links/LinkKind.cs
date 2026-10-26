namespace ShipLean.Contracts.Links;

// What a URL link points at, by its address. Anything that isn't a recognised pull request, merge request or issue is a page.
public enum LinkKind
{
    GithubPull,
    GithubIssue,
    GitlabMergeRequest,
    GitlabIssue,
    Page,
}
