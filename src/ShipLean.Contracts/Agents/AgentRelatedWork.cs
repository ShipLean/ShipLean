namespace ShipLean.Contracts.Agents;

// An existing item a propose session may see (D10): only its key, type, state and title, its pull request URLs and their merge
// commits. Never its description, comments or people.
public sealed record AgentRelatedWork(
    string Key,
    string Type,
    string State,
    string Title,
    IReadOnlyList<string> PullRequestUrls,
    IReadOnlyList<AgentMergeCommit> MergeCommits);
