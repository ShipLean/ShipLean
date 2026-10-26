using ShipLean.Contracts.Import.Jira;
using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Import;

// A Jira status of one Jira issue type lands in the target type's state called TargetState. CreatesState: no such state exists
// yet (a new project's defaults count as existing), so the run creates it with TargetCategory, which defaults from Jira's category.
// The server recomputes CreatesState at run time and never trusts the client's value.
public sealed record StatusMappingDto(
    string JiraType, string JiraStatus, JiraStatusCategory JiraCategory, int Count,
    string TargetState, StateCategory TargetCategory, bool CreatesState, bool IsNewSinceLastRun);
