using System.Data.Common;
using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;

namespace ShipLean.WebApi.Features.Mcp.Planning.Capture;

// The claim on a previewed plan (mcp_capture_plan_apply): one row per applied plan id, written in the tree's own transaction. A
// second insert of one plan id fails on the primary key, so a preview applies at most once.
[RegisterPerRequest]
public sealed class CapturePlanClaims(NpgsqlDataSource dataSource, TimeProvider time)
{
    // The Feature the plan was applied under, or null when this plan id has not been applied in this project.
    public async Task<Guid?> FindAsync(Guid planId, Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            "SELECT feature_id FROM mcp_capture_plan_apply WHERE plan_id = @planId AND project_id = @projectId",
            new { planId, projectId }, cancellationToken: cancellationToken));
    }

    // Whether the exception is the primary key refusing a second claim of one plan id.
    public static bool IsAlreadyClaimed(Exception exception)
        => exception is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "mcp_capture_plan_apply_pkey" };

    // The hook for the tree write: inside its transaction, after the items are written, it claims the plan id and, on an
    // existing Feature, re-checks that the Feature holds no plan but this call's. It runs after the store took the project's
    // work-item lock, which every work-item write in the project takes first and holds to commit, so by now every competing
    // write has committed or waits behind this one; the count sees them all and no further lock is needed. The claim goes
    // first: a second apply of one plan id then always hits the primary key and takes the claimed path, and only a different
    // plan reaches the count. If the hook throws, the tree is rolled back.
    public Func<DbTransaction, CancellationToken, Task> InTransaction(Guid planId, Guid projectId, string? existingKey, Guid? existingId, int stories)
        => async (transaction, cancellationToken) =>
        {
            var connection = transaction.Connection!;
            // A new Feature is the project's highest-numbered one. Every create reserves its number with an UPDATE of the
            // project row, which the project lock holds to commit, so no other transaction can hold a higher number while this
            // runs; and the tree creates exactly one Feature, the root. (Not xmin: a backlog renumber rewrites every row.)
            var featureId = existingId ?? await connection.QuerySingleAsync<Guid>(new CommandDefinition(
                "SELECT id FROM work_item WHERE project_id = @projectId AND type = 'feature' ORDER BY number DESC LIMIT 1",
                new { projectId }, transaction, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO mcp_capture_plan_apply (plan_id, project_id, feature_id, applied_at) VALUES (@planId, @projectId, @featureId, @appliedAt)",
                new { planId, projectId, featureId, appliedAt = time.GetUtcNow().UtcDateTime }, transaction, cancellationToken: cancellationToken));

            if (existingId is { } existing && await CapturePlanStories.CountAsync(connection, transaction, existing, cancellationToken) > stories)
            {
                throw ApiException.Validation($"{existingKey} already has a plan; capture_plan adds a plan to a Feature that has none.", "feature.key");
            }
        };
}
