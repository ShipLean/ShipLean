namespace ShipLean.Contracts.Import;

// What the importer sends to Jira with every call. The client keeps no state, so nothing here outlives a call.
public sealed record JiraCredentials(string BaseUrl, JiraKind Kind, string Email, string Token)
{
    public override string ToString() => $"JiraCredentials {{ BaseUrl = {BaseUrl}, Kind = {Kind}, Email = {Email}, Token = *** }}";
}
