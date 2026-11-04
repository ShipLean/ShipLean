using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Fakes.YouTube;

// One upload the fake recorded: the video id it returned, what was asked for, and the bytes sent.
public sealed record FakeYouTubeUpload(string VideoId, YouTubeUpload Upload, ReadOnlyMemory<byte> Content);
