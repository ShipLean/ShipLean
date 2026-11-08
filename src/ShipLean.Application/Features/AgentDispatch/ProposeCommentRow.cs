namespace ShipLean.Application.Features.AgentDispatch;

// A comment mirrored from GitHub, by the login its provenance row names (AgentContextLoader.LoadProposeAsync).
internal sealed record ProposeCommentRow(Guid Id, string AuthorLogin, DateTimeOffset CreatedAt, string Body);
