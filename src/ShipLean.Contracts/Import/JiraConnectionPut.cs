namespace ShipLean.Contracts.Import;

// Saving a connection. The token is write-only; Replace is a new Put. ToString never prints it (request logging at Trace).
public sealed record JiraConnectionPut(string BaseUrl, JiraKind Kind, string Email, string Token)
{
    public override string ToString() => $"JiraConnectionPut {{ BaseUrl = {BaseUrl}, Kind = {Kind}, Email = {Email}, Token = *** }}";
}
