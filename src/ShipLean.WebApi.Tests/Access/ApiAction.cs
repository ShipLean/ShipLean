namespace ShipLean.WebApi.Tests.Access;

// One mapped route under /api: the method (NoVerb when the action declares none), the attribute route as mapped
// (lower-cased, no leading slash) and whether the route is [InstanceAdmin].
public sealed record ApiAction(string Method, string Template, bool InstanceAdmin)
{
    // An action with no [HttpGet]-style attribute answers every method; no row can match it, so coverage fails.
    public const string NoVerb = "(no http verb)";

    public override string ToString() => $"{Method} {Template}";
}
