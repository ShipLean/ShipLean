using ShipLean.Contracts.Errors;

namespace ShipLean.Contracts.Retros;

// The shape checks of the retro routes, run after the access check and before the seam. Each throws ApiException.Validation.
public static class RetroChecks
{
    public static RetroRange Range(string? range) => range switch
    {
        null or "6" => RetroRange.Last6,
        "3" => RetroRange.Last3,
        "all" => RetroRange.All,
        _ => throw ApiException.Validation("range is 6, 3 or all.", "range"),
    };

    public static void PointCreate(RetroPointCreate request)
    {
        var targets = new[] { request.RetroId, request.WorkItemId, request.SprintId }.Count(id => id is not null);
        if (targets != 1)
        {
            throw ApiException.Validation("Name exactly one of retroId, workItemId and sprintId.", "retroId");
        }
        if (!Enum.IsDefined(request.Category))
        {
            throw ApiException.Validation("category is not a retro category.", "category");
        }
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            throw ApiException.Validation("The point needs text.", "text");
        }
        ThemeChoice(request.ThemeId, request.ThemeName);
    }

    public static void ThemePut(RetroPointThemePut request) => ThemeChoice(request.ThemeId, request.ThemeName);

    public static void Rename(RetroThemeRename request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw ApiException.Validation("The theme needs a name.", "name");
        }
    }

    public static void Merge(RetroThemeMerge request)
    {
        if (request.SourceIds is not { Count: > 0 } || request.SourceIds.Contains(request.TargetId))
        {
            throw ApiException.Validation("Name at least one source theme, and not the target itself.", "sourceIds");
        }
    }

    public static void ActionCreate(RetroActionCreate request)
    {
        if (request.Title is not null && string.IsNullOrWhiteSpace(request.Title))
        {
            throw ApiException.Validation("The title cannot be blank.", "title");
        }
        if (request.Points is < 0)
        {
            throw ApiException.Validation("points cannot be negative.", "points");
        }
    }

    private static void ThemeChoice(Guid? themeId, string? themeName)
    {
        if (themeId is not null && themeName is not null)
        {
            throw ApiException.Validation("Name a theme by id or by name, not both.", "themeId");
        }
    }
}
