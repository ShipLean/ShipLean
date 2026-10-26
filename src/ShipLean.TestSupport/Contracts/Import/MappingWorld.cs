namespace ShipLean.TestSupport.Contracts.Import;

// A connection and Jira project the producer can suggest for, and a previous job that was mapped before NewStatus and NewUserAccountId
// (a status and a Jira user, by name and account id) appeared in the Jira project. That job's choices differ from the defaults: it used the
// story points field ChosenStoryPointsFieldId, sent the Story status ChosenStatus to the state ChosenTargetState, and matched the Jira user
// SamePersonAccountId to the existing ShipLean user SamePersonUserId.
public sealed record MappingWorld(
    Guid ConnectionId, string JiraProjectKey, Guid PreviousJobId, string NewStatus, string NewUserAccountId,
    string ChosenStoryPointsFieldId, string ChosenStatus, string ChosenTargetState, string SamePersonAccountId, Guid SamePersonUserId);
