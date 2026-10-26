namespace ShipLean.Application.Features.WorkItems.Reads.Changes;

// One row of the changes query; Dapper maps the columns onto the constructor by name.
internal sealed record ChangeRow(
    Guid WorkItemId,
    string Key,
    string Title,
    int Kind,
    string Tie,
    Guid? ActorId,
    string? ActorName,
    DateTime At,
    string? Changes,
    string? Body,
    int? Revision);
