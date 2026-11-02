using System.Reflection;

namespace ShipLean.Contracts.Usage;

// The extra property keys an event may carry. The standard properties are not here: the dispatcher derives them from the record.
public static class UsageProps
{
    public const string Screen = "screen";
    public const string Board = "board";
    public const string Provider = "provider";
    public const string Count = "count";

    // The client being connected, on agent.connected / agent.consent_denied; cut to 64 characters.
    public const string AgentName = "agent_name";

    public static readonly IReadOnlySet<string> All = typeof(UsageProps)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral)
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToHashSet(StringComparer.Ordinal);
}
