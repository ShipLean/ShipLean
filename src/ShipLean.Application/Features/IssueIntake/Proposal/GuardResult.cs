namespace ShipLean.Application.Features.IssueIntake.Proposal;

// What PublicTextGuard.Clean returns: the text that may be posted, and one line per change it made (what kind, and what).
// Removed is for ShipLean's own log and the Triage item, never for GitHub: it holds the very names and links it removed.
public sealed record GuardResult(string Text, IReadOnlyList<string> Removed);
