namespace ShipLean.Contracts.WorkItems;

// Who a revision came from: a person using the app, the Jira import, or a GitHub or GitLab webhook (no person: the
// revision names the pull or merge request instead).
public enum RevisionOrigin
{
    User,
    Jira,
    Webhook,
}
