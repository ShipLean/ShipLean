namespace ShipLean.Application.Features.Demos.Runs;

// Via is 'close', 'button' or 'mcp' (sprint.demo_requested_via). UserId is the person the run acts as: the session's creator and the link's owner.
public sealed record DemoRunRequest(Guid ProjectId, Guid SprintId, Guid ItemId, Guid UserId, string Via, string BaseUrl, string AgentStamp, Guid? AuthorizationId);
