namespace ShipLean.Contracts.Usage;

// Per HTTP request (HttpContext.Items). Does nothing outside a plain HTTP request (no context, or a SignalR hub call).
// Projects are written by the IProjectAccess decorator, extra properties by commands; both are read by the capture seams.
public interface IUsageScope
{
    void ProjectTouched(Guid projectId);

    // The one project, when exactly one distinct project was touched.
    Guid? ProjectId { get; }

    int ProjectCount { get; }

    // The request's ViewportHeader parsed by UsageViewports.Parse; null when missing or unknown, and outside a plain request.
    string? Viewport { get; }

    // An extra property (a UsageProps key) for the usage event of the current web action; the action filter records it.
    // Only a Track action records it: a property added in a self-emitted or untracked action is not recorded.
    void AddProperty(string key, object value);

    // The extra properties added so far in this request.
    IReadOnlyDictionary<string, object> Properties { get; }
}
