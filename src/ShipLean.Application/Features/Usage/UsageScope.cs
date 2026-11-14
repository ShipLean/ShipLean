using System.Collections.Immutable;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Usage;

namespace ShipLean.Application.Features.Usage;

// The distinct projects one HTTP request touched and the extra properties its command added, kept in HttpContext.Items, and the
// request's viewport header. A singleton like RequestCurrentAgent: it reads the request at call time. With no request, or in a
// SignalR hub call (its HttpContext lives as long as the connection), it does nothing.
public sealed class UsageScope(IServiceProvider services) : IUsageScope
{
    private const string ItemKey = "shiplean.usage.projects";
    private const string PropertiesKey = "shiplean.usage.properties";
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

    public string? Viewport => UsageViewports.Parse(Plain?.Request.Headers[ViewportHeader.Name].FirstOrDefault());

    public void AddProperty(string key, object value)
    {
        if (!UsageProps.All.Contains(key))
        {
            throw new ArgumentException($"'{key}' is not a usage property key.", nameof(key));
        }
        if (Plain is not { } context)
        {
            return;
        }
        lock (context.Items)
        {
            context.Items[PropertiesKey] = PropertiesIn(context.Items).SetItem(key, value);
        }
    }

    // Copy on write: each AddProperty stores a new immutable map, so a reader holds a snapshot without copying.
    public IReadOnlyDictionary<string, object> Properties
    {
        get
        {
            if (Plain is not { } context)
            {
                return ImmutableDictionary<string, object>.Empty;
            }
            lock (context.Items)
            {
                return PropertiesIn(context.Items);
            }
        }
    }

    private static ImmutableDictionary<string, object> PropertiesIn(IDictionary<object, object?> items)
        => items[PropertiesKey] as ImmutableDictionary<string, object> ?? ImmutableDictionary<string, object>.Empty.WithComparers(StringComparer.Ordinal);

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
