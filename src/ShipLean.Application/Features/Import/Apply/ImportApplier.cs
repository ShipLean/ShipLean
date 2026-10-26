using ShipLean.Application.Features.Import.Mapping;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Import.Apply;

public sealed class ImportApplier(
    IWorkItemStore workItems, ICommentStore comments, IAttachmentStore attachments, IJiraClient jira,
    StateProvisioner states, SprintResolver sprints, UserResolver users, ImportErrorWriter errors, EarlierItems earlier, IProjectEvents events) : IImportApplier
{
    public ImportRun Begin(Guid projectId, Guid jobId, JiraCredentials credentials, ImportMappingDto mapping)
        => new(projectId, jobId, credentials, mapping);

    public async Task PrepareAsync(ImportRun run, IReadOnlyList<JiraSprint> jiraSprints, CancellationToken cancellationToken = default)
    {
        await earlier.LoadAsync(run, cancellationToken);
        foreach (var row in run.Mapping.Statuses)
        {
            if (TypeOf(run, row.JiraType) is { } type)
            {
                await states.EnsureAsync(run, type, row.TargetState, row.TargetCategory, cancellationToken);
            }
        }

        sprints.Schedule(run, jiraSprints);
        foreach (var sprint in jiraSprints)
        {
            await sprints.ResolveAsync(run, sprint, cancellationToken);
        }
    }

    public async Task<ApplyOutcome> ApplyIssueAsync(ImportRun run, JiraIssue issue, CancellationToken cancellationToken = default)
    {
        if (TypeOf(run, issue.Type) is not { } type)
        {
            return ApplyOutcome.Skipped;
        }

        try
        {
            var unresolved = new List<string>();
            var item = await BuildAsync(run, issue, type, unresolved, cancellationToken);
            var result = await workItems.ImportAsync(run.ProjectId, run.JobId, item, cancellationToken);
            run.ItemIds[issue.Key] = result.Id;
            return new ApplyOutcome(result.Id, result.Key, result.Created, false, unresolved);
        }
        catch (ApiException rejected)
        {
            await errors.WriteAsync(run.JobId, ImportStep.Issues, "issue", issue.Key, rejected.Message, cancellationToken);
            return new ApplyOutcome(null, null, false, true, []);
        }
    }

    public async Task<int> ApplyCommentsAsync(ImportRun run, JiraIssue issue, Guid itemId, CancellationToken cancellationToken = default)
    {
        if (!run.Mapping.Fields.IncludeComments)
        {
            return 0;
        }

        var written = 0;
        foreach (var comment in await jira.GetCommentsAsync(run.Credentials, issue.Key, cancellationToken))
        {
            try
            {
                var author = await users.ResolveAsync(run, comment.Author, cancellationToken);
                await comments.ImportAsync(itemId, new ImportedComment(JiraImportUser.System, comment.Id, author, comment.Body, comment.Created), cancellationToken);
                written++;
            }
            catch (ApiException rejected)
            {
                await errors.WriteAsync(run.JobId, ImportStep.Comments, "comment", $"{issue.Key}/{comment.Id}", rejected.Message, cancellationToken);
            }
        }

        // The store's import path publishes nothing; one event per item tells subscribers (the search index) its comments changed.
        if (written > 0)
        {
            await events.PublishAsync(new CommentsChanged(run.ProjectId, itemId));
        }

        return written;
    }

    public async Task<int> ApplyAttachmentsAsync(ImportRun run, JiraIssue issue, Guid itemId, CancellationToken cancellationToken = default)
    {
        if (!run.Mapping.Fields.IncludeAttachments)
        {
            return 0;
        }

        var written = 0;
        foreach (var attachment in issue.Attachments)
        {
            try
            {
                await using var content = await jira.OpenAttachmentAsync(run.Credentials, attachment, cancellationToken);
                // Jira gives no uploader on the attachment shape, so the file is attributed to the Jira import user.
                var imported = new ImportedAttachment(JiraImportUser.System, attachment.Id, attachment.Filename, attachment.MimeType, attachment.Size, JiraImportUser.Id, issue.Created);
                await attachments.ImportAsync(itemId, imported, content, cancellationToken);
                written++;
            }
            catch (ApiException rejected)
            {
                await errors.WriteAsync(run.JobId, ImportStep.Attachments, "attachment", $"{issue.Key}/{attachment.Id}", rejected.Message, cancellationToken);
            }
        }

        return written;
    }

    // An issue applied in this run, else one an earlier run wrote.
    private static bool TryFindItem(ImportRun run, string key, out Guid id) => run.ItemIds.TryGetValue(key, out id) || run.EarlierItemIds.TryGetValue(key, out id);

    private static WorkItemTypeId? TypeOf(ImportRun run, string jiraType)
        => run.Mapping.IssueTypes.FirstOrDefault(t => t.JiraType == jiraType) is { } row ? TargetTypes.WorkItemType(row.Target) : null;

    private async Task<Guid?> StateOfAsync(ImportRun run, WorkItemTypeId type, string jiraType, string status, CancellationToken cancellationToken)
        => run.Mapping.Statuses.FirstOrDefault(s => s.JiraType == jiraType && s.JiraStatus == status) is { } row
            ? await states.EnsureAsync(run, type, row.TargetState, row.TargetCategory, cancellationToken)
            : null;

    private async Task<ImportedWorkItem> BuildAsync(ImportRun run, JiraIssue issue, WorkItemTypeId type, List<string> unresolved, CancellationToken cancellationToken)
    {
        var stateId = await StateOfAsync(run, type, issue.Type, issue.Status, cancellationToken)
            ?? throw ApiException.Validation($"The mapping has no state for {issue.Type} status {issue.Status}.");
        var number = KeyNumber(issue.Key);
        var creator = await users.ResolveAsync(run, issue.Reporter, cancellationToken);
        var lastEditor = issue.Changelog.OrderBy(c => c.Created).LastOrDefault() is { } last ? await users.ResolveAsync(run, last.Author, cancellationToken) : creator;
        Guid? assignee = issue.Assignee is null ? null : await users.ResolveAsync(run, issue.Assignee, cancellationToken);
        Guid? sprint = issue.Sprints.Count == 0 ? null : await sprints.ResolveAsync(run, ChosenSprint(issue.Sprints), cancellationToken);

        Guid? parent = null;
        if (issue.ParentKey is { } parentKey)
        {
            if (TryFindItem(run, parentKey, out var parentId))
            {
                parent = parentId;
            }
            else
            {
                unresolved.Add(parentKey);
            }
        }

        var links = LinksOf(run, issue, unresolved);
        var fields = run.Mapping.Fields;
        decimal? points = fields.StoryPointsFieldId.Length > 0 && issue.Numbers.TryGetValue(fields.StoryPointsFieldId, out var value) ? value : null;
        var pointsName = fields.Candidates.FirstOrDefault(c => c.FieldId == fields.StoryPointsFieldId)?.Name ?? string.Empty;
        var revisions = fields.IncludeChangelog
            ? await ChangelogMapper.MapAsync(
                issue, pointsName,
                async status => await StateOfAsync(run, type, issue.Type, status, cancellationToken),
                author => users.ResolveAsync(run, author, cancellationToken))
            : [];
        IEnumerable<string> tags = fields.IncludeComponents ? issue.Labels.Concat(issue.Components) : issue.Labels;
        var url = $"{run.Credentials.BaseUrl.TrimEnd('/')}/browse/{issue.Key}";
        return new ImportedWorkItem(
            new OriginalSystemDto(JiraImportUser.System, issue.Key, url), number, type, issue.Summary, issue.Description ?? string.Empty, [], string.Empty,
            null, stateId, assignee, sprint, PriorityOf(issue.Priority), points, parent, [.. tags.Distinct()], links,
            creator, issue.Created, lastEditor, issue.Updated, issue.Updated, revisions);
    }

    // The sprint the issue is in now: the one that has not ended, else the latest.
    private static JiraSprint ChosenSprint(IReadOnlyList<JiraSprint> sprints)
        => sprints.FirstOrDefault(s => s.State == "active") ?? sprints.FirstOrDefault(s => s.State == "future") ?? sprints[^1];

    // Jira links to issues already applied. A "blocks" link is kept from the blocked side only, and a symmetrical link from the outward side only, as Jira lists each on both issues.
    private static List<LinkValue> LinksOf(ImportRun run, JiraIssue issue, List<string> unresolved)
    {
        var links = new List<LinkValue>();
        foreach (var link in issue.Links)
        {
            var name = link.TypeName.ToLowerInvariant();
            LinkType? type = name switch
            {
                "blocks" when link.Direction == JiraLinkDirection.Inward => LinkType.BlockedBy,
                "duplicate" when link.Direction == JiraLinkDirection.Outward => LinkType.Duplicate,
                "blocks" or "duplicate" => null,
                _ when link.Direction == JiraLinkDirection.Outward => LinkType.Related,
                _ => null,
            };
            if (type is null)
            {
                continue;
            }

            if (TryFindItem(run, link.OtherKey, out var other))
            {
                links.Add(new LinkValue(type.Value, other, null, null, null));
            }
            else
            {
                unresolved.Add(link.OtherKey);
            }
        }

        return links;
    }

    private static int KeyNumber(string key)
        => int.TryParse(key[(key.LastIndexOf('-') + 1)..], out var number) && number > 0
            ? number
            : throw ApiException.Validation($"{key} is not a Jira issue key.");

    private static int PriorityOf(string? priority) => priority?.ToLowerInvariant() switch
    {
        "highest" or "blocker" or "critical" => 1,
        "high" or "major" => 2,
        "low" or "minor" or "lowest" or "trivial" => 4,
        _ => 3,
    };
}
