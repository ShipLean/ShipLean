using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Retros;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Retros;

namespace ShipLean.AcceptanceTests.Lanes.Retros.Journeys;

// Joel decides what to do with the points of a sprint's retros: no action needed (with a reason, undoable), a new action item over
// several points, or an addition to an action that already exists. Each test has a sprint of its own, so they run in any order.
[TestFixture]
public class DecideJourneyTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";
    private static readonly Guid ProjectId = SeedIds.Projects.Ship;

    [Test]
    public async Task Decide_WhenPersonWorksThroughASprint_ShouldEndWithNothingToDecide()
    {
        var sprint = SeedIds.Sprints.Sprint19;
        var (reviews, estimates, standups, pairing) = ("Reviews waited on one person", "Estimates ignore review latency", "Standups ran long", "Pairing cut the handover time");
        await using (var connection = await OpenAsync())
        {
            var first = await AgentRetroAsync(connection, sprint, null);
            var second = await AgentRetroAsync(connection, sprint, null);
            var seed = new RetroSeed(connection, ProjectId);
            await seed.PointAsync(first, new RetroSeedPoint(RetroCategory.WentBadly, reviews));
            await seed.PointAsync(first, new RetroSeedPoint(RetroCategory.WentWell, pairing));
            await seed.PointAsync(second, new RetroSeedPoint(RetroCategory.Improve, estimates));
            await seed.PointAsync(second, new RetroSeedPoint(RetroCategory.NotInPlan, standups));
        }

        var retro = new SprintRetroPage(Page);
        await retro.OpenAsync(Project, sprint);
        await Expect(retro.KpiToDecide).ToHaveTextAsync("3");

        // Went well is celebrated, not decided: no controls.
        var well = retro.Board.NoteOf(pairing);
        await Expect(well.Root).ToBeVisibleAsync();
        await Expect(well.AddAction).ToHaveCountAsync(0);
        await Expect(well.NotNeeded).ToHaveCountAsync(0);

        // Not needed needs a reason; Undo gives the point back.
        var standup = retro.Board.NoteOf(standups);
        await standup.NotNeeded.ClickAsync();
        await Expect(standup.WhySave).ToBeDisabledAsync();
        await standup.WhyText.FillAsync("One-off, the team agreed in the meeting");
        var standupId = await standup.Root.GetAttributeAsync("data-point-id");
        var marked = await Page.RunAndWaitForApiAsync($"/api/retros/points/{standupId}/not-needed", "POST", () => standup.WhySave.ClickAsync());
        marked.Ok.ShouldBeTrue(await marked.TextAsync());
        await Expect(standup.None).ToContainTextAsync("One-off, the team agreed in the meeting");
        await Expect(retro.KpiToDecide).ToHaveTextAsync("2");
        var undone = await Page.RunAndWaitForApiAsync($"/api/retros/points/{standupId}/undo-not-needed", "POST", () => standup.Undo.ClickAsync());
        undone.Ok.ShouldBeTrue(await undone.TextAsync());
        await Expect(standup.None).ToHaveCountAsync(0);
        await Expect(retro.KpiToDecide).ToHaveTextAsync("3");
        await standup.NotNeeded.ClickAsync();
        await standup.WhyText.FillAsync("Not worth a fix");
        await Page.RunAndWaitForApiAsync($"/api/retros/points/{standupId}/not-needed", "POST", () => standup.WhySave.ClickAsync());
        await Expect(retro.KpiToDecide).ToHaveTextAsync("2");

        // One action item over two points from two retros. A point already decided is not offered.
        await retro.Board.NoteOf(reviews).AddAction.ClickAsync();
        await Expect(retro.Dialog.Dialog).ToBeVisibleAsync();
        await Expect(retro.Dialog.Pick.Filter(new() { HasText = standups })).ToHaveCountAsync(0);
        await retro.Dialog.Pick.Filter(new() { HasText = estimates }).ClickAsync();
        var created = await Page.RunAndWaitForApiAsync($"/api/projects/{ProjectId}/retros/actions", "POST", () => retro.Dialog.Save.ClickAsync());
        created.Ok.ShouldBeTrue(await created.TextAsync());
        using var reply = JsonDocument.Parse(await created.TextAsync());
        var key = reply.RootElement.GetProperty("action").GetProperty("key").GetString()!;
        reply.RootElement.GetProperty("action").GetProperty("pointCount").GetInt32().ShouldBe(2);

        await Expect(retro.KpiToDecide).ToHaveTextAsync("0");
        await Expect(retro.Board.NoteOf(reviews).Pin).ToContainTextAsync(key);
        await Expect(retro.Board.NoteOf(estimates).Pin).ToContainTextAsync(key);
        await Expect(retro.Board.NoteOf(reviews).Pin).ToContainTextAsync("+1 more point");
        await Expect(retro.Board.NoteOf(reviews).AddAction).ToHaveCountAsync(0);

        // Nothing is left under To decide.
        await Page.GotoAsync(Links.SprintRetro(Project, sprint, ("decide", "open")));
        await retro.Panel.WaitForAsync();
        await Expect(retro.Points).ToHaveCountAsync(0);

        await using var check = await OpenAsync();
        (await check.ExecuteScalarAsync<int>("SELECT count(*)::int FROM retro_action_point rap JOIN retro_action ra ON ra.work_item_id = rap.work_item_id JOIN work_item w ON w.id = ra.work_item_id WHERE w.key = @key AND rap.archived_at IS NULL", new { key })).ShouldBe(2);
    }

    [Test]
    public async Task RemovingAnActionItem_ShouldReturnItsPointsToUndecided()
    {
        var sprint = SeedIds.Sprints.Sprint20;
        var (one, two) = ("Hand-offs lost the context", "Tickets had no acceptance criteria");
        await using (var connection = await OpenAsync())
        {
            var seed = new RetroSeed(connection, ProjectId);
            var retroId = await AgentRetroAsync(connection, sprint, null);
            await seed.PointAsync(retroId, new RetroSeedPoint(RetroCategory.Improve, one));
            await seed.PointAsync(retroId, new RetroSeedPoint(RetroCategory.Improve, two));
        }

        var retro = new SprintRetroPage(Page);
        await retro.OpenAsync(Project, sprint);
        await retro.Board.NoteOf(one).AddAction.ClickAsync();
        await retro.Dialog.Pick.Filter(new() { HasText = two }).ClickAsync();
        var created = await Page.RunAndWaitForApiAsync($"/api/projects/{ProjectId}/retros/actions", "POST", () => retro.Dialog.Save.ClickAsync());
        created.Ok.ShouldBeTrue(await created.TextAsync());
        using var reply = JsonDocument.Parse(await created.TextAsync());
        var itemId = reply.RootElement.GetProperty("action").GetProperty("workItemId").GetGuid();
        await Expect(retro.KpiToDecide).ToHaveTextAsync("0");

        // Removing the action item (the work item's own Remove) gives both points back.
        var item = await Page.Context.APIRequest.GetAsync($"/api/work-items/{itemId}");
        item.Ok.ShouldBeTrue(await item.TextAsync());
        using var current = JsonDocument.Parse(await item.TextAsync());
        var removed = await Page.Context.APIRequest.PostAsync($"/api/work-items/{itemId}/remove", new() { DataObject = new { expectedRevision = current.RootElement.GetProperty("revision").GetInt32() } });
        removed.Ok.ShouldBeTrue(await removed.TextAsync());

        await retro.OpenAsync(Project, sprint);
        await Expect(retro.KpiToDecide).ToHaveTextAsync("2");
        await Expect(retro.Board.NoteOf(one).Pin).ToHaveCountAsync(0);
        await Expect(retro.Board.NoteOf(one).AddAction).ToBeVisibleAsync();
        await Expect(retro.Board.NoteOf(two).AddAction).ToBeVisibleAsync();

        // The points can be taken by a new action item: the old join rows do not block it.
        await retro.Board.NoteOf(two).AddAction.ClickAsync();
        var again = await Page.RunAndWaitForApiAsync($"/api/projects/{ProjectId}/retros/actions", "POST", () => retro.Dialog.Save.ClickAsync());
        again.Ok.ShouldBeTrue(await again.TextAsync());
        await Expect(retro.KpiToDecide).ToHaveTextAsync("1");
    }

    [Test]
    public async Task LegacyAction_WhenMigrated_ShouldStillShowItsPoint()
    {
        var sprint = SeedIds.Sprints.Sprint21;
        var text = "Release notes were written the night before";
        string key;
        await using (var connection = await OpenAsync())
        {
            var retroId = await AgentRetroAsync(connection, sprint, null);
            var seed = new RetroSeed(connection, ProjectId);
            var pointId = await seed.PointAsync(retroId, new RetroSeedPoint(RetroCategory.Improve, text));
            var actionId = await new WorkItemBuilder(ProjectId).WithType("action").WithTitle("Write release notes as we go").InsertAsync(connection);
            // As the migration left it: the old one-point column set, the join row copied from it, no fix type.
            await seed.ActionAsync(new RetroSeedAction(actionId, PointId: pointId, RaisedSprintId: sprint));
            await connection.ExecuteAsync("UPDATE retro_action SET point_id = @pointId WHERE work_item_id = @actionId", new { pointId, actionId });
            key = (await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @actionId", new { actionId }))!;
        }

        var retro = new SprintRetroPage(Page);
        await retro.OpenAsync(Project, sprint);
        await Expect(retro.KpiToDecide).ToHaveTextAsync("0");
        await Expect(retro.Board.NoteOf(text).Pin).ToContainTextAsync(key);
        await Expect(retro.Board.NoteOf(text).AddAction).ToHaveCountAsync(0);

        await Page.GotoAsync(Links.WorkItem(Project, key));
        var why = new ActionWhyPart(Page);
        await Expect(why.Section).ToBeVisibleAsync();
        await Expect(why.Points).ToHaveCountAsync(1);
        await Expect(why.PointOf(text)).ToBeVisibleAsync();
    }

    [Test]
    public async Task AddToExisting_WhenLaterRetroHasTheSameFix_ShouldNotRaiseADuplicate()
    {
        var sprint = SeedIds.Sprints.Sprint22;
        var (earlier, later) = ("Flaky suite hid real failures", "The flaky suite hid another failure");
        Guid actionId;
        string key;
        await using (var connection = await OpenAsync())
        {
            var seed = new RetroSeed(connection, ProjectId);
            var first = await AgentRetroAsync(connection, sprint, null);
            var pointId = await seed.PointAsync(first, new RetroSeedPoint(RetroCategory.WentBadly, earlier));
            actionId = await new WorkItemBuilder(ProjectId).WithType("action").WithTitle("Quarantine the flaky suite").InsertAsync(connection);
            await seed.ActionAsync(new RetroSeedAction(actionId, PointId: pointId, RaisedSprintId: sprint, FixType: RetroFixType.Test));
            var second = await AgentRetroAsync(connection, sprint, null);
            await seed.PointAsync(second, new RetroSeedPoint(RetroCategory.WentBadly, later));
            key = (await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @actionId", new { actionId }))!;
        }

        var retro = new SprintRetroPage(Page);
        await retro.OpenAsync(Project, sprint);
        await Expect(retro.KpiToDecide).ToHaveTextAsync("1");
        await retro.Board.NoteOf(later).AddAction.ClickAsync();
        await retro.Dialog.ModeExisting.ClickAsync();
        await retro.Dialog.Existing.SelectOptionAsync(actionId.ToString());
        var added = await Page.RunAndWaitForApiAsync($"/api/retros/actions/{actionId}/points", "POST", () => retro.Dialog.Save.ClickAsync());
        added.Ok.ShouldBeTrue(await added.TextAsync());

        await Expect(retro.KpiToDecide).ToHaveTextAsync("0");
        await Expect(retro.Board.NoteOf(later).Pin).ToContainTextAsync(key);
        await Expect(retro.Board.NoteOf(earlier).Pin).ToContainTextAsync("+1 more point");
        await using var check = await OpenAsync();
        (await check.ExecuteScalarAsync<int>("SELECT count(*)::int FROM retro_action WHERE project_id = @ProjectId AND raised_sprint_id = @sprint", new { ProjectId, sprint })).ShouldBe(1);
        (await check.ExecuteScalarAsync<int>("SELECT count(*)::int FROM retro_action_point WHERE work_item_id = @actionId AND archived_at IS NULL", new { actionId })).ShouldBe(2);
    }

    [Test]
    public async Task Add_WhenPersonAddsOnAgentRetroBoard_ShouldWriteOwnRetroAndShowIt()
    {
        var sprint = SeedIds.Sprints.Sprint23;
        var (agentText, ownText) = ("The importer had no owner", "I would pair on importer stories");
        string key;
        Guid agentRetro;
        await using (var connection = await OpenAsync())
        {
            var story = await new WorkItemBuilder(ProjectId).WithType("story").WithTitle("Importer retro story").WithSprint(sprint).InsertAsync(connection);
            key = (await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @story", new { story }))!;
            agentRetro = await AgentRetroAsync(connection, sprint, story);
            await new RetroSeed(connection, ProjectId).PointAsync(agentRetro, new RetroSeedPoint(RetroCategory.WentBadly, agentText));
        }

        var item = new RetroItemPage(Page);
        await item.OpenAsync(Project, key);
        await item.Board.AddIn("Can be improved").ClickAsync();
        await item.Board.AddText.FillAsync(ownText);
        var created = await Page.RunAndWaitForApiAsync($"/api/projects/{ProjectId}/retros/points", "POST", () => item.Board.AddSave.ClickAsync());
        created.Ok.ShouldBeTrue(await created.TextAsync());
        await Expect(item.Board.NoteOf(ownText).Root).ToBeVisibleAsync();
        await Expect(item.Board.NoteOf(agentText).Root).ToBeVisibleAsync();

        await using var check = await OpenAsync();
        var own = await check.QuerySingleAsync<(Guid Retro, Guid Author, string? Agent)>(
            "SELECT r.id AS Retro, r.author_user_id AS Author, r.agent_name AS Agent FROM retro_point p JOIN retrospective r ON r.id = p.retrospective_id WHERE p.text = @ownText", new { ownText });
        own.Retro.ShouldNotBe(agentRetro);
        own.Author.ShouldBe(SeedIds.Users.Joel);
        own.Agent.ShouldBeNull();
        (await check.ExecuteScalarAsync<int>("SELECT count(*)::int FROM retro_point WHERE retrospective_id = @agentRetro", new { agentRetro })).ShouldBe(1);
    }

    [Test]
    public async Task AgentText_ShouldStayReadOnly()
    {
        var sprint = SeedIds.Sprints.Sprint24;
        var text = "Deploys needed a manual database step";
        Guid agentRetro, pointId;
        string agentName;
        await using (var connection = await OpenAsync())
        {
            agentRetro = await AgentRetroAsync(connection, sprint, null);
            agentName = (await connection.ExecuteScalarAsync<string>("SELECT agent_name FROM retrospective WHERE id = @agentRetro", new { agentRetro }))!;
            pointId = await new RetroSeed(connection, ProjectId).PointAsync(agentRetro, new RetroSeedPoint(RetroCategory.WentBadly, text));
        }

        var retro = new SprintRetroPage(Page);
        await retro.OpenAsync(Project, sprint);
        var note = retro.Board.NoteOf(text);

        // No menu to archive it, and deciding on it leaves its words alone.
        await Expect(note.More).ToHaveCountAsync(0);
        await Expect(note.Archive).ToHaveCountAsync(0);
        await note.NotNeeded.ClickAsync();
        await note.WhyText.FillAsync("Handled by the deploy script now");
        await Page.RunAndWaitForApiAsync($"/api/retros/points/{pointId}/not-needed", "POST", () => note.WhySave.ClickAsync());
        await Expect(note.None).ToBeVisibleAsync();
        await note.Undo.ClickAsync();
        await Expect(note.None).ToHaveCountAsync(0);
        await note.AddAction.ClickAsync();
        await retro.Dialog.Title.FillAsync("Script the database step");
        var created = await Page.RunAndWaitForApiAsync($"/api/projects/{ProjectId}/retros/actions", "POST", () => retro.Dialog.Save.ClickAsync());
        created.Ok.ShouldBeTrue(await created.TextAsync());
        await Expect(note.Pin).ToBeVisibleAsync();
        await Expect(note.Root).ToContainTextAsync(text);

        await using var check = await OpenAsync();
        var row = await check.QuerySingleAsync<(string Text, string? Agent, Guid Retro)>(
            "SELECT p.text AS Text, p.agent_name AS Agent, p.retrospective_id AS Retro FROM retro_point p WHERE p.id = @pointId", new { pointId });
        row.Text.ShouldBe(text);
        row.Agent.ShouldBe(agentName);
        row.Retro.ShouldBe(agentRetro);
    }

    [Test]
    public async Task ActionPage_WhenSprintLevelPoint_ShouldLinkToTheHighlightedNote()
    {
        var sprint = SeedIds.Sprints.Sprint25;
        var (quoted, other) = ("Capacity was set before the leave was known", "Planning ran an hour over");
        string key;
        Guid quotedId;
        await using (var connection = await OpenAsync())
        {
            var seed = new RetroSeed(connection, ProjectId);
            var retroId = await AgentRetroAsync(connection, sprint, null);
            await seed.PointAsync(retroId, new RetroSeedPoint(RetroCategory.WentBadly, other));
            quotedId = await seed.PointAsync(retroId, new RetroSeedPoint(RetroCategory.Improve, quoted));
            var actionId = await new WorkItemBuilder(ProjectId).WithType("action").WithTitle("Set capacity after leave is booked").InsertAsync(connection);
            await seed.ActionAsync(new RetroSeedAction(actionId, PointId: quotedId, RaisedSprintId: sprint, FixType: RetroFixType.Guide));
            key = (await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @actionId", new { actionId }))!;
        }

        await Page.GotoAsync(Links.WorkItem(Project, key));
        var why = new ActionWhyPart(Page);
        await Expect(why.PointOf(quoted)).ToBeVisibleAsync();
        await why.LinkOf(quoted).ClickAsync();

        // The link is the sprint's Retro tab with point= naming the note, which is the one highlighted.
        await Page.WaitForURLAsync(url => url.Contains($"/sprints/{sprint}/retro", StringComparison.Ordinal) && url.Contains($"point={quotedId}", StringComparison.Ordinal));
        var retro = new SprintRetroPage(Page);
        await retro.Panel.WaitForAsync();
        await Expect(retro.Board.NoteOf(quoted).Root).ToHaveAttributeAsync("data-highlighted", "true");
        await Expect(retro.Board.NoteOf(other).Root).Not.ToHaveAttributeAsync("data-highlighted", "true");
    }

    [Test]
    public async Task Layout_WhenColumnIsLong_ShouldKeepTheToolbarInsideTheWindow()
    {
        var sprint = SeedIds.Sprints.Sprint26;
        await using (var connection = await OpenAsync())
        {
            var seed = new RetroSeed(connection, ProjectId);
            var retroId = await AgentRetroAsync(connection, sprint, null);
            for (var n = 1; n <= 30; n++)
            {
                await seed.PointAsync(retroId, new RetroSeedPoint(RetroCategory.Improve, $"Long column point {n:00} about how the team hands work over between people"));
            }
        }

        var retro = new SprintRetroPage(Page);
        foreach (var (width, height) in new[] { (1280, 720), (1440, 800) })
        {
            await Page.SetViewportSizeAsync(width, height);
            await retro.OpenAsync(Project, sprint);
            await Expect(retro.Points).ToHaveCountAsync(30);
            await retro.Points.Last.ScrollIntoViewIfNeededAsync();
            foreach (var control in new[] { retro.ViewByCategory, retro.ToDecideFilter, retro.WrittenByFilter })
            {
                var box = (await control.BoundingBoxAsync())!;
                box.Y.ShouldBeGreaterThanOrEqualTo(0, $"{width}x{height}: a toolbar control is above the window");
                (box.Y + box.Height).ShouldBeLessThanOrEqualTo(height, $"{width}x{height}: a toolbar control is below the window");
            }

            var overflow = await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth");
            overflow.ShouldBeFalse($"{width}x{height}: the page scrolls sideways");
            var panel = (await retro.Panel.BoundingBoxAsync())!;
            (panel.X + panel.Width).ShouldBeLessThanOrEqualTo(width, $"{width}x{height}: the Retro tab is wider than the window");
        }
    }

    // An agent's retro on the sprint (and on the item, when given); its agent name is its own so retros do not merge.
    private static async Task<Guid> AgentRetroAsync(NpgsqlConnection connection, Guid sprintId, Guid? workItemId)
        => await new RetroSeed(connection, ProjectId).RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.SubAgent, WorkItemId: workItemId, SprintId: sprintId, AgentName: $"agent-{Guid.NewGuid():N}"));

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }
}
