using System.Net;
using Dapper;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Links.Rows;
using ShipLean.Contracts.Links;
using ShipLean.Contracts.Persisted;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Links;
using Shouldly;

namespace ShipLean.WebApi.Tests.Links.Cards;

// What the real card service decides beyond the endpoint contract: the fallback preview, which cards are due for a refresh,
// the lowest-Order source winning, and what stays unfetched on a list. One host for the class; each test uses addresses of its own on SHIP-101.
[TestFixture]
public class LinkCardsServiceTests : ApiFixture
{
    private static readonly string ItemUrl = $"api/work-items/{SeedIds.WorkItems.Ship101}";

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    private readonly FakeLinkCardSource _early = new(order: 10, host: "early.example");
    private readonly FakeLinkCardSource _late = new(order: 90, host: "early.example");

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton<ILinkCardSource>(_late);
        services.AddSingleton<ILinkCardSource>(_early);
    };

    private static string NewAddress(string host = "plain.example") => $"https://{host}/{Guid.NewGuid():N}";

    private async Task<LinkCardDto> AddAsync(string url)
    {
        using var response = await Api.PostJsonAsync($"{ItemUrl}/link-cards", new { url });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<LinkCardDto>(Wire))!;
    }

    private async Task<LinkCardDto> ListedAsync(Guid id)
    {
        using var response = await Api.GetAsync($"{ItemUrl}/link-cards");
        var links = (await response.Content.ReadFromJsonAsync<LinkCardsDto>(Wire))!;
        return links.Code.Concat(links.Pages).Single(c => c.Id == id);
    }

    // Every http/https address has a source (Open Graph, Order 100), so the fallback card means that source couldn't read it: in
    // test hosts NoNetworkHostResolver answers HostNotFound for "plain.example".
    [Test]
    public async Task Preview_WhenTheOpenGraphSourceCannotReachTheHost_ShouldReturnUnreachableFallbackCard()
    {
        var url = NewAddress();

        using var response = await Api.PostJsonAsync($"{ItemUrl}/link-cards/preview", new { url });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var preview = (await response.Content.ReadFromJsonAsync<LinkPreviewDto>(Wire))!;
        preview.Title.ShouldBe(url);
        preview.TitleSource.ShouldBe(LinkTitleSource.Url);
        preview.Kind.ShouldBe(LinkKind.Page);
        preview.Host.ShouldBe("plain.example");
        preview.PreviewStatus.ShouldBe(LinkPreviewStatus.Unreachable);
        preview.ImageUrl.ShouldBeNull();
        preview.FaviconUrl.ShouldBeNull();
        preview.Message.ShouldBe("Couldn't reach that address: no preview yet. It will be tried again.");
    }

    [Test]
    public async Task Preview_WhenPrivate_ShouldSayAnAgentCanReportItsState()
    {
        var url = NewAddress(_early.Host);
        _early.Respond(url, new LinkCardFetch(LinkKind.Page, null, null, null, null, null, null, null, null, LinkPreviewStatus.Private));

        using var response = await Api.PostJsonAsync($"{ItemUrl}/link-cards/preview", new { url });

        (await response.Content.ReadFromJsonAsync<LinkPreviewDto>(Wire))!.Message.ShouldBe("Private address: no preview. An agent can report its state.");
    }

    [Test]
    public async Task AddUrl_WhenTwoSourcesHandle_ShouldUseTheLowestOrder()
    {
        var url = NewAddress(_early.Host);
        _early.Respond(url, FakeLinkCardSource.ReachablePage("From the early source"));
        _late.Respond(url, FakeLinkCardSource.ReachablePage("From the late source"));

        var added = await AddAsync(url);

        added.Title.ShouldBe("From the early source");
        _late.Fetched.ShouldNotContain(new Uri(url).AbsoluteUri);
    }

    [Test]
    public async Task List_WhenStateOlderThan10Minutes_ShouldMarkRefreshDue()
    {
        var url = NewAddress(_early.Host);
        _early.Respond(url, new LinkCardFetch(LinkKind.GithubPull, "Old state", 3, "acme/storefront", null, LinkState.Open, null, null, null, LinkPreviewStatus.Ok));
        var added = await AddAsync(url);
        added.RefreshDue.ShouldBeFalse();
        using var scope = Host.Services.CreateScope();
        var rows = scope.ServiceProvider.GetRequiredService<LinkCardRows>();

        await rows.UpdateStateAsync(added.Id, new LinkStateWrite(LinkState.Open, LinkStateSource.Live, TimeProvider.System.GetUtcNow().AddMinutes(-11), null), CancellationToken.None);

        (await ListedAsync(added.Id)).RefreshDue.ShouldBeTrue();
    }

    [Test]
    public async Task List_WhenFreshlyFetched_ShouldNotFetchAgainAndNotBeDue()
    {
        var url = NewAddress(_early.Host);
        var added = await AddAsync(url);
        var fetchedBefore = _early.Fetched.Count;

        var listed = await ListedAsync(added.Id);

        listed.RefreshDue.ShouldBeFalse();
        _early.Fetched.Count.ShouldBe(fetchedBefore);
    }

    // An address the Open Graph source couldn't read is due (to be tried again), and so is a stored card a source can read but none
    // has read yet.
    [Test]
    public async Task List_WhenTheSourceCouldNotReadItOrNoneHasReadItYet_ShouldMarkRefreshDue()
    {
        var unreadable = await AddAsync(NewAddress());
        unreadable.PreviewStatus.ShouldBe(LinkPreviewStatus.Unreachable);
        unreadable.TitleSource.ShouldBe(LinkTitleSource.Url);
        unreadable.RefreshDue.ShouldBeTrue();
        (await ListedAsync(unreadable.Id)).RefreshDue.ShouldBeTrue();
        var late = NewAddress(_early.Host);
        using var scope = Host.Services.CreateScope();
        var rows = scope.ServiceProvider.GetRequiredService<LinkCardRows>();
        var pending = await rows.InsertUrlAsync(SeedIds.WorkItems.Ship101, late, null, null, LinkKind.Page, SeedIds.Users.Joel, TimeProvider.System.GetUtcNow(), CancellationToken.None);

        var listed = await ListedAsync(pending);

        listed.PreviewStatus.ShouldBe(LinkPreviewStatus.None);
        listed.TitleSource.ShouldBe(LinkTitleSource.Url);
        listed.RefreshDue.ShouldBeTrue();
    }

    [Test]
    public async Task AddWorkItemLink_WhenTargetIsInAnotherProject_ShouldReturnNotFound()
    {
        var key = await OtherProjectItemKeyAsync();

        using var response = await Api.PostJsonAsync($"{ItemUrl}/work-item-links", new { targetKey = key, type = "related" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Edit_WhenALinkToAWorkItem_ShouldReturnNotFound()
    {
        using var added = await Api.PostJsonAsync($"{ItemUrl}/work-item-links", new { targetKey = "SHIP-93", type = "related" });
        var row = (await added.Content.ReadFromJsonAsync<WorkItemLinkRowDto>(Wire))!;

        using var response = await Api.PatchJsonAsync($"api/link-cards/{row.Id}", new { title = "x" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Edit_WhenArchived_ShouldReturnNotFound()
    {
        var added = await AddAsync(NewAddress());
        using var archived = await Api.PostJsonAsync($"api/link-cards/{added.Id}/archive");
        archived.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var response = await Api.PatchJsonAsync($"api/link-cards/{added.Id}", new { title = "x" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<string> OtherProjectItemKeyAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<string>( "SELECT key FROM work_item WHERE project_id <> @ship LIMIT 1", new { ship = SeedIds.Projects.Ship });
    }
}
