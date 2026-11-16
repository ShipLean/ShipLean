namespace ShipLean.Contracts.TaskChanges;

// Unreadable: 401, 403, a 404 on the repository, or a network failure. Missing: the repository can be read but the commit can't.
public enum CommitReadStatus
{
    Found,
    Unreadable,
    Missing,
}
