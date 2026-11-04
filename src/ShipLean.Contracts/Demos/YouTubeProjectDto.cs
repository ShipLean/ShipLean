namespace ShipLean.Contracts.Demos;

public sealed record YouTubeProjectDto(YouTubeConnectionStatus Status, string? ChannelTitle, YouTubeVisibility? Visibility, string? CheckedLabel, string? Refusal);
