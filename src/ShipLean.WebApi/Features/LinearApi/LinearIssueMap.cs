using System.Text;
using ShipLean.Application.Features.LinearApi;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.LinearApi;

// A work item as Linear's Issue, the same mapping as the webhook's issue (AgentContextRenderer): identifier is the item key,
// the team is the project, `project` is the Feature the item sits under, `parent` the parent item. Nested objects carry what
// ShipLean has (an id, a person's name); the rest of a nested object is the field defaults.
internal static class LinearIssueMap
{
    private static readonly string[] PriorityLabels = ["No priority", "Urgent", "High", "Medium", "Low"];

    public static IReadOnlyDictionary<string, object?> Of(LinearIssue issue, string baseUrl, LinearRequest request)
    {
        var url = McpOpenUrls.WorkItem(baseUrl, issue.ProjectKey, issue.Key);
        return new Dictionary<string, object?>
        {
            ["id"] = issue.Id.ToString(),
            ["identifier"] = issue.Key,
            ["number"] = (double)issue.Number,
            ["title"] = issue.Title,
            ["description"] = issue.Description,
            ["url"] = url,
            ["branchName"] = BranchName(issue),
            ["labelIds"] = issue.Tags.ToList(),
            // ShipLean priority 1 is the most urgent of 1-4, as in Linear (1 urgent .. 4 low; 0 none).
            ["priority"] = (double)issue.Priority,
            ["priorityLabel"] = PriorityLabels[Math.Clamp(issue.Priority, 0, 4)],
            ["createdAt"] = issue.CreatedAt,
            ["updatedAt"] = issue.UpdatedAt,
            ["creator"] = issue.Creator is { } creator ? LinearPersonMap.Of(creator) : null,
            ["assignee"] = issue.Assignee is { } assignee ? LinearPersonMap.Of(assignee) : null,
            ["team"] = Id(issue.ProjectId),
            ["state"] = Id(issue.StateId),
            ["parent"] = issue.ParentId is { } parent ? Id(parent) : null,
            ["project"] = issue.FeatureId is { } feature ? Id(feature) : null,
            ["comments"] = (LinearLazy)(async context =>
            {
                var cancellationToken = context.RequestAborted;
                var page = await request.Reads.CommentsAsync(request.Caller.ConnectionId, issue.Id, LinearPaging.First(context), LinearPaging.After(context), cancellationToken);
                return LinearPaging.Connection([.. page.Items.Select(c => LinearCommentMap.Of(c, url))], page.HasNextPage);
            }),
        };
    }

    private static Dictionary<string, object?> Id(Guid id) => new() { ["id"] = id.ToString() };

    // Linear's suggested branch name: the identifier and a slug of the title, lower case.
    private static string BranchName(LinearIssue issue)
    {
        var slug = new StringBuilder();
        foreach (var c in issue.Title.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                slug.Append(c);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }
        var tail = slug.ToString().Trim('-');
        tail = tail.Length > 50 ? tail[..50].Trim('-') : tail;
        return tail.Length == 0 ? issue.Key.ToLowerInvariant() : $"{issue.Key.ToLowerInvariant()}-{tail}";
    }
}
