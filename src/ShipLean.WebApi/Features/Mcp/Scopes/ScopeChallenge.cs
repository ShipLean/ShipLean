namespace ShipLean.WebApi.Features.Mcp.Scopes;

// The 403 step-up: the scopes to ask for are the granted ones plus the missing one, sorted, so the client sends the person
// back to consent once for all of them.
public static class ScopeChallenge
{
    public static string Header(IEnumerable<string> granted, string required, string resourceMetadataUrl)
    {
        var scope = string.Join(' ', granted.Append(required).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        return $"Bearer error=\"insufficient_scope\", scope=\"{scope}\", resource_metadata=\"{resourceMetadataUrl}\"";
    }
}
