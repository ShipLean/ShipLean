namespace ShipLean.WebApi.Features.Mcp.Payloads;

// Opts a tool method in to payloads: its large arguments may come from a file uploaded with create_payload_upload, named
// by the `payload` argument the method then takes last before its CancellationToken (McpPayloadTargets reads this back).
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class McpPayloadAttribute : Attribute
{
}
