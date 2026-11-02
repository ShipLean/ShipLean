namespace ShipLean.Contracts.Usage;

// Per HTTP request (HttpContext.Items). Does nothing outside a plain HTTP request (no context, or a SignalR hub call).
// Written by the IProjectAccess decorator; read by the capture filters.
public interface IUsageScope
{
    void ProjectTouched(Guid projectId);

    // The one project, when exactly one distinct project was touched.
    Guid? ProjectId { get; }

    int ProjectCount { get; }
}
