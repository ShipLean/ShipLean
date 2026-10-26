using Agoda.IoC.Core;
using Npgsql;
using ShipLean.Application.Features.Mcp.Status;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Tools.Planning;

namespace ShipLean.WebApi.Features.Mcp.Planning.Capture;

// The existing Feature a plan names, or null when it creates a new one. A plan lands only under a live Feature that has no
// plan yet: the tree writer would take a Removed root or one that already holds plan stories, so this refuses them. Run at
// the preview and again at the apply, because either can change in between.
[RegisterPerRequest]
public sealed class CapturePlanTargets(ItemResolver items, FeatureProgressReads heads, NpgsqlDataSource dataSource)
{
    private const string Field = "feature.key";

    public async Task<CapturePlanTarget?> ResolveAsync(CapturePlanFeature feature, Guid projectId, CancellationToken cancellationToken)
    {
        if (feature.Key is not { Length: > 0 } key)
        {
            return null;
        }

        var resolved = await items.ResolveAsync(key, ProjectRole.Member, projectId, Field, cancellationToken);
        var head = await heads.HeadAsync(resolved.Id, cancellationToken);
        if (head.Type != "feature")
        {
            throw ApiException.Validation($"{resolved.Key} is not a Feature, so a plan can't land under it.", Field);
        }
        if (Enum.Parse<StateCategory>(head.StateCategory, ignoreCase: true) == StateCategory.Removed)
        {
            throw ApiException.Validation($"{resolved.Key} was removed, so a plan can't land under it.", Field);
        }

        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            if (await CapturePlanStories.CountAsync(connection, null, resolved.Id, cancellationToken) > 0)
            {
                throw ApiException.Validation($"{resolved.Key} already has a plan; capture_plan adds a plan to a Feature that has none.", Field);
            }
        }
        return new CapturePlanTarget(resolved.Id, resolved.Key);
    }
}
