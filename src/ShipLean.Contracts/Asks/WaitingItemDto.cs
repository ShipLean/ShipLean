namespace ShipLean.Contracts.Asks;

// One item with open asks the signed-in person owns. Type is the work item type id. OldestAt is when its oldest open ask
// was raised or asked.
public sealed record WaitingItemDto(
    Guid ProjectId,
    string ProjectKey,
    string ProjectName,
    Guid Id,
    string Key,
    string Type,
    string Title,
    int OpenQuestions,
    int OpenDecisions,
    DateTimeOffset OldestAt,
    string OldestAtLabel);
