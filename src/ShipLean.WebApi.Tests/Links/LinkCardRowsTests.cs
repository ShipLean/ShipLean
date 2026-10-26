using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Features.Links.Rows;
using ShipLean.Contracts.Links;
using ShipLean.Contracts.Persisted;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Database;

namespace ShipLean.WebApi.Tests.Links;

// LinkCardRows on Postgres: one seeded clone for the class, a new Ship story for every test. The stored columns are read
// back through the row, so a column the row forgets to write or read fails here.
[TestFixture]
public class LinkCardRowsTests
{
    private static readonly DateTimeOffset Fetched = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);

    private ClonedDatabase _database = null!;
    private NpgsqlDataSource _dataSource = null!;
    private LinkCardRows _rows = null!;

    [OneTimeSetUp]
    public async Task CreateDatabase()
    {
        _database = await ShipLeanDatabase.CloneSeedAsync();
        _dataSource = NpgsqlDataSource.Create(DatabaseServices.PinUtc(_database.ConnectionString));
        _rows = new LinkCardRows(_dataSource);
    }

    [OneTimeTearDown]
    public async Task DropDatabase()
    {
        await _dataSource.DisposeAsync();
        await _database.DisposeAsync();
    }

    private async Task<Guid> ArrangeWorkItemAsync()
    {
        await using var connection = await _database.OpenAsync();
        return await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
    }

    [Test]
    public async Task LinkCardRows_WhenPreviewAndStateWritten_ShouldReadThemBack()
    {
        var item = await ArrangeWorkItemAsync();
        var id = await _rows.InsertUrlAsync(item, "https://github.com/acme/storefront/pull/9", "Typed", "A note", LinkKind.GithubPull, SeedIds.Users.Joel, Fetched, CancellationToken.None);

        (await _rows.UpdatePreviewAsync(
            id, new LinkPreviewWrite("Fetched title", "A description", "GitHub", "links/a/image", "links/a/favicon", LinkPreviewStatus.Ok, Fetched, 9, "acme/storefront", "priya"), CancellationToken.None)).ShouldBeTrue();
        (await _rows.UpdateStateAsync(id, new LinkStateWrite(LinkState.Merged, LinkStateSource.Reported, Fetched.AddHours(1), "Claude Code (claude.ai)"), CancellationToken.None)).ShouldBeTrue();
        var row = (await _rows.FindAsync(id, CancellationToken.None))!;

        row.Title.ShouldBe("Typed");
        row.Note.ShouldBe("A note");
        row.PreviewTitle.ShouldBe("Fetched title");
        row.PreviewDescription.ShouldBe("A description");
        row.PreviewSite.ShouldBe("GitHub");
        row.PreviewImageKey.ShouldBe("links/a/image");
        row.PreviewFaviconKey.ShouldBe("links/a/favicon");
        row.PreviewStatusValue.ShouldBe(LinkPreviewStatus.Ok);
        row.PreviewFetchedAt.ShouldBe(Fetched);
        row.Number.ShouldBe(9);
        row.Location.ShouldBe("acme/storefront");
        row.Author.ShouldBe("priya");
        row.KindOrAddress.ShouldBe(LinkKind.GithubPull);
        row.StateValue.ShouldBe(LinkState.Merged);
        row.StateSourceValue.ShouldBe(LinkStateSource.Reported);
        row.StateAsOf.ShouldBe(Fetched.AddHours(1));
        row.StateReportedBy.ShouldBe("Claude Code (claude.ai)");
        row.WorkItemId.ShouldBe(item);
        row.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        var dto = row.ToDto(new Microsoft.Extensions.Time.Testing.FakeTimeProvider(Fetched.AddHours(2)));
        dto.ImageUrl.ShouldBe($"/api/link-cards/{id}/image");
        dto.FaviconUrl.ShouldBe($"/api/link-cards/{id}/favicon");
        dto.Title.ShouldBe("Typed");
        dto.TitleSource.ShouldBe(LinkTitleSource.User);
        dto.ReportedBy.ShouldBe("Claude Code (claude.ai)");
        dto.StateAsOfLabel.ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task ListAsync_WhenLinksAddedAndOneArchived_ShouldListTheLiveOnesOldestFirst()
    {
        var item = await ArrangeWorkItemAsync();
        var first = await _rows.InsertUrlAsync(item, "https://docs.example/one", null, null, LinkKind.Page, SeedIds.Users.Joel, Fetched, CancellationToken.None);
        var second = await _rows.InsertUrlAsync(item, "https://docs.example/two", null, null, LinkKind.Page, SeedIds.Users.Joel, Fetched.AddMinutes(1), CancellationToken.None);
        var archived = await _rows.InsertUrlAsync(item, "https://docs.example/three", null, null, LinkKind.Page, SeedIds.Users.Joel, Fetched.AddMinutes(2), CancellationToken.None);

        var result = await _rows.ArchiveAsync(archived, Fetched.AddMinutes(3), CancellationToken.None);

        result.ShouldBe((item, SeedIds.Projects.Ship));
        (await _rows.ListAsync(item, CancellationToken.None)).Select(r => r.Id).ShouldBe([first, second]);
        (await _rows.FindAsync(archived, CancellationToken.None)).ShouldBeNull();
        (await _rows.ArchiveAsync(archived, Fetched.AddMinutes(4), CancellationToken.None)).ShouldBeNull();
    }

    [Test]
    public async Task UpdateTextAsync_WhenBlank_ShouldStoreNullSoTheAddressTitlesTheCard()
    {
        var item = await ArrangeWorkItemAsync();
        var id = await _rows.InsertUrlAsync(item, "https://docs.example/text", "Typed", "Note", LinkKind.Page, SeedIds.Users.Joel, Fetched, CancellationToken.None);

        await _rows.UpdateTextAsync(id, "  ", string.Empty, CancellationToken.None);
        var row = (await _rows.FindAsync(id, CancellationToken.None))!;

        row.Title.ShouldBeNull();
        row.Note.ShouldBeNull();
        row.ToDto(TimeProvider.System).Title.ShouldBe("https://docs.example/text");
    }

    [Test]
    public async Task WorkItemLinks_WhenAddedAndListed_ShouldCarryTheTargetsKeyTitleTypeAndState()
    {
        var item = await ArrangeWorkItemAsync();
        var id = await _rows.InsertWorkItemLinkAsync(item, SeedIds.WorkItems.Ship90, LinkType.BlockedBy, SeedIds.Users.Joel, Fetched, CancellationToken.None);

        var links = await _rows.ListWorkItemLinksAsync(item, CancellationToken.None);

        var link = links.ShouldHaveSingleItem();
        link.Id.ShouldBe(id);
        link.Type.ShouldBe(LinkType.BlockedBy);
        link.WorkItemId.ShouldBe(SeedIds.WorkItems.Ship90);
        link.Key.ShouldBe("SHIP-90");
        link.ItemType.Id.ShouldBe(ShipLean.Contracts.Refs.WorkItemTypeId.Epic);
        link.State.Name.ShouldNotBeNullOrWhiteSpace();
        (await _rows.ListAsync(item, CancellationToken.None)).ShouldBeEmpty();
    }

    [Test]
    public async Task FindByUrlAsync_WhenUserIsMemberOfOnlySomeProjects_ShouldFindOnlyLinksInTheirProjects()
    {
        var item = await ArrangeWorkItemAsync();
        var url = $"https://docs.example/{Guid.NewGuid():N}";
        var id = await _rows.InsertUrlAsync(item, url, null, null, LinkKind.Page, SeedIds.Users.Joel, Fetched, CancellationToken.None);

        (await _rows.FindByUrlAsync(url, SeedIds.Users.Joel, CancellationToken.None)).Select(r => r.Id).ShouldBe([id]);
        (await _rows.FindByUrlAsync(url, SeedIds.Users.Kai, CancellationToken.None)).ShouldBeEmpty();
    }

    [Test]
    public async Task FindWorkItemByKeyAsync_WhenKeyIsLowerCaseOrUnknown_ShouldMatchIgnoringCaseOrAnswerNull()
    {
        (await _rows.FindWorkItemByKeyAsync(SeedIds.Projects.Ship, " ship-90 ", CancellationToken.None)).ShouldBe(SeedIds.WorkItems.Ship90);
        (await _rows.FindWorkItemByKeyAsync(SeedIds.Projects.Ship, "SHIP-999999", CancellationToken.None)).ShouldBeNull();
        (await _rows.FindWorkItemByKeyAsync(SeedIds.Projects.Pay, "SHIP-90", CancellationToken.None)).ShouldBeNull();
    }
}
