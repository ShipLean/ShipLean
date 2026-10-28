using Dapper;
using Npgsql;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.AgentDispatch;
using ShipLean.WebApi.Tests.Instance.AgentConnections;

namespace ShipLean.WebApi.Tests.LinearApi;

// Two connections, each with a session on an item of its own: A's is working with a link, a plan, three activities and two
// comments on its item; B's is submitted. Sessions are written straight to the table: the trigger and the delivery service
// are other tests' business.
internal sealed class LinearApiWorld
{
    public const string RepoA = "https://github.com/ShipLean/ShipLean.git";
    public const string RepoB = "git@github.com:Other/Repo.git";

    private AgentDispatchArrange _arranged = null!;

    public required Guid ConnectionA { get; init; }
    public required string NameA { get; init; }
    public required string TokenA { get; init; }
    public required Guid ConnectionB { get; init; }
    public required string TokenB { get; init; }
    public required Guid ItemA { get; init; }
    public required string KeyA { get; init; }
    public required Guid ItemB { get; init; }
    public required string KeyB { get; init; }
    public required Guid SessionA { get; init; }
    public required Guid SessionB { get; init; }
    public required Guid FirstActivityA { get; init; }
    public required Guid FirstCommentA { get; init; }

    public static async Task<LinearApiWorld> CreateAsync(ApiClient api, NpgsqlConnection database)
    {
        var arranged = await AgentDispatchArrange.CreateAsync(database, api, connection: false, repository: false);
        var a = await AgentConnectionsWire.CreateAsync(api, $"Relay A {Guid.NewGuid():N}");
        var b = await AgentConnectionsWire.CreateAsync(api, $"Relay B {Guid.NewGuid():N}");
        var itemA = await arranged.CreateItemAsync(api);
        var itemB = await arranged.CreateItemAsync(api);
        await database.ExecuteAsync(
            "UPDATE work_item SET description = 'Body', acceptance_criteria = '[{\"text\":\"Works\",\"done\":true},{\"text\":\"Ships\",\"done\":false}]'::jsonb WHERE id = @id",
            new { id = itemA.Id });
        var sessionA = Guid.NewGuid();
        var sessionB = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);
        await database.ExecuteAsync(
            """
            INSERT INTO agent_session (id, work_item_id, connection_id, state, repo_url, base_branch, external_urls, plan, last_activity_at, created_by, created_at)
            VALUES (@sessionA, @itemA, @a, 'working', @repoA, 'develop', '[{"url":"https://github.com/ShipLean/ShipLean/pull/9","label":"Pull request"}]'::jsonb,
                    '[{"content":"Write it","status":"inProgress"}]'::jsonb, @now, @joel, @now),
                   (@sessionB, @itemB, @b, 'submitted', @repoB, 'main', '[]'::jsonb, '[]'::jsonb, @now, @joel, @now)
            """,
            new { sessionA, sessionB, itemA = itemA.Id, itemB = itemB.Id, a = a.Connection.Id, b = b.Connection.Id, repoA = RepoA, repoB = RepoB, now, joel = SeedIds.Users.Joel });

        var firstActivity = Guid.NewGuid();
        await database.ExecuteAsync(
            """
            INSERT INTO agent_activity (id, session_id, type, body, action, parameter, result, ephemeral, signal, created_at) VALUES
              (@first, @sessionA, 'thought', 'Reading the item', NULL, NULL, NULL, true, NULL, @t1),
              (gen_random_uuid(), @sessionA, 'action', NULL, 'Run', 'dotnet test', 'passed', false, NULL, @t2),
              (gen_random_uuid(), @sessionA, 'elicitation', 'Which database?', NULL, NULL, NULL, false, 'select', @t3)
            """,
            new { first = firstActivity, sessionA, t1 = now.AddSeconds(1), t2 = now.AddSeconds(2), t3 = now.AddSeconds(3) });

        var firstComment = Guid.NewGuid();
        await database.ExecuteAsync(
            """
            INSERT INTO comment (id, work_item_id, author_id, body, created_at, updated_at) VALUES
              (@first, @itemA, @joel, 'First thoughts', @t1, @t1),
              (gen_random_uuid(), @itemA, @joel, 'Second thoughts', @t2, @t2)
            """,
            new { first = firstComment, itemA = itemA.Id, joel = SeedIds.Users.Joel, t1 = now.AddSeconds(-20), t2 = now.AddSeconds(-10) });

        return new LinearApiWorld
        {
            _arranged = arranged,
            ConnectionA = a.Connection.Id,
            NameA = a.Connection.Name,
            TokenA = a.CallbackToken!,
            ConnectionB = b.Connection.Id,
            TokenB = b.CallbackToken!,
            ItemA = itemA.Id,
            KeyA = itemA.Key,
            ItemB = itemB.Id,
            KeyB = itemB.Key,
            SessionA = sessionA,
            SessionB = sessionB,
            FirstActivityA = firstActivity,
            FirstCommentA = firstComment,
        };
    }

    // A session of connection A on an item of its own (one live session per item), for a test that moves it. `lastActivity`
    // defaults to now.
    public async Task<(Guid Session, Guid Item, string Key)> AddSessionAsync(
        ApiClient api, NpgsqlConnection database, string state = "working", bool stopRequested = false, DateTimeOffset? lastActivity = null)
    {
        var item = await _arranged.CreateItemAsync(api);
        var session = Guid.NewGuid();
        var at = lastActivity ?? TimeProvider.System.GetUtcNow();
        await database.ExecuteAsync(
            """
            INSERT INTO agent_session (id, work_item_id, connection_id, state, repo_url, base_branch, last_activity_at, stop_requested_at, created_by, created_at)
            VALUES (@session, @item, @connection, @state, @repo, 'develop', @at, @stop, @joel, @at)
            """,
            new { session, item = item.Id, connection = ConnectionA, state, repo = RepoA, at, stop = stopRequested ? at : (DateTimeOffset?)null, joel = SeedIds.Users.Joel });
        return (session, item.Id, item.Key);
    }
}
