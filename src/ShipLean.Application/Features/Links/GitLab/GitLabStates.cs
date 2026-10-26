using ShipLean.Contracts.Links;

namespace ShipLean.Application.Features.Links.GitLab;

public static class GitLabStates
{
    // GitLab says opened, closed, merged (and locked, a merge request being closed). Draft is a flag of an opened merge request.
    public static LinkState? Of(GitLabReply reply) => reply switch
    {
        { State: "merged" } => LinkState.Merged,
        { State: "closed" or "locked" } => LinkState.Closed,
        { State: "opened", Draft: true } => LinkState.Draft,
        { State: "opened" } => LinkState.Open,
        _ => null,
    };
}
