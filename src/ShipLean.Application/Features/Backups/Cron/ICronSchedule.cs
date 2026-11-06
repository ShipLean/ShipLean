namespace ShipLean.Application.Features.Backups.Cron;

// The backup schedule's cron rules (decision D2): 5 fields, UTC, runs at least an hour apart over the next 400 days.
// The server is the only parser; the Backups tab shows Preview's description and next runs.
public interface ICronSchedule
{
    CronPreview Preview(string cron, DateTimeOffset from, int count = 5);

    // The first occurrence strictly after `after`, or null when the expression doesn't parse or never runs again.
    DateTimeOffset? Next(string cron, DateTimeOffset after);

    // Null when the expression is allowed, otherwise the message to show.
    string? Validate(string cron);
}
