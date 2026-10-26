using System.Net;
using System.Text.Json.Nodes;

namespace ShipLean.Application.Features.Import.Jira;

// What Jira answered once the cases every call treats the same (bad credentials, Jira down) are out of the way: a
// success, or a 400 or 404 that the call itself may read. Message is Jira's own error text.
public sealed record JiraReply(HttpStatusCode Status, JsonNode? Json, string? Message)
{
    public bool IsSuccess => (int)Status is >= 200 and < 300;
}
