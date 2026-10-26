using ShipLean.Contracts.Links;

namespace ShipLean.Application.Features.Links.GitHub;

public static class GitHubStates
{
    // Merged and draft are flags of an open or closed pull request; an issue is only open or closed.
    public static LinkState? Of(GitHubReply reply) => reply switch
    {
        { Merged: true } => LinkState.Merged,
        { State: "closed" } => LinkState.Closed,
        { State: "open", Draft: true } => LinkState.Draft,
        { State: "open" } => LinkState.Open,
        _ => null,
    };
}
