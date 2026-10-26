using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Import;

namespace ShipLean.Application.Fakes.Import;

// Suggests the mapping a test arranged for a connection and Jira project. With a previous job it keeps that job's saved choices (read through
// IImportJobs.GetMappingAsync) for every row the job covered, and flags the issue types, statuses and users it did not cover as new, with the
// suggested defaults; without one nothing is new.
public sealed class InMemoryImportMappings(IImportJobs jobs) : IImportMappings
{
    private readonly object _gate = new();
    private readonly Dictionary<(Guid, string), ImportMappingDto> _suggestions = [];

    public void ArrangeSuggestion(Guid connectionId, string jiraProjectKey, ImportMappingDto suggestion)
    {
        lock (_gate)
        {
            _suggestions[(connectionId, jiraProjectKey)] = suggestion;
        }
    }

    public async Task<ImportMappingDto> SuggestAsync(Guid connectionId, string jiraProjectKey, Guid? previousJobId, CancellationToken cancellationToken = default)
    {
        ImportMappingDto? current;
        lock (_gate)
        {
            _suggestions.TryGetValue((connectionId, jiraProjectKey), out current);
        }
        if (current is null)
        {
            throw ApiException.NotFound("That Jira connection or project does not exist.");
        }
        if (previousJobId is not { } jobId)
        {
            return current with
            {
                IssueTypes = [.. current.IssueTypes.Select(t => t with { IsNewSinceLastRun = false })],
                Statuses = [.. current.Statuses.Select(s => s with { IsNewSinceLastRun = false })],
                Users = [.. current.Users.Select(u => u with { IsNewSinceLastRun = false })],
            };
        }
        var then = await jobs.GetMappingAsync(jobId, cancellationToken);
        return current with
        {
            IssueTypes = [.. current.IssueTypes.Select(t => then.IssueTypes.FirstOrDefault(o => o.JiraType == t.JiraType) is { } kept
                ? t with { Target = kept.Target, IsNewSinceLastRun = false }
                : t with { IsNewSinceLastRun = true })],
            Statuses = [.. current.Statuses.Select(s => then.Statuses.FirstOrDefault(o => o.JiraType == s.JiraType && o.JiraStatus == s.JiraStatus) is { } kept
                ? s with { TargetState = kept.TargetState, TargetCategory = kept.TargetCategory, IsNewSinceLastRun = false }
                : s with { IsNewSinceLastRun = true })],
            Users = [.. current.Users.Select(u => then.Users.FirstOrDefault(o => o.JiraAccountId == u.JiraAccountId) is { } kept
                ? u with { Resolution = kept.Resolution, User = kept.User, IsNewSinceLastRun = false }
                : u with { IsNewSinceLastRun = true })],
            Fields = current.Fields with
            {
                StoryPointsFieldId = then.Fields.StoryPointsFieldId,
                IncludeComponents = then.Fields.IncludeComponents,
                IncludeComments = then.Fields.IncludeComments,
                IncludeAttachments = then.Fields.IncludeAttachments,
                IncludeChangelog = then.Fields.IncludeChangelog,
            },
        };
    }
}
