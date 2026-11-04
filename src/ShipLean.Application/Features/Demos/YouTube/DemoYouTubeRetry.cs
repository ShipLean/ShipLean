using Dapper;
using Npgsql;
using ShipLean.Application.Features.Demos.Reads;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;

namespace ShipLean.Application.Features.Demos.YouTube;

// Puts a failed upload back to pending for the publisher to pick up. The video and its files are not touched, and neither is the sprint's updated_at.
public sealed class DemoYouTubeRetry(NpgsqlDataSource dataSource, ISprintDemoReader reader, IProjectEvents events) : IDemoYouTubeRetry
{
    public async Task<SprintDemoDto> RetryAsync(Guid sprintId, CancellationToken cancellationToken = default)
    {
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            var projectId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
                """
                UPDATE sprint SET demo_youtube_state = 'pending', demo_youtube_failure = NULL, demo_youtube_video_id = NULL,
                                  demo_youtube_privacy = NULL, demo_youtube_attempted_at = NULL
                WHERE id = @sprintId AND archived_at IS NULL AND demo_youtube_state = 'failed' AND demo_video_attachment_id IS NOT NULL
                RETURNING project_id
                """,
                new { sprintId }, cancellationToken: cancellationToken));
            if (projectId is null)
            {
                var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                    "SELECT EXISTS (SELECT 1 FROM sprint WHERE id = @sprintId AND archived_at IS NULL)", new { sprintId }, cancellationToken: cancellationToken));
                throw exists ? ApiException.Conflict("The YouTube upload hasn't failed.") : ApiException.NotFound("That sprint was not found.");
            }

            await events.PublishAsync(new SprintDemoChanged(projectId.Value, sprintId));
        }

        return await reader.GetAsync(sprintId, cancellationToken);
    }
}
