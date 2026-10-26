namespace ShipLean.Contracts.Telemetry;

// What the background jobs report for the ShipLean meter. Application code cannot see the host's meter, so it calls this; the host
// records it. Outcomes and counts only: nothing that names a person, an item or a project.
public interface IJobMetrics
{
    // A finished import job: outcome is "done", "done_with_errors", "failed" or "stopped".
    void ImportJobFinished(string outcome);

    // Work items an import job wrote.
    void ImportItemsWritten(int count);

    // One indexing pass: rows embedded, and rows still waiting for a vector afterwards.
    void SearchPassFinished(int indexed, int remaining);
}
