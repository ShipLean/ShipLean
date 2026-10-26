using ShipLean.Contracts;

namespace ShipLean.TestSupport.AccessSweep.Mcp;

// One tool call the MCP sweep makes. Tool is the tool's name; Arguments the JSON object it is called with against PART
// (a project Joel is not in; seeded ids). Scope is the scope the tool needs (McpScopes value) and Minimum the least project role that may run it.
// ReadOnly says the call changes nothing. Argument-less rows are for tools that take none.
public sealed record McpSweepRow(string Tool, string Scope, ProjectRole Minimum, string? Arguments = null, bool ReadOnly = false);
