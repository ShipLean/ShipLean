namespace ShipLean.Contracts.Import.Jira;

// One issue as the importer needs it. Type and Status are Jira's names; Description is plain markdown-ish text (not ADF or
// wiki markup); Changelog is oldest first; ParentKey is the parent or the epic (Cloud serves both as parent; Server serves an epic through the Epic Link field, customfield_10014). Numbers holds the number fields the search asked for
// (field id to value), absent when the issue has no value.
public sealed record JiraIssue(
    string Key, string Id, string Type, string Status, string Summary, string? Description,
    JiraUser? Assignee, JiraUser? Reporter, string? Priority, IReadOnlyDictionary<string, decimal> Numbers,
    IReadOnlyList<JiraSprint> Sprints, string? ParentKey, IReadOnlyList<string> Labels, IReadOnlyList<string> Components,
    DateTimeOffset Created, DateTimeOffset Updated,
    IReadOnlyList<JiraIssueLink> Links, IReadOnlyList<JiraAttachment> Attachments, IReadOnlyList<JiraChangelogEntry> Changelog);
