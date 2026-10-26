using System.Text.Json;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.Features.Backlog.Shared;

// The filter query parameter: a CriteriaJson document as JSON text.
public static class BacklogFilter
{
    public static CriteriaJson? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        try
        {
            var criteria = JsonSerializer.Deserialize<CriteriaJson>(text, PersistedJson.Options);
            return criteria is { Clauses: not null } && !criteria.Clauses.Contains(null!)
                ? criteria
                : throw ApiException.Validation("The filter is not valid criteria.");
        }
        catch (JsonException)
        {
            throw ApiException.Validation("The filter is not valid criteria.");
        }
    }
}
