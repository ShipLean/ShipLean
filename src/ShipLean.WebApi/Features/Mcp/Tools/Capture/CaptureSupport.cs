using System.Text.Json;
using Agoda.IoC.Core;
using ModelContextProtocol.Protocol;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Transport;

namespace ShipLean.WebApi.Features.Mcp.Tools.Capture;

// What the capture and refine tools share: the instance's base URL for open_url, resolving an agent's words into ids, and the tool result shape.
[RegisterPerRequest]
public sealed class CaptureSupport(IServiceProvider services, AgentBaseUrl baseUrl, ItemResolver items, SprintResolver sprints, ProjectResolver projects)
{
    public const int MaxItems = 25;

    private HttpContext Http => services.GetRequiredService<IHttpContextAccessor>().HttpContext!;

    public async Task<string> BaseUrlAsync(CancellationToken cancellationToken)
        => (await baseUrl.ReadAsync(Http.Request, cancellationToken)).Issuer;

    // The agent authorization this call runs under, or null for a caller that has none (the gateway).
    public Guid? AgentAuthorizationId()
        => Guid.TryParse(Http.User.FindFirst(AgentClaims.AuthorizationClaim)?.Value, out var id) ? id : null;

    // The project an item key belongs to (its key prefix), for the result's `project` and open_url.
    public Task<ResolvedProject> ProjectOfAsync(ResolvedItem item, CancellationToken cancellationToken)
        => projects.ResolveAsync(item.Key[..item.Key.IndexOf('-')], forWrite: true, cancellationToken);

    public static CallToolResult Reply(McpResult result, string summary)
        => McpToolResult.Of(JsonSerializer.SerializeToElement(result, result.GetType()), summary);

    public static WorkItemTypeId ParseType(string? text, string field)
        => new string((text ?? string.Empty).Where(char.IsLetter).ToArray()).ToLowerInvariant() switch
        {
            "epic" => WorkItemTypeId.Epic,
            "feature" => WorkItemTypeId.Feature,
            "story" or "userstory" => WorkItemTypeId.Story,
            "bug" => WorkItemTypeId.Bug,
            "task" => WorkItemTypeId.Task,
            _ => throw ApiException.Validation($"\"{text}\" is not a work item type: use epic, feature, story, bug or task.", field),
        };

    public static string TypeName(WorkItemTypeId type) => type.ToString().ToLowerInvariant();

    public async Task<PlannedItem> PlanAsync(CaptureItem item, ResolvedProject project, int index, CancellationToken cancellationToken)
    {
        var at = $"items[{index}]";
        var type = ParseType(item.Type, $"{at}.type");
        if (string.IsNullOrWhiteSpace(item.Title))
        {
            throw ApiException.Validation("Every item needs a title.", $"{at}.title");
        }

        ResolvedItem? parent = string.IsNullOrWhiteSpace(item.Parent)
            ? null
            : await items.ResolveAsync(item.Parent, ProjectRole.Member, project.Id, $"{at}.parent", cancellationToken);
        ResolvedSprint? sprint = string.IsNullOrWhiteSpace(item.Sprint) || NameMatcher.Same(item.Sprint.Trim(), "none")
            ? null
            : await sprints.ResolveAsync(project.Id, item.Sprint, $"{at}.sprint", cancellationToken);
        return new PlannedItem(
            type, item.Title.Trim(), item.Description, [.. item.AcceptanceCriteria ?? []], item.ReproSteps, item.Severity, item.Points,
            [.. item.Tags ?? []], item.Priority, parent?.Id, parent?.Key, sprint?.Id, sprint?.Name);
    }

    public static WorkItemCreate ToCreate(Guid projectId, PlannedItem item) => new(projectId, item.Type, item.Title, item.ParentId, item.SprintId, new WorkItemChanges
    {
        Description = item.Description is null ? null : new FieldSet<string>(item.Description),
        AcceptanceCriteria = item.AcceptanceCriteria.Count == 0 ? null : new FieldSet<IReadOnlyList<ChecklistItem>>([.. item.AcceptanceCriteria.Select(text => new ChecklistItem(text, false))]),
        ReproSteps = item.ReproSteps is null ? null : new FieldSet<string>(item.ReproSteps),
        Severity = item.Severity is null ? null : new FieldSet<int?>(item.Severity),
        Points = item.Points is null ? null : new FieldSet<decimal?>(item.Points),
        Tags = item.Tags.Count == 0 ? null : new FieldSet<IReadOnlyList<string>>(item.Tags),
        Priority = item.Priority is null ? null : new FieldSet<int>(item.Priority.Value),
    });

    public async Task<CaptureResult> CreatedAsync(ResolvedProject project, IReadOnlyList<PlannedItem> planned, IReadOnlyList<WriteOutcome> outcomes, CancellationToken cancellationToken)
    {
        var url = await BaseUrlAsync(cancellationToken);
        var created = outcomes.Select((outcome, i) =>
        {
            var key = outcome.Written!.Key;
            return new CreatedItem(key, planned[i].Title, TypeName(planned[i].Type), McpOpenUrls.WorkItem(url, project.Key, key));
        }).ToList();
        return new CaptureResult(project.Key, created.Count == 1 ? created[0].OpenUrl : McpOpenUrls.Backlog(url, project.Key, "stories"), created);
    }
}
