using ShipLean.Contracts;

namespace ShipLean.TestSupport.AccessSweep;

// One route the access sweep calls: the least role it needs, and a body when the method takes one. RouteTemplate is the
// attribute route exactly as mapped (api/projects/{id:guid}/sprints); the sweep calls Url when given, otherwise the
// template with every {…} replaced by SHIP's id. An instance-scoped row (InstanceScoped) needs an instance admin, not a
// project role; Minimum is ignored for it.
public sealed record AccessSweepRow(
    string Method, string RouteTemplate, ProjectRole Minimum, string? Body = null, string? Url = null, bool InstanceScoped = false);
