namespace ShipLean.Contracts.Screens;

// The open questions and decisions waiting on the caller. A Feature's counts include its stories' asks; OnChildren is true
// when some of them are on the stories.
public sealed record NeedsYouDto(int Questions, int Decisions, bool OnChildren);
