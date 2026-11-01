using ShipLean.Contracts.Usage;

namespace ShipLean.Application.Features.Usage;

// The distinct projects one HTTP request touched, kept in HttpContext.Items. A singleton like RequestCurrentAgent: it reads the
// request at call time. With no request, or in a SignalR hub call (its HttpContext lives as long as the connection), it does nothing.
public sealed class UsageScope(IServiceProvider services) : IUsageScope
{
    private const string ItemKey = "shiplean.usage.projects";
    private const string HubPrefix = "/hubs/";

    public void ProjectTouched(Guid projectId)
    {
        if (Plain is not { } context)
        {
            return;
        }
        lock (context.Items)
        {
            if (context.Items[ItemKey] is not HashSet<Guid> touched)
            {
                touched = [];
                context.Items[ItemKey] = touched;
            }
            touched.Add(projectId);
        }
    }

    public Guid? ProjectId => Snapshot is [var only] ? only : null;

    public int ProjectCount => Snapshot.Length;

    private Guid[] Snapshot
    {
        get
        {
            if (Plain is not { } context)
            {
                return [];
            }
            lock (context.Items)
            {
                return context.Items[ItemKey] is HashSet<Guid> touched ? [.. touched] : [];
            }
        }
    }

    private HttpContext? Plain
        => services.GetService<IHttpContextAccessor>()?.HttpContext is { } context && !context.Request.Path.StartsWithSegments(HubPrefix.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
            ? context
            : null;
}
