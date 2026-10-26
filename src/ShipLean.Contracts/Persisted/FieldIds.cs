using System.Text.Json;
using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Persisted;

// The fixed field-id vocabulary, in the canonical field order. Pinned to field-ids.json by a test.
public static class FieldIds
{
    public const string Id = "id";
    public const string Key = "key";
    public const string Title = "title";
    public const string Type = "type";
    public const string State = "state";
    public const string StateCategory = "stateCategory";
    public const string Assignee = "assignee";
    public const string Priority = "priority";
    public const string Points = "points";
    public const string Tags = "tags";
    public const string Sprint = "sprint";
    public const string Parent = "parent";
    public const string Epic = "epic";
    public const string Severity = "severity";
    public const string Description = "description";
    public const string AcceptanceCriteria = "acceptanceCriteria";
    public const string ReproSteps = "reproSteps";
    public const string Goal = "goal";
    public const string StatusLine = "statusLine";
    public const string StatusVerifiedAt = "statusVerifiedAt";
    public const string Links = "links";
    public const string CreatedAt = "createdAt";
    public const string CreatedBy = "createdBy";
    public const string ChangedAt = "changedAt";
    public const string ChangedBy = "changedBy";
    public const string OriginalSystem = "originalSystem";
    public const string OriginalSystemId = "originalSystemId";
    public const string OriginalUrl = "originalUrl";

    public static IReadOnlyList<FieldDefinition> All { get; } =
    [
        new(Id, "ID", FieldKind.WorkItem, [WorkItemTypeId.Epic, WorkItemTypeId.Feature, WorkItemTypeId.Story, WorkItemTypeId.Bug, WorkItemTypeId.Task], false, [FieldUse.Criteria], [CriteriaOp.Equal, CriteriaOp.NotEqual, CriteriaOp.In], null),
        new(Key, "Key", FieldKind.Text, [WorkItemTypeId.Epic, WorkItemTypeId.Feature, WorkItemTypeId.Story, WorkItemTypeId.Bug, WorkItemTypeId.Task], false, [FieldUse.Criteria], [CriteriaOp.Equal, CriteriaOp.NotEqual, CriteriaOp.Contains, CriteriaOp.NotContains], null),
        new(Title, "Title", FieldKind.Text, [WorkItemTypeId.Epic, WorkItemTypeId.Feature, WorkItemTypeId.Story, WorkItemTypeId.Bug, WorkItemTypeId.Task], false, [FieldUse.Criteria, FieldUse.History], [CriteriaOp.Equal, CriteriaOp.NotEqual, CriteriaOp.Contains, CriteriaOp.NotContains], null),
        new(Type, "Work item type", FieldKind.Type, [WorkItemTypeId.Epic, WorkItemTypeId.Feature, WorkItemTypeId.Story, WorkItemTypeId.Bug, WorkItemTypeId.Task], false, [FieldUse.Criteria, FieldUse.History], [CriteriaOp.Equal, CriteriaOp.NotEqual, CriteriaOp.In], [new(Str("epic"), "Epic"), new(Str("feature"), "Feature"), new(Str("story"), "User Story"), new(Str("bug"), "Bug"), new(Str("task"), "Task")]),
        new(State, "State", FieldKind.State, [WorkItemTypeId.Epic, WorkItemTypeId.Feature, WorkItemTypeId.Story, WorkItemTypeId.Bug, WorkItemTypeId.Task], false, [FieldUse.Criteria, FieldUse.CardField, FieldUse.History], [CriteriaOp.Equal, CriteriaOp.NotEqual, CriteriaOp.In], null),
        new(StateCategory, "State category", FieldKind.StateCategory, [WorkItemTypeId.Epic, WorkItemTypeId.Feature, WorkItemTypeId.Story, WorkItemTypeId.Bug, WorkItemTypeId.Task], false, [FieldUse.Criteria], [CriteriaOp.Equal, CriteriaOp.NotEqual, CriteriaOp.In], [new(Str("proposed"), "Proposed"), new(Str("inProgress"), "In Progress"), new(Str("resolved"), "Resolved"), new(Str("completed"), "Completed"), new(Str("removed"), "Removed")]),
        new(Assignee, "Assigned to", FieldKind.User, [WorkItemTypeId.Epic, WorkItemTypeId.Feature, WorkItemTypeId.Story, WorkItemTypeId.Bug, WorkItemTypeId.Task], true, [FieldUse.Criteria, FieldUse.History], [CriteriaOp.Equal, CriteriaOp.NotEqual, CriteriaOp.In, CriteriaOp.IsEmpty, CriteriaOp.IsNotEmpty], null),
        new(Priority, "Priority", FieldKind.Number, [WorkItemTypeId.Epic, WorkItemTypeId.Feature, WorkItemTypeId.Story, WorkItemTypeId.Bug, WorkItemTypeId.Task], false, [FieldUse.Criteria, FieldUse.CardField, FieldUse.History], [CriteriaOp.Equal, CriteriaOp.NotEqual, CriteriaOp.GreaterThan, CriteriaOp.LessThan, CriteriaOp.In], [new(Num(1), "1"), new(Num(2), "2"), new(Num(3), "3"), new(Num(4), "4")]),
        new(Points, "Story points", FieldKind.Points, [WorkItemTypeId.Epic, WorkItemTypeId.Feature, WorkItemTypeId.Story, WorkItemTypeId.Bug, WorkItemTypeId.Task], true, [FieldUse.Criteria, FieldUse.History], [CriteriaOp.Equal, CriteriaOp.NotEqual, CriteriaOp.GreaterThan, CriteriaOp.LessThan, CriteriaOp.In, CriteriaOp.IsEmpty, CriteriaOp.IsNotEmpty], null),
        new(Tags, "Tags", FieldKind.Tags, [WorkItemTypeId.Epic, WorkItemTypeId.Feature, WorkItemTypeId.Story, WorkItemTypeId.Bug, WorkItemTypeId.Task], false, [FieldUse.Criteria, FieldUse.History], [CriteriaOp.Contains, CriteriaOp.NotContains, CriteriaOp.IsEmpty, CriteriaOp.IsNotEmpty], null),
        new(Sprint, "Sprint", FieldKind.Sprint, [WorkItemTypeId.Story, WorkItemTypeId.Bug, WorkItemTypeId.Task], true, [FieldUse.Criteria, FieldUse.CardField, FieldUse.History], [CriteriaOp.Equal, CriteriaOp.NotEqual, CriteriaOp.In, CriteriaOp.IsEmpty, CriteriaOp.IsNotEmpty], null),
        new(Parent, "Parent", FieldKind.WorkItem, [WorkItemTypeId.Feature, WorkItemTypeId.Story, WorkItemTypeId.Bug, WorkItemTypeId.Task], true, [FieldUse.Criteria, FieldUse.CardField, FieldUse.History], [CriteriaOp.Equal, CriteriaOp.NotEqual, CriteriaOp.In, CriteriaOp.IsEmpty, CriteriaOp.IsNotEmpty], null),
        new(Epic, "Epic", FieldKind.WorkItem, [WorkItemTypeId.Feature], true, [FieldUse.Criteria], [CriteriaOp.Equal, CriteriaOp.NotEqual, CriteriaOp.In, CriteriaOp.IsEmpty, CriteriaOp.IsNotEmpty], null),
        new(Severity, "Severity", FieldKind.Number, [WorkItemTypeId.Bug], true, [FieldUse.Criteria, FieldUse.CardField, FieldUse.History], [CriteriaOp.Equal, CriteriaOp.NotEqual, CriteriaOp.GreaterThan, CriteriaOp.LessThan, CriteriaOp.In, CriteriaOp.IsEmpty, CriteriaOp.IsNotEmpty], [new(Num(1), "1 - Critical"), new(Num(2), "2 - High"), new(Num(3), "3 - Medium"), new(Num(4), "4 - Low")]),
        new(Description, "Description", FieldKind.Markdown, [WorkItemTypeId.Epic, WorkItemTypeId.Feature, WorkItemTypeId.Story, WorkItemTypeId.Bug, WorkItemTypeId.Task], true, [FieldUse.History], [], null),
        new(AcceptanceCriteria, "Acceptance criteria", FieldKind.Checklist, [WorkItemTypeId.Story], false, [FieldUse.History], [], null),
        new(ReproSteps, "Repro steps", FieldKind.Markdown, [WorkItemTypeId.Bug], true, [FieldUse.History], [], null),
        new(Goal, "Goal", FieldKind.Text, [WorkItemTypeId.Feature], true, [FieldUse.History], [], null),
        new(StatusLine, "Status", FieldKind.Text, [WorkItemTypeId.Feature], true, [FieldUse.History], [], null),
        new(StatusVerifiedAt, "Status verified", FieldKind.Date, [WorkItemTypeId.Feature], true, [FieldUse.History], [], null),
        new(Links, "Links", FieldKind.Links, [WorkItemTypeId.Epic, WorkItemTypeId.Feature, WorkItemTypeId.Story, WorkItemTypeId.Bug, WorkItemTypeId.Task], false, [FieldUse.History], [], null),
        new(CreatedAt, "Created", FieldKind.Date, [WorkItemTypeId.Epic, WorkItemTypeId.Feature, WorkItemTypeId.Story, WorkItemTypeId.Bug, WorkItemTypeId.Task], false, [FieldUse.Criteria, FieldUse.CardField], [CriteriaOp.GreaterThan, CriteriaOp.LessThan], null),
        new(CreatedBy, "Created by", FieldKind.User, [WorkItemTypeId.Epic, WorkItemTypeId.Feature, WorkItemTypeId.Story, WorkItemTypeId.Bug, WorkItemTypeId.Task], true, [FieldUse.Criteria, FieldUse.CardField], [CriteriaOp.Equal, CriteriaOp.NotEqual, CriteriaOp.In, CriteriaOp.IsEmpty, CriteriaOp.IsNotEmpty], null),
        new(ChangedAt, "Changed", FieldKind.Date, [WorkItemTypeId.Epic, WorkItemTypeId.Feature, WorkItemTypeId.Story, WorkItemTypeId.Bug, WorkItemTypeId.Task], false, [FieldUse.Criteria, FieldUse.CardField], [CriteriaOp.GreaterThan, CriteriaOp.LessThan], null),
        new(ChangedBy, "Changed by", FieldKind.User, [WorkItemTypeId.Epic, WorkItemTypeId.Feature, WorkItemTypeId.Story, WorkItemTypeId.Bug, WorkItemTypeId.Task], true, [FieldUse.Criteria, FieldUse.CardField], [CriteriaOp.Equal, CriteriaOp.NotEqual, CriteriaOp.In, CriteriaOp.IsEmpty, CriteriaOp.IsNotEmpty], null),
        new(OriginalSystem, "Original system", FieldKind.Text, [WorkItemTypeId.Epic, WorkItemTypeId.Feature, WorkItemTypeId.Story, WorkItemTypeId.Bug, WorkItemTypeId.Task], true, [FieldUse.Criteria], [CriteriaOp.Equal, CriteriaOp.NotEqual, CriteriaOp.IsEmpty, CriteriaOp.IsNotEmpty], [new(Str("jira"), "Jira")]),
        new(OriginalSystemId, "Original system ID", FieldKind.Text, [WorkItemTypeId.Epic, WorkItemTypeId.Feature, WorkItemTypeId.Story, WorkItemTypeId.Bug, WorkItemTypeId.Task], true, [FieldUse.Criteria], [CriteriaOp.Equal, CriteriaOp.NotEqual, CriteriaOp.Contains, CriteriaOp.NotContains, CriteriaOp.IsEmpty, CriteriaOp.IsNotEmpty], null),
        new(OriginalUrl, "Original URL", FieldKind.Text, [WorkItemTypeId.Epic, WorkItemTypeId.Feature, WorkItemTypeId.Story, WorkItemTypeId.Bug, WorkItemTypeId.Task], true, [], [], null),
    ];

    private static JsonElement Str(string value) => JsonSerializer.SerializeToElement(value);

    private static JsonElement Num(int value) => JsonSerializer.SerializeToElement(value);
}
