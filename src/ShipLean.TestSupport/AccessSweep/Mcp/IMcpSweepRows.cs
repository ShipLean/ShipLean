namespace ShipLean.TestSupport.AccessSweep.Mcp;

// Implemented once per tool area, in a file of its own with a parameterless constructor, in any test assembly. The
// MCP sweep finds every implementation by reflection, and every tool must have a row. Test infrastructure, like
// IAccessSweepRows.
public interface IMcpSweepRows
{
    IEnumerable<McpSweepRow> Rows { get; }
}
