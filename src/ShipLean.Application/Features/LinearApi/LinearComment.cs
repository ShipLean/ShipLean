namespace ShipLean.Application.Features.LinearApi;

public sealed record LinearComment(Guid Id, Guid IssueId, string Body, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, LinearPerson? Author);
