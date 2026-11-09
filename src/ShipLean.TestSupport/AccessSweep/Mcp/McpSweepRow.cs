using ShipLean.Contracts;

namespace ShipLean.TestSupport.AccessSweep.Mcp;

// One tool call the MCP sweep makes. Tool is the tool's name; Arguments the JSON object it is called with against PART
// (a project Joel is not in; seeded ids). Scope is the scope the tool needs (McpScopes value) and Minimum the least project role that may run it.
// ReadOnly says the call changes nothing. Argument-less rows are for tools that take none. NoProject is the reason a tool names
// no project at all (so there is no project access rule to sweep); the sweep lists such a row but does not call it, and fails it
// when the reason is blank or the tool takes a project argument.
public sealed record McpSweepRow(string Tool, string Scope, ProjectRole Minimum, string? Arguments = null, bool ReadOnly = false, string? NoProject = null);
