using System.Collections.Concurrent;

namespace ShipLean.WebApi.Features.Mcp.Planning;

// The guide's Markdown files, embedded in this assembly as planning-guide/<wire name>.md. Each is read once.
public static class PlanGuide
{
    private static readonly ConcurrentDictionary<PlanGuidePart, string> Cache = new();

    public static string Read(PlanGuidePart part) => Cache.GetOrAdd(part, Load);

    private static string Load(PlanGuidePart part)
    {
        var name = $"planning-guide/{part.WireName()}.md";
        using var stream = typeof(PlanGuide).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"The embedded guide file {name} is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
