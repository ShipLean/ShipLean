using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Events;

// Something that happened inside one project. Ids only, never whole DTOs: a client refetches what it shows. The "type"
// discriminator is the wire shape (mirrored in src/ShipLean.Client/src/api/events.ts).
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(WorkItemCreated), "workItemCreated")]
[JsonDerivedType(typeof(WorkItemUpdated), "workItemUpdated")]
[JsonDerivedType(typeof(WorkItemMoved), "workItemMoved")]
[JsonDerivedType(typeof(WorkItemReordered), "workItemReordered")]
[JsonDerivedType(typeof(WorkItemRemoved), "workItemRemoved")]
[JsonDerivedType(typeof(BoardSettingsChanged), "boardSettingsChanged")]
[JsonDerivedType(typeof(SprintChanged), "sprintChanged")]
[JsonDerivedType(typeof(StatesChanged), "statesChanged")]
[JsonDerivedType(typeof(ProjectChanged), "projectChanged")]
[JsonDerivedType(typeof(PresenceChanged), "presenceChanged")]
[JsonDerivedType(typeof(ImportProgress), "importProgress")]
[JsonDerivedType(typeof(CommentsChanged), "commentsChanged")]
[JsonDerivedType(typeof(AttachmentsChanged), "attachmentsChanged")]
[JsonDerivedType(typeof(LinksChanged), "linksChanged")]
public abstract record ProjectEvent(Guid ProjectId)
{
    // The browser tab whose request caused the event (its ClientHeader), stamped by the bus when the event is published;
    // null for a write with no such header (a script, a job). Always written to the JSON. A tab ignores its own events.
    public string? ClientId { get; init; }
}
