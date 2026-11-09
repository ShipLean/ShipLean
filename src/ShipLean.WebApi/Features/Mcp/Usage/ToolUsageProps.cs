using ShipLean.Contracts.Usage;

namespace ShipLean.WebApi.Features.Mcp.Usage;

// The extra properties of a tool call's usage event. The tools that accept a payload say whether the call's arguments came
// by payload or inline, so adoption can be measured against inline use; every other tool adds nothing.
public static class ToolUsageProps
{
    public const string InputPayload = "payload";
    public const string InputInline = "inline";

    public static IReadOnlyDictionary<string, object> For(bool toolTakesPayload, bool payloadUsed)
    {
        var props = new Dictionary<string, object>();
        if (toolTakesPayload)
        {
            props[UsageProps.Input] = payloadUsed ? InputPayload : InputInline;
        }
        return props;
    }
}
