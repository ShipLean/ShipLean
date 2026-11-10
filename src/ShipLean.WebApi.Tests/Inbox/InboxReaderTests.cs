using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Application.Features.Inbox;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Inbox;
using ShipLean.Contracts.Refs;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Inbox;

namespace ShipLean.WebApi.Tests.Inbox;

// GET inbox composed from FakeInboxAskSource rows, with the real host and Postgres for item metadata and links. Each test sets
// the fake the host's single registered source delegates to, and arranges its own items; Tom is the signed-in user.
[TestFixture]
public class InboxReaderTests : ApiFixture
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private static readonly UserRef Raiser = new(SeedIds.Users.Priya, "Priya Shah", "PS", "#888888", null);

    // Two registered sources, each handing every call to the fake the current test chose: the reader must compose both.
    private static readonly SwitchableSource Source = new();

    private static readonly SwitchableSource Second = new();

    protected override TestUser SignedInAs => TestUser.Tom;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<IInboxAskSource>();
        services.AddSingleton<IInboxAskSource>(Source);
        services.AddSingleton<IInboxAskSource>(Second);
    };

    private static DateTimeOffset Now => TimeProvider.System.GetUtcNow();

    private static InboxAskDto Question(int number, DateTimeOffset at) => new(
        InboxAskKind.Question, $"Q{number}", at, "now", Raiser, null, null, new InboxQuestionDto(number, "Which way?", []), null, null);

    private static InboxAskDto Decision(int number, DateTimeOffset at) => new(
        InboxAskKind.Decision, $"D{number}", at, "now", Raiser, null, new InboxDecisionDto(number, "Use X?", "Yes", ["No"]), null, null, null);

    private static InboxAskDto Mention(DateTimeOffset at) => new(
        InboxAskKind.Mention, "@", at, "now", Raiser, null, null, null, new InboxMentionDto(Guid.NewGuid(), Guid.NewGuid(), "Look at this"), null);

    private static InboxAskDto Status(DateTimeOffset at) => new(
        InboxAskKind.Status, "Status", at, "now", null, null, null, null, null, new InboxStatusDto("On track", null));

    private async Task<Guid> ItemAsync(string type = "story", string title = "Arranged item", Guid? parent = null)
    {
        await using var connection = await OpenDatabaseAsync();
        var builder = new WorkItemBuilder(SeedIds.Projects.Ship).WithType(type).WithTitle(title);
        return await (parent is { } p ? builder.WithParent(p) : builder).InsertAsync(connection);
    }

    private async Task<InboxDto> GetAsync(
        IReadOnlyList<InboxAskRow> rows, IReadOnlyDictionary<Guid, int>? counts = null, bool ever = false, FakeInboxAskSource? second = null)
    {
        Source.Current = new FakeInboxAskSource(rows, counts, ever);
        Second.Current = second ?? new FakeInboxAskSource();
        using var response = await Api.ActingAs(TestUser.Tom).GetAsync($"api/projects/{SeedIds.Projects.Ship}/inbox");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<InboxDto>(text, Wire)!;
    }

    private async Task<string> KeyOfAsync(Guid id)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id });
    }

    [Test]
    public async Task GetInbox_WhenSourcesReturnRows_ShouldFillTypeKeyTitleAndRevision()
    {
        var bug = await ItemAsync("bug", "Crash on save");

        var inbox = await GetAsync([new InboxAskRow(bug, Question(1, Now.AddHours(-3)))]);

        var item = inbox.Items.ShouldHaveSingleItem();
        item.Id.ShouldBe(bug);
        item.Key.ShouldBe(await KeyOfAsync(bug));
        item.Type.ShouldBe("bug");
        item.Title.ShouldBe("Crash on save");
        item.Revision.ShouldBe(1);
        item.AgeLabel.ShouldBe("3h");
        inbox.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        inbox.OpenAsks.ShouldBe(1);
        inbox.OpenItems.ShouldBe(1);
        inbox.LoadedAtLabel.ShouldMatch(@"^\d\d:\d\d$");
    }

    [Test]
    public async Task GetInbox_WhenItemHasTwoMentions_ShouldLabelThemAt1AndAt2OldestFirst()
    {
        var first = await ItemAsync();
        var second = await ItemAsync();
        var older = Mention(Now.AddHours(-30));
        var newer = Mention(Now.AddHours(-5));

        // The second item also has a mention: numbering restarts per item, it does not run across the Inbox.
        var inbox = await GetAsync(
        [
            new InboxAskRow(first, newer), new InboxAskRow(first, older),
            new InboxAskRow(second, Mention(Now.AddHours(-1))),
        ]);

        var asks = inbox.Items.Single(i => i.Id == first).Asks;
        asks.Select(a => a.Label).ShouldBe(["@1", "@2"]);
        asks[0].Mention!.MentionId.ShouldBe(older.Mention!.MentionId);
        inbox.Items.Single(i => i.Id == second).Asks.Select(a => a.Label).ShouldBe(["@1"]);
    }

    [Test]
    public async Task GetInbox_WhenItemHasEveryKind_ShouldOrderQuestionsDecisionsMentionsThenStatus()
    {
        var feature = await ItemAsync("feature");
        var at = Now.AddHours(-10);

        var inbox = await GetAsync(
        [
            new InboxAskRow(feature, Status(at)), new InboxAskRow(feature, Mention(at.AddHours(2))),
            new InboxAskRow(feature, Decision(2, at)), new InboxAskRow(feature, Decision(1, at)),
            new InboxAskRow(feature, Question(2, at)), new InboxAskRow(feature, Mention(at.AddHours(1))),
            new InboxAskRow(feature, Question(1, at)),
        ]);

        inbox.Items.ShouldHaveSingleItem().Asks.Select(a => a.Label).ShouldBe(["Q1", "Q2", "D1", "D2", "@1", "@2", "Status"]);
    }

    [Test]
    public async Task GetInbox_WhenFourItemsBlockedByIt_ShouldListThreeAndOneMore()
    {
        var blocker = await ItemAsync("story", "Blocker");
        var blocked = new List<Guid>();
        for (var i = 1; i <= 4; i++)
        {
            blocked.Add(await ItemAsync("story", $"Waiting {i}"));
        }
        await BlockAsync(blocked, blocker);

        var inbox = await GetAsync([new InboxAskRow(blocker, Question(1, Now.AddHours(-2)))]);

        var item = inbox.Items.ShouldHaveSingleItem();
        item.WaitedOnBy.Select(r => r.Title).ShouldBe(["Waiting 1", "Waiting 2", "Waiting 3"]);
        item.WaitedOnBy[0].Key.ShouldBe(await KeyOfAsync(blocked[0]));
        item.WaitedOnByMore.ShouldBe(1);
    }

    [Test]
    public async Task GetInbox_WhenBlockedByLinkArchived_ShouldIgnoreIt()
    {
        var blocker = await ItemAsync("story", "Blocker");
        var live = await ItemAsync("story", "Still waiting");
        var archived = await ItemAsync("story", "Link archived");
        await BlockAsync([live, archived], blocker);
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync(
                "UPDATE work_item_link SET archived_at = now() WHERE source_id = @archived", new { archived });
        }

        var inbox = await GetAsync([new InboxAskRow(blocker, Question(1, Now.AddHours(-2)))]);

        var item = inbox.Items.ShouldHaveSingleItem();
        item.WaitedOnBy.Select(r => r.Title).ShouldBe(["Still waiting"]);
        item.WaitedOnByMore.ShouldBe(0);
    }

    [Test]
    public async Task GetInbox_WhenOtherProjectsHaveCounts_ShouldSumThemAndDropZeros()
    {
        var inbox = await GetAsync(
            [],
            new Dictionary<Guid, int> { [SeedIds.Projects.Part] = 2, [SeedIds.Projects.Ship] = 9 },
            second: new FakeInboxAskSource(counts: new Dictionary<Guid, int> { [SeedIds.Projects.Part] = 3, [SeedIds.Projects.Mob] = 0 }));

        var other = inbox.OtherProjects.ShouldHaveSingleItem();
        other.ProjectId.ShouldBe(SeedIds.Projects.Part);
        other.OpenAsks.ShouldBe(5);
        other.ProjectKey.ShouldBe("PART");
    }

    [TestCase(2 * 24 * 60 + 5, true)]
    [TestCase(2 * 24 * 60 - 5, false)]
    public async Task GetInbox_WhenOldestAskJustOverTwoDays_ShouldSetLongWait(int minutesAgo, bool expected)
    {
        var item = await ItemAsync();
        var oldest = Now.AddMinutes(-minutesAgo);

        // A newer ask on the same item must not move the clock: LongWait runs from the oldest.
        var inbox = await GetAsync([new InboxAskRow(item, Question(1, Now.AddMinutes(-10))), new InboxAskRow(item, Decision(1, oldest))]);

        inbox.Items.ShouldHaveSingleItem().LongWait.ShouldBe(expected);
    }

    [Test]
    public async Task GetInbox_WhenOnlyOneSourceEverHadAsks_ShouldSetEverWaited()
    {
        var none = await GetAsync([]);
        var some = await GetAsync([], second: new FakeInboxAskSource(ever: true));
        var firstOnly = await GetAsync([], ever: true);

        none.EverWaited.ShouldBeFalse();
        some.EverWaited.ShouldBeTrue();
        firstOnly.EverWaited.ShouldBeTrue();
    }

    [Test]
    public async Task GetInbox_WhenCallerNotMember_ShouldReturnNotFound()
    {
        var item = await ItemAsync();
        Source.Current = new FakeInboxAskSource([new InboxAskRow(item, Question(1, Now.AddHours(-1)))]);
        Second.Current = new FakeInboxAskSource();

        using var response = await Api.ActingAs(TestUser.Kai).GetAsync($"api/projects/{SeedIds.Projects.Ship}/inbox");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    private async Task BlockAsync(IEnumerable<Guid> waiting, Guid blocker)
    {
        await using var connection = await OpenDatabaseAsync();
        foreach (var source in waiting)
        {
            await connection.ExecuteAsync(
                "INSERT INTO work_item_link (id, source_id, target_id, link_type) VALUES (@id, @source, @blocker, 'blockedBy')",
                new { id = Guid.NewGuid(), source, blocker });
        }
    }

    private sealed class SwitchableSource : IInboxAskSource
    {
        public FakeInboxAskSource Current { get; set; } = new();

        public Task<IReadOnlyList<InboxAskRow>> OpenAsync(Guid userId, Guid projectId, CancellationToken cancellationToken = default) =>
            Current.OpenAsync(userId, projectId, cancellationToken);

        public Task<IReadOnlyDictionary<Guid, int>> OpenCountsByProjectAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Current.OpenCountsByProjectAsync(userId, cancellationToken);

        public Task<bool> EverAsync(Guid userId, CancellationToken cancellationToken = default) => Current.EverAsync(userId, cancellationToken);
    }
}
