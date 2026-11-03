using ShipLean.Contracts.Errors;

namespace ShipLean.Contracts.Retros;

// The shape checks of the retro routes, run after the access check and before the seam. Each throws ApiException.Validation.
// The controllers only check the route's project, point and theme. The ids in a body (RetroId, WorkItemId, SprintId, SourceIds, TargetId)
// are not access-checked here: the seam must answer not_found for any of them outside the route's projectId.
public static class RetroChecks
{
    public static RetroRange Range(string? range) => range switch
    {
        null or "6" => RetroRange.Last6,
        "3" => RetroRange.Last3,
        "all" => RetroRange.All,
        // TODO(T1): the reader treats w6 and w12 as Last6 until week mode replaces it.
        "w6" => RetroRange.Weeks6,
        "w12" => RetroRange.Weeks12,
        _ => throw ApiException.Validation("range is 6, 3, all, w6 or w12.", "range"),
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
        if (request.RootCause is not null && request.Category != RetroCategory.WentBadly)
        {
            throw ApiException.Validation("rootCause is only for wentBadly points.", "rootCause");
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

    public static void ActionNew(RetroActionNew request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw ApiException.Validation("The title cannot be blank.", "title");
        }
        FixTypeChoice(request.FixType);
        PointIds(request.PointIds);
        ThemeChoice(request.ThemeId, request.ThemeName);
    }

    public static void ActionAddPoints(RetroActionAddPoints request) => PointIds(request.PointIds);

    public static void ActionPut(RetroActionPut request)
    {
        if (request.FixType is { } fixType)
        {
            FixTypeChoice(fixType);
        }
        ThemeChoice(request.ThemeId, request.ThemeName);
    }

    public static void NotNeededPut(RetroNotNeededPut request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw ApiException.Validation("Say why the point needs no action.", "reason");
        }
    }

    private static void PointIds(IReadOnlyList<Guid>? pointIds)
    {
        if (pointIds is not { Count: > 0 })
        {
            throw ApiException.Validation("Name at least one point.", "pointIds");
        }
        if (pointIds.Distinct().Count() != pointIds.Count)
        {
            throw ApiException.Validation("A point can be named once.", "pointIds");
        }
    }

    private static void FixTypeChoice(RetroFixType fixType)
    {
        if (!Enum.IsDefined(fixType))
        {
            throw ApiException.Validation("fixType is not a type of fix.", "fixType");
        }
    }

    private static void ThemeChoice(Guid? themeId, string? themeName)
    {
        if (themeId is not null && themeName is not null)
        {
            throw ApiException.Validation("Name a theme by id or by name, not both.", "themeId");
        }
        if (themeName is not null && string.IsNullOrWhiteSpace(themeName))
        {
            throw ApiException.Validation("The theme name cannot be blank.", "themeName");
        }
    }
}
