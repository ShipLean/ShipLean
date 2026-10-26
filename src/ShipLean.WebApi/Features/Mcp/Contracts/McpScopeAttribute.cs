namespace ShipLean.WebApi.Features.Mcp.Contracts;

// The scope a tool needs, on the tool method or on its [McpServerToolType] type (the method wins). A call without it is
// refused with the 403 step-up. PreviewScope is what the call needs when the tool's `plan` argument is absent (a
// preview changes nothing, so it needs only read); null means the tool has no preview and Scope always applies.
// PreviewWhenManyOf names an array argument: when set, a call without `plan` is a preview only if that array holds more
// than one entry (capture_work creates a single item straight away, so that call is a write).
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class McpScopeAttribute(string scope) : Attribute
{
    public string Scope { get; } = scope;

    public string? PreviewScope { get; init; }

    public string? PreviewWhenManyOf { get; init; }
}
