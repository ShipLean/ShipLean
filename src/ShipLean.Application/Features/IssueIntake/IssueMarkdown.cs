namespace ShipLean.Application.Features.IssueIntake;

// Markdown ShipLean adds to what it posts on GitHub. The outbox marker is a hidden HTML comment carrying the outbox entry's
// id, so a retry after an ambiguous POST can find the comment it may already have made. Anyone can type a marker, so only a
// comment by ShipLean's own login counts as carrying one.
public static class IssueMarkdown
{
    public static string OutboxMarker(Guid id) => $"<!-- shiplean:outbox:{id:D} -->";

    public static string WithMarker(string body, Guid id) => $"{body.TrimEnd()}\n\n{OutboxMarker(id)}";
}
