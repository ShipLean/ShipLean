namespace ShipLean.Contracts.Prompts;

// Agent null: the request's agent when ICurrentRequest.Exists, else other. UserId null: the request's user when there is a request, else none.
public sealed record PromptRequest(Guid? ProjectId, PromptKey Key, string? Agent = null, string? ItemKey = null, Guid? UserId = null);
