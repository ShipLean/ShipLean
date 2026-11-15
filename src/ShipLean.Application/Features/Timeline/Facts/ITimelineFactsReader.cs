namespace ShipLean.Application.Features.Timeline.Facts;

// Reads the facts of one Feature as of asOf (C3). Callers check access and that featureId is a Feature of projectId first.
public interface ITimelineFactsReader
{
    Task<TimelineFacts> ReadAsync(Guid projectId, Guid featureId, DateTimeOffset asOf, CancellationToken cancellationToken);
}
