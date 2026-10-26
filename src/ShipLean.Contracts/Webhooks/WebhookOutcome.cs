namespace ShipLean.Contracts.Webhooks;

// Processed: a pull or merge request event was applied (it may have matched nothing). Duplicate: this delivery id was
// seen before, so nothing was done. Ignored: an event or action ShipLean doesn't act on. Pong: GitHub's ping on setup.
public enum WebhookOutcome
{
    Processed,
    Duplicate,
    Ignored,
    Pong,
}
