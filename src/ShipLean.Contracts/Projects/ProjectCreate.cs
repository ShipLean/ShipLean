using ShipLean.Contracts.Sprints;

namespace ShipLean.Contracts.Projects;

// CopyFromProjectId: start from the states and settings of a project the caller is in. FirstSprint: null = no Sprint 1.
public sealed record ProjectCreate(
    string Name, string Key, Guid? CopyFromProjectId, IReadOnlyList<string> MemberEmails,
    IReadOnlyList<int> WorkingDays, SprintCreate? FirstSprint);
