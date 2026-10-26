using ShipLean.Contracts.Import;

namespace ShipLean.Application.Features.Import.Apply;

// One import job's working memory: the choices it applies and what it has already resolved (users, states, sprints, items by Jira key),
// so each is looked up or created once. One caller drives a run at a time; a run lives as long as the job's pass over the issues.
public sealed class ImportRun(Guid projectId, Guid jobId, JiraCredentials credentials, ImportMappingDto mapping)
{
    internal Dictionary<string, Guid> Users { get; } = [];

    internal Dictionary<(string Type, string Name), Guid> States { get; } = new();

    internal Dictionary<long, Guid> Sprints { get; } = [];

    internal Dictionary<long, (DateOnly Start, DateOnly End)> SprintDates { get; } = [];

    internal Dictionary<string, Guid> ItemIds { get; } = [];

    // Items an earlier run wrote to the project, by Jira key: the parents and links a fresh run (or a retry of one issue) can still resolve.
    internal Dictionary<string, Guid> EarlierItemIds { get; } = [];

    internal List<string> SprintWarnings { get; } = [];

    public Guid ProjectId { get; } = projectId;

    public Guid JobId { get; } = jobId;

    public JiraCredentials Credentials { get; } = credentials;

    public ImportMappingDto Mapping { get; } = mapping;

    // What Prepare changed to make the Jira sprints fit (a sprint moved to start after the one before it).
    public IReadOnlyList<string> Warnings => SprintWarnings;

    // The work item ids written so far in this run, by Jira key.
    public IReadOnlyDictionary<string, Guid> Items => ItemIds;
}
