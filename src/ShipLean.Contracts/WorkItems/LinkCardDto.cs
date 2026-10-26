namespace ShipLean.Contracts.WorkItems;

// What a provider last reported about the pull or merge request a url link points at. AsOfLabel is DisplayDate.Relative(AsOf).
public sealed record LinkCardDto(LinkCardState State, string? Title, DateTimeOffset AsOf, string AsOfLabel, WebhookProvider Source);
