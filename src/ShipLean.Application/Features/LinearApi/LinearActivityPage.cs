namespace ShipLean.Application.Features.LinearApi;

public sealed record LinearActivityPage(IReadOnlyList<LinearActivity> Items, bool HasNextPage);
