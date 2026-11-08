using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Webhooks;

// One pull or merge request event, whatever the provider. Each provider's parser turns its payload into this, and
// everything after that is shared. Reference names it in history ("GitHub acme/api#12", "GitLab acme/api!7").
// MergeCommitSha (lower-case hex) and MergedAt are set only for a merged GitHub pull request.
public sealed record PullRequestEvent(
    WebhookProvider Provider,
    string Url,
    LinkCardState State,
    string Title,
    string SourceBranch,
    string Body,
    DateTimeOffset AsOf,
    string Repository,
    string Reference,
    string? MergeCommitSha = null,
    DateTimeOffset? MergedAt = null);
