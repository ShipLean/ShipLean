namespace ShipLean.Application.Features.LinearApi;

public sealed record LinearCommentPage(IReadOnlyList<LinearComment> Items, bool HasNextPage);
