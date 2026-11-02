namespace ShipLean.Contracts.Usage;

// Every client route id: the keys of routeDefs in src/ShipLean.Client/src/routes.ts. A test parses the client file and fails on drift.
public static class ScreenIds
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        "login", "newProject", "instanceSettings", "profile", "importStart", "storiesBacklog", "featuresBacklog",
        "epicsBacklog", "workItem", "workItemAttachment", "featureBoard", "storiesBoard", "featuresBoard", "epicsBoard",
        "sprints", "sprintBacklog", "taskboard", "capacity", "sprintRetro", "retros", "retroInsights", "jiraImport",
        "projectSettings", "authorize",
    };
}
