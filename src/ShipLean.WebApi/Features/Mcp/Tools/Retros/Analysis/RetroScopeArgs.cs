using System.Globalization;
using ShipLean.Application.Features.Mcp.Planning.Lessons;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros.Decide;
using ShipLean.WebApi.Features.Mcp.Resolve;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros.Analysis;

// The scope arguments the retro tools share (decide_retro_actions, export_retro_points, record_retro_analysis): exactly one of a Feature's key, a sprint, or a date range.
internal static class RetroScopeArgs
{
    public static async Task<DecideScope> RequireOneAsync(
        ItemResolver items, SprintResolver sprints, FeatureKind features, Guid projectId, string? feature, string? sprint, string? from, string? to, CancellationToken cancellationToken)
    {
        var hasFeature = !string.IsNullOrWhiteSpace(feature);
        var hasSprint = !string.IsNullOrWhiteSpace(sprint);
        var hasFrom = !string.IsNullOrWhiteSpace(from);
        var hasTo = !string.IsNullOrWhiteSpace(to);
        if (hasFrom != hasTo)
        {
            throw ApiException.Validation("Give `from` and `to` together, or neither.", "scope");
        }

        if (new[] { hasFeature, hasSprint, hasFrom }.Count(given => given) != 1)
        {
            throw ApiException.Validation("Give exactly one scope: `feature`, `sprint`, or `from` with `to`.", "scope");
        }

        if (hasFeature)
        {
            var resolvedFeature = await items.ResolveAsync(feature!, ProjectRole.Member, projectId, "feature", cancellationToken);
            if (!await features.IsFeatureAsync(resolvedFeature.Id, cancellationToken))
            {
                throw ApiException.Validation($"{resolvedFeature.Key} is not a Feature. Give the key of a Feature.", "feature");
            }

            return new DecideScope(resolvedFeature.Id, null, null, null);
        }

        if (hasSprint)
        {
            return new DecideScope(null, (await sprints.ResolveAsync(projectId, sprint!, cancellationToken: cancellationToken)).Id, null, null);
        }

        var start = Day(from!, "from");
        var end = Day(to!, "to");
        return start > end ? throw ApiException.Validation("`from` is after `to`.", "from") : new DecideScope(null, null, start, end);
    }

    private static DateOnly Day(string text, string field)
        => DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day
            : throw ApiException.Validation($"\"{text}\" is not a date like 2026-11-03.", field);
}
