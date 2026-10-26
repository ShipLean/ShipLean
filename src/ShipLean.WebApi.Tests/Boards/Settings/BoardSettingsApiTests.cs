using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Criteria;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Persisted;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Boards.Settings;

// One seeded scenario for the board settings endpoints. Each test works on a board of its own.
[TestFixture]
public class BoardSettingsApiTests : ApiFixture
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private async Task<BoardSettingsDto> GetAsync(Guid boardId)
    {
        using var response = await Api.GetAsync($"api/boards/{boardId}/settings");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return JsonSerializer.Deserialize<BoardSettingsDto>(await response.Content.ReadAsStringAsync(), PersistedJson.Options)!;
    }

    private Task<HttpResponseMessage> PutAsync(Guid boardId, BoardSettingsJson basedOn, BoardSettingsJson edited)
        => Api.PutJsonAsync($"api/boards/{boardId}/settings", JsonSerializer.SerializeToNode(new BoardSettingsPut(basedOn, edited), PersistedJson.Options));

    private static async Task<BoardSettingsPutResult> ReadResultAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<BoardSettingsPutResult>(text, PersistedJson.Options)!;
    }

    [Test]
    public async Task SettingsApi_WhenOtherSectionsStale_ShouldKeepTheirs()
    {
        var board = await GetAsync(SeedIds.Boards.ShipFeatures);
        var theirs = board.Settings with { TagColours = [new TagColourJson("Urgent", Swatch.Danger)] };
        var mine = board.Settings with { Annotations = new CardAnnotationsJson(false, true, true, false) };
        using var first = await PutAsync(board.Id, board.Settings, theirs);
        await ReadResultAsync(first);

        using var response = await PutAsync(board.Id, board.Settings, mine);

        var result = await ReadResultAsync(response);
        result.MergedSections.ShouldBe(["tagColours"]);
        result.Board.Settings.TagColours.Select(t => t.Tag).ShouldBe(["Urgent"]);
        result.Board.Settings.Annotations.ShouldBe(mine.Annotations);
        (await GetAsync(board.Id)).Settings.TagColours.Select(t => t.Tag).ShouldBe(["Urgent"]);
    }

    [Test]
    public async Task SettingsApi_WhenColumnWithSeededCardsRemoved_ShouldBe400()
    {
        var board = await GetAsync(SeedIds.Boards.ShipStories);
        await using var connection = await OpenDatabaseAsync();
        var held = (await connection.QueryAsync<string>(
            "SELECT DISTINCT board_column_id FROM work_item WHERE project_id = @projectId AND type IN ('story', 'bug') AND board_column_id IS NOT NULL",
            new { projectId = SeedIds.Projects.Ship })).ToHashSet();
        var column = board.Settings.Columns.Skip(1).SkipLast(1).First(c => held.Contains(c.Id));
        var edited = board.Settings with { Columns = board.Settings.Columns.Where(c => c.Id != column.Id).ToList() };

        using var response = await PutAsync(board.Id, board.Settings, edited);

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Error.ShouldContain($"Column {column.Name} still has cards");
        (await GetAsync(board.Id)).Settings.Columns.Select(c => c.Id).ShouldContain(column.Id);
    }

    private static IEnumerable<TestCaseData> MalformedBodies()
    {
        yield return Malformed("basedOnNull", b => b["basedOn"] = null);
        yield return Malformed("settingsNull", b => b["settings"] = null);
        yield return Malformed("columnsNull", b => b["settings"]!["columns"] = null);
        yield return Malformed("swimlanesMissing", b => b["settings"]!.AsObject().Remove("swimlanes"));
        yield return Malformed("annotationsNull", b => b["settings"]!["annotations"] = null);
        yield return Malformed("stateMappingsNull", b => b["settings"]!["columns"]![0]!["stateMappings"] = null);
        yield return Malformed("columnNameNull", b => b["settings"]!["columns"]![0]!["name"] = null);
        yield return Malformed("cardFieldExtraFieldsNull", b => b["settings"]!["cardFields"]![0]!["extraFields"] = null);
        yield return Malformed("styleRuleCriteriaNull", b => b["settings"]!["styleRules"]!.AsArray().Add(new JsonObject { ["id"] = "r", ["name"] = "R", ["enabled"] = true, ["bold"] = false, ["italic"] = false, ["underline"] = false, ["criteria"] = null }));
        yield return Malformed("basedOnColumnsNull", b => b["basedOn"]!["columns"] = null);
        yield return Malformed("basedOnStateMappingsNull", b => b["basedOn"]!["columns"]![0]!["stateMappings"] = null);
        yield return Malformed("basedOnSwimlanesMissing", b => b["basedOn"]!.AsObject().Remove("swimlanes"));
    }

    private static TestCaseData Malformed(string name, Action<JsonObject> mutate) => new TestCaseData(mutate).SetName($"SettingsApi_WhenBodyMalformed_ShouldBe400({name})");

    [TestCaseSource(nameof(MalformedBodies))]
    public async Task SettingsApi_WhenBodyMalformed_ShouldBe400(Action<JsonObject> mutate)
    {
        var board = await GetAsync(SeedIds.Boards.ShipStories);
        var body = JsonSerializer.SerializeToNode(new BoardSettingsPut(board.Settings, board.Settings), PersistedJson.Options)!.AsObject();
        mutate(body);

        using var response = await Api.PutJsonAsync($"api/boards/{board.Id}/settings", body);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task SettingsApi_WhenLaneCriteriaHasUnknownField_ShouldBe400()
    {
        var board = await GetAsync(SeedIds.Boards.ShipEpics);
        var criteria = new CriteriaJson(CriteriaMatch.All, [new CriteriaClause("colour", CriteriaOp.Equal, JsonSerializer.SerializeToElement("red"))]);
        var lane = new SwimlaneJson("lane-bad", "Bad", null, SwimlaneKind.Criteria, criteria);

        using var response = await PutAsync(board.Id, board.Settings, board.Settings with { Swimlanes = [lane, .. board.Settings.Swimlanes] });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task SettingsApi_WhenSaved_ShouldPublishBoardSettingsChanged()
    {
        var board = await GetAsync(SeedIds.Boards.ShipTaskboard);
        var published = Channel.CreateUnbounded<BoardSettingsChanged>();
        using var subscription = Host.Services.GetRequiredService<IProjectEvents>().Subscribe(SeedIds.Projects.Ship, e =>
        {
            if (e is BoardSettingsChanged changed && changed.BoardId == board.Id)
            {
                published.Writer.TryWrite(changed);
            }
            return Task.CompletedTask;
        });

        (await ReadResultAsync(await PutAsync(board.Id, board.Settings, board.Settings with { TagColours = [new TagColourJson("Later", Swatch.Muted)] }))).MergedSections.ShouldBeEmpty();

        using var timeout = new CancellationTokenSource(Patience);
        var changed = await published.Reader.ReadAsync(timeout.Token);
        changed.Sections.ShouldBe(["tagColours"]);
        changed.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        changed.ClientId.ShouldBe(ApiClient.ClientId);
    }

    private Channel<BoardSettingsChanged> Collect(Guid boardId, out IDisposable subscription)
    {
        var published = Channel.CreateUnbounded<BoardSettingsChanged>();
        subscription = Host.Services.GetRequiredService<IProjectEvents>().Subscribe(SeedIds.Projects.Ship, e =>
        {
            if (e is BoardSettingsChanged changed && changed.BoardId == boardId)
            {
                published.Writer.TryWrite(changed);
            }
            return Task.CompletedTask;
        });
        return published;
    }

    [Test]
    public async Task SettingsApi_WhenPutChangesNothing_ShouldPublishNothing()
    {
        var board = await GetAsync(SeedIds.Boards.ShipEpics);
        var published = Collect(board.Id, out var subscription);
        using var _ = subscription;

        await ReadResultAsync(await PutAsync(board.Id, board.Settings, board.Settings));
        await ReadResultAsync(await PutAsync(board.Id, board.Settings, board.Settings with { TagColours = [new TagColourJson($"Marker-{Guid.NewGuid()}", Swatch.Muted)] }));

        using var timeout = new CancellationTokenSource(Patience);
        var first = await published.Reader.ReadAsync(timeout.Token);
        first.Sections.ShouldBe(["tagColours"]);
    }

    [Test]
    public async Task SettingsApi_WhenOtherSectionsStale_ShouldNameOnlyMySectionsInEvent()
    {
        var board = await GetAsync(SeedIds.Boards.ShipFeatures);
        await ReadResultAsync(await PutAsync(board.Id, board.Settings, board.Settings with { TagColours = [new TagColourJson($"Theirs-{Guid.NewGuid()}", Swatch.Danger)] }));
        var published = Collect(board.Id, out var subscription);
        using var _ = subscription;

        var annotations = board.Settings.Annotations with { Tasks = !board.Settings.Annotations.Tasks };
        await ReadResultAsync(await PutAsync(board.Id, board.Settings, board.Settings with { Annotations = annotations }));

        using var timeout = new CancellationTokenSource(Patience);
        var changed = await published.Reader.ReadAsync(timeout.Token);
        changed.Sections.ShouldBe(["annotations"]);
    }

    [Test]
    public async Task Put_WhenTwoWritersChangeDifferentSections_ShouldKeepBoth()
    {
        var store = Host.Services.GetRequiredService<IBoardSettingsStore>();
        var boardId = SeedIds.Boards.ShipStories;
        for (var round = 0; round < 10; round++)
        {
            var start = (await store.GetAsync(boardId)).Settings;
            var tag = $"Round{round}";
            var annotations = new CardAnnotationsJson(round % 2 == 0, false, false, round % 2 == 1);
            var gate = new TaskCompletionSource();
            var first = Task.Run(async () =>
            {
                await gate.Task;
                await store.PutAsync(boardId, new BoardSettingsPut(start, start with { TagColours = [new TagColourJson(tag, Swatch.Muted)] }));
            });
            var second = Task.Run(async () =>
            {
                await gate.Task;
                await store.PutAsync(boardId, new BoardSettingsPut(start, start with { Annotations = annotations }));
            });
            gate.SetResult();
            await Task.WhenAll(first, second);

            var saved = (await store.GetAsync(boardId)).Settings;
            saved.TagColours.Select(t => t.Tag).ShouldBe([tag], $"round {round}");
            saved.Annotations.ShouldBe(annotations, $"round {round}");
        }
    }

    [Test]
    public async Task BoardsApi_WhenListed_ShouldBeInLevelOrder()
    {
        var boards = await Api.GetJsonAsync<List<JsonNode>>($"api/projects/{SeedIds.Projects.Ship}/boards");

        boards.Select(b => b["level"]!.GetValue<string>()).ShouldBe(["stories", "features", "epics", "taskboard"]);
        boards.Select(b => b["id"]!.GetValue<Guid>()).ShouldBe([SeedIds.Boards.ShipStories, SeedIds.Boards.ShipFeatures, SeedIds.Boards.ShipEpics, SeedIds.Boards.ShipTaskboard]);
    }
}
