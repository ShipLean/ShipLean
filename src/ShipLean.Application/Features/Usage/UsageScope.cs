using System.Diagnostics.CodeAnalysis;
using System.Collections.Immutable;
using ShipLean.Contracts;
using ShipLean.Contracts.Usage;

namespace ShipLean.Application.Features.Usage;

// The distinct projects one HTTP request touched and the extra properties its command added, kept in the request's items, and the
// request's viewport header. A singleton like RequestCurrentAgent: it reads the request (ICurrentRequest) at call time. With no
// request, or in a SignalR hub call (its request lives as long as the connection), it does nothing.
public sealed class UsageScope(ICurrentRequest request) : IUsageScope
{
    private const string ItemKey = "shiplean.usage.projects";
    private const string PropertiesKey = "shiplean.usage.properties";
    private const string HubPrefix = "/hubs/";

    public void ProjectTouched(Guid projectId)
    {
        if (!TryPlain(out var items))
        {
            return;
        }
        lock (items)
        {
            if (items[ItemKey] is not HashSet<Guid> touched)
            {
                touched = [];
                items[ItemKey] = touched;
            }
            touched.Add(projectId);
        }
    }

    public Guid? ProjectId => Snapshot is [var only] ? only : null;

    public int ProjectCount => Snapshot.Length;

    public string? Viewport => UsageViewports.Parse(TryPlain(out _) ? request.Header(ViewportHeader.Name) : null);

    public void AddProperty(string key, object value)
    {
        if (!UsageProps.All.Contains(key))
        {
            throw new ArgumentException($"'{key}' is not a usage property key.", nameof(key));
        }
        if (!TryPlain(out var items))
        {
            return;
        }
        lock (items)
        {
            items[PropertiesKey] = PropertiesIn(items).SetItem(key, value);
        }
    }

    // Copy on write: each AddProperty stores a new immutable map, so a reader holds a snapshot without copying.
    public IReadOnlyDictionary<string, object> Properties
    {
        get
        {
            if (!TryPlain(out var items))
            {
                return ImmutableDictionary<string, object>.Empty;
            }
            lock (items)
            {
                return PropertiesIn(items);
            }
        }
    }

    private static ImmutableDictionary<string, object> PropertiesIn(IDictionary<object, object?> items)
        => items[PropertiesKey] as ImmutableDictionary<string, object> ?? ImmutableDictionary<string, object>.Empty.WithComparers(StringComparer.Ordinal);

    private Guid[] Snapshot
    {
        get
        {
            if (!TryPlain(out var items))
            {
                return [];
            }
            lock (items)
            {
                return items[ItemKey] is HashSet<Guid> touched ? [.. touched] : [];
            }
        }
    }

    // The request's items, unless there is no request or this is a hub call.
    private bool TryPlain([NotNullWhen(true)] out IDictionary<object, object?>? items)
    {
        items = request.Items;
        return items is not null && !request.Path.StartsWithSegments(HubPrefix.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
    }
}
