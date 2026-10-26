using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Search;
using ShipLean.Contracts.Views.Search;

namespace ShipLean.Application.Features.Search.Query;

// What GET /api/search does: the project check first, then the ranker, then the view model with its links.
[RegisterPerRequest]
public sealed class SearchViews(IProjectAccess access, ISearchRanker ranker, NpgsqlDataSource dataSource)
{
    public async Task<SearchViewDto> SearchAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        var projectIds = request.Project is { } key ? new[] { await ProjectIdAsync(key, cancellationToken) } : null;
        var text = request.Q?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            throw ApiException.Validation("q is required.", "q");
        }

        if (request.Limit is < 1 or > SearchRequest.MaxLimit)
        {
            throw ApiException.Validation($"limit must be from 1 to {SearchRequest.MaxLimit}.", "limit");
        }

        var results = await ranker.SearchAsync(
            new SearchQuery(text, projectIds, ParseTypes(request.Types) is { Count: > 0 } types ? types : null, null, request.Closed, request.Prefix, request.Limit),
            cancellationToken);
        // The view builds every link; an empty base URL leaves the relative SPA path (the patterns in McpOpenUrls).
        return new SearchViewDto(
            text,
            results.Semantic,
            [.. results.Projects.Select(p => new SearchProjectDto(p.Key, p.Name, McpOpenUrls.Backlog("", p.Key, "stories")))],
            [.. results.Sprints.Select(s => new SearchSprintDto(s.Name, s.Goal, s.StartDate, s.EndDate, s.ProjectKey, UrlFor(s)))],
            [.. results.Items.Select(ToItem)],
            results.More);
    }

    // The project key is the caller's word for a project; an unknown key and a project the caller is not in look the same.
    private async Task<Guid> ProjectIdAsync(string key, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var id = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            "SELECT id FROM project WHERE key = @key", new { key = key.Trim().ToUpperInvariant() }, cancellationToken: cancellationToken));
        if (id is not { } projectId)
        {
            throw ApiException.NotFound($"No project {key}.");
        }

        await access.RequireAsync(projectId, ProjectRole.Member, cancellationToken);
        return projectId;
    }

    // Blank entries are ignored; no entries left gives an empty list, which the caller reads as no type filter.
    private static List<WorkItemTypeId> ParseTypes(string? types)
    {
        var entries = (types ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (entries.Length == 0)
        {
            return [];
        }

        var names = Enum.GetNames<WorkItemTypeId>();
        return [.. entries.Select(t =>
            names.FirstOrDefault(n => string.Equals(n, t, StringComparison.OrdinalIgnoreCase)) is { } name
                ? Enum.Parse<WorkItemTypeId>(name)
                : throw ApiException.Validation($"Unknown type \"{t}\". Use {string.Join(", ", names.Select(n => char.ToLowerInvariant(n[0]) + n[1..]))}.", "types"))];
    }

    private static SearchItemDto ToItem(SearchResultItem item) => new(
        item.Key,
        item.Title,
        item.Type,
        item.State,
        item.ProjectKey,
        item.ParentPath,
        MatchedIn(item.MatchedIn),
        item.Snippet,
        McpOpenUrls.WorkItem("", item.ProjectKey, item.Key),
        item.Attachment);

    private static SearchMatchedIn MatchedIn(SearchField field) => field switch
    {
        SearchField.Title => SearchMatchedIn.Title,
        SearchField.Tags => SearchMatchedIn.Tags,
        SearchField.Body => SearchMatchedIn.Description,
        SearchField.Comment => SearchMatchedIn.Comment,
        SearchField.Attachment => SearchMatchedIn.Attachment,
        _ => throw new InvalidOperationException($"An item cannot match in {field}."),
    };

    private static string UrlFor(SearchResultSprint sprint) => McpOpenUrls.Taskboard("", sprint.ProjectKey, sprint.Id);
}
