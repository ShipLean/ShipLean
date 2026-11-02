using System.Reflection;

namespace ShipLean.Application.Features.Analytics;

// The running build's version for events: the entry assembly's informational version without the "+<sha>" build metadata.
public static class ShipLeanVersion
{
    public static string Current { get; } = Read();

    private static string Read()
    {
        var text = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(text))
        {
            return "unknown";
        }
        var plus = text.IndexOf('+', StringComparison.Ordinal);
        return plus > 0 ? text[..plus] : text;
    }
}
