using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Comments;

// A comment marked superseded: still shown, struck through, with who marked it and when.
public sealed record CommentSuperseded(DateTimeOffset At, string AtLabel, UserRef By);
