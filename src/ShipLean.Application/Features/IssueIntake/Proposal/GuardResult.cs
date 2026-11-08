namespace ShipLean.Application.Features.IssueIntake.Proposal;

// What PublicTextGuard.Clean returns. Safe says the text passed the final check and may be posted. When Safe is false, Text is
// empty and the last line of Removed says why: the caller must never post anything, and holds the issue for a maintainer.
// Removed is for ShipLean's own log and the Triage item, never for GitHub: it holds the very names and links it removed.
public sealed record GuardResult(bool Safe, string Text, IReadOnlyList<string> Removed);
