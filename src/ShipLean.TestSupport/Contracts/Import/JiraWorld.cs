using ShipLean.Contracts.Import;

namespace ShipLean.TestSupport.Contracts.Import;

// The Jira the client under test talks to. IssueKeys: every issue of ProjectKey in key order. CommentedIssueKey has CommentCount
// comments. Some issue carries an attachment whose bytes are AttachmentBytes. EpicChildKey is a story under the epic EpicKey. BotAccountId is the account id of the automation user who made some change (Cloud only; Server has no bot type, so null).
// Credentials and Client already carry the fault.
public sealed record JiraWorld(
    JiraCredentials Credentials, IJiraClient Client, string ProjectKey, IReadOnlyList<string> IssueKeys,
    string CommentedIssueKey, int CommentCount, byte[] AttachmentBytes, string EpicKey, string EpicChildKey, string? BotAccountId);
