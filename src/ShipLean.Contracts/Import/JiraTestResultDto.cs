namespace ShipLean.Contracts.Import;

// Ok: Who is the signed-in Jira user. Not ok: Message says what to check. Never an error response for a bad connection.
public sealed record JiraTestResultDto(bool Ok, JiraWhoDto? Who, string? Message);
