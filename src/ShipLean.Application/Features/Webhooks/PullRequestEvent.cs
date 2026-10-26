using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Webhooks;

// One pull or merge request event, whatever the provider. Each provider's parser turns its payload into this, and
// everything after that is shared. Reference names it in history ("GitHub acme/api#12", "GitLab acme/api!7").
public sealed record PullRequestEvent(
    WebhookProvider Provider,
    string Url,
    LinkCardState State,
    string Title,
    string SourceBranch,
    DateTimeOffset AsOf,
    string Repository,
    string Reference);
