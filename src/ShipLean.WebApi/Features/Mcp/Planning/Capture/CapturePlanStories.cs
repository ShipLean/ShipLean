using System.Data.Common;
using Dapper;
using ShipLean.Application.Features.Mcp.Planning.Tags;

namespace ShipLean.WebApi.Features.Mcp.Planning.Capture;

// C1's definition of "this Feature already has a plan": a live Story child with a wave: tag, which only a captured plan writes. Stories made
// by hand don't count, so a plan can still be captured under them (PlanReads reads both). Used by the target check at the preview and the
// apply, and by the re-check inside the apply's transaction, so the three agree.
public static class CapturePlanStories
{
    public static async Task<int> CountAsync(DbConnection connection, DbTransaction? transaction, Guid featureId, CancellationToken cancellationToken)
    {
        var tags = await connection.QueryAsync<string[]>(new CommandDefinition(
            "SELECT w.tags FROM work_item w JOIN state s ON s.id = w.state_id WHERE w.parent_id = @featureId AND w.type = 'story' AND s.category <> 'removed'",
            new { featureId }, transaction, cancellationToken: cancellationToken));
        return tags.Count(storyTags => PlanTags.Parse(storyTags).Wave is not null);
    }
}
