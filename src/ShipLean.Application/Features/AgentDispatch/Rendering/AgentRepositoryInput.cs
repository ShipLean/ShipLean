namespace ShipLean.Application.Features.AgentDispatch.Rendering;

// The project's repository, resolved by the caller. Rendered as <repository url=… base-ref=…/> in promptContext.
public sealed record AgentRepositoryInput(string Url, string BaseRef);
