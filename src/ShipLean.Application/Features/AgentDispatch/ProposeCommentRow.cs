namespace ShipLean.Application.Features.AgentDispatch;

// A comment mirrored from GitHub, by the login and with the GitHub text its provenance row holds (AgentContextLoader.LoadProposeAsync).
internal sealed record ProposeCommentRow(Guid Id, string AuthorLogin, DateTimeOffset CreatedAt, string Body);
