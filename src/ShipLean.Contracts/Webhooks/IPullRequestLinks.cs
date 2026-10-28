namespace ShipLean.Contracts.Webhooks;

// Links a GitHub or GitLab pull or merge request to an item the way the PR webhook does: an item that has (or had, before someone
// archived it) a link to the same url is left alone, and the write is a webhook write with no acting user.
public interface IPullRequestLinks
{
    // False when the url is not a pull or merge request, or the item already has it. `linkedBy` names who asked ("agent Relay"), for history.
    Task<bool> LinkAsync(Guid workItemId, string url, string? title, string linkedBy, CancellationToken cancellationToken = default);
}
