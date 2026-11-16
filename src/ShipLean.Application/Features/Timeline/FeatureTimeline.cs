using Dapper;
using Npgsql;
using ShipLean.Application.Features.Timeline.Facts;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Views.Timeline;

namespace ShipLean.Application.Features.Timeline;

// A Feature's timeline (C2, C3): member access first, then the key must name a Feature of the project, then the facts as of now
// go through the pure builder in the clock's zone.
public sealed class FeatureTimeline(IProjectAccess access, NpgsqlDataSource dataSource, ITimelineFactsReader reader, TimeProvider clock)
    : IFeatureTimeline
{
    public async Task<FeatureTimelineViewDto> GetAsync(Guid projectId, string featureKey, CancellationToken ct)
    {
        await access.RequireAsync(projectId, ProjectRole.Member, ct);
        Guid? featureId;
        await using (var connection = await dataSource.OpenConnectionAsync(ct))
        {
            featureId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
                "SELECT id FROM work_item WHERE project_id = @projectId AND key = upper(@featureKey) AND type = 'feature'",
                new { projectId, featureKey }, cancellationToken: ct));
        }

        if (featureId is not { } id)
        {
            throw ApiException.NotFound("Feature not found.");
        }

        var asOf = clock.GetUtcNow();
        var facts = await reader.ReadAsync(projectId, id, asOf, ct);
        return TimelineBuilder.Build(facts, asOf, clock.LocalTimeZone);
    }
}
