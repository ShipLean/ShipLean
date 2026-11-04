namespace ShipLean.WebApi.Tests.Mcp.Sprint;

public sealed record DemoCall(Guid SprintId, string BaseUrl, string AgentStamp, Guid? AuthorizationId);
