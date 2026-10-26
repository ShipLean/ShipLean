using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Links;
using ShipLean.Contracts.Persisted;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Links;
using Shouldly;

namespace ShipLean.WebApi.Tests.Links;

// What the link endpoints do, whatever sits behind ILinkCards (the contract of the link API). One host and database for the class; every test uses addresses
// of its own on SHIP-101 (a Feature). A FakeLinkCardSource handles "fake.example". Cases marked [Category("L1")] describe the
// real card service (validation, repeats, reading addresses through sources, RefreshDue) and [Category("L4")] the refresh;
// the rest hold for the basic-persistence stub too.
[TestFixture]
public class LinkCardsApiContract : ApiFixture
{
    private static readonly string ItemUrl = $"api/work-items/{SeedIds.WorkItems.Ship101}";

    private static readonly JsonSerializerOptions Wire = WireOptions();

    protected FakeLinkCardSource Source { get; } = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton<ILinkCardSource>(Source);

    private static string NewAddress(string host = "docs.example") => $"https://{host}/{Guid.NewGuid():N}";

    private static JsonSerializerOptions WireOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private async Task<LinkCardDto> AddAsync(string url, string? title = null, string? note = null)
    {
        using var response = await Api.PostJsonAsync($"{ItemUrl}/link-cards", new { url, title, note });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<LinkCardDto>(Wire))!;
    }

    private async Task<LinkCardsDto> ListAsync()
    {
        using var response = await Api.GetAsync($"{ItemUrl}/link-cards");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<LinkCardsDto>(Wire))!;
    }

    private static IEnumerable<LinkCardDto> Cards(LinkCardsDto links) => links.Code.Concat(links.Pages);

    [Test]
    public async Task AddUrl_ThenList_ShouldReturnStoredCard()
    {
        var url = NewAddress();

        var added = await AddAsync(url, "Rollout plan", "Read before the review");
        var stored = Cards(await ListAsync()).Single(c => c.Id == added.Id);

        stored.ShouldBe(added);
        stored.Url.ShouldBe(url);
        stored.Title.ShouldBe("Rollout plan");
        stored.TitleSource.ShouldBe(LinkTitleSource.User);
        stored.Note.ShouldBe("Read before the review");
        stored.Kind.ShouldBe(LinkKind.Page);
        stored.Group.ShouldBe(LinkGroup.Page);
        stored.Host.ShouldBe("docs.example");
        (await ListAsync()).Pages.ShouldContain(c => c.Id == added.Id);
    }

    [Test]
    public async Task AddUrl_WhenNoSourceHandlesAndNoTitleGiven_ShouldTitleTheCardWithTheAddress()
    {
        var url = NewAddress();

        var added = await AddAsync(url);

        added.Title.ShouldBe(url);
        added.TitleSource.ShouldBe(LinkTitleSource.Url);
        added.StateSource.ShouldBe(LinkStateSource.None);
        added.State.ShouldBeNull();
    }

    [Test]
    public async Task AddUrl_WhenPullRequestAddress_ShouldFileItUnderCodeWithItsNumberAndLocation()
    {
        var url = $"https://github.com/acme/storefront/pull/{Random.Shared.Next(1000, 999999)}";

        var added = await AddAsync(url);

        added.Kind.ShouldBe(LinkKind.GithubPull);
        added.Group.ShouldBe(LinkGroup.Code);
        added.Number.ShouldBe(int.Parse(url[(url.LastIndexOf('/') + 1)..]));
        added.Location.ShouldBe("acme/storefront");
        (await ListAsync()).Code.ShouldContain(c => c.Id == added.Id);
    }

    [Test]
    public async Task AddUrl_ShouldPublishLinksChanged()
    {
        var seen = new TaskCompletionSource<LinksChanged>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var onTimeout = timeout.Token.Register(() => seen.TrySetCanceled());
        using var subscription = Host.Services.GetRequiredService<IProjectEvents>().Subscribe(SeedIds.Projects.Ship, e =>
        {
            if (e is LinksChanged changed)
            {
                seen.TrySetResult(changed);
            }
            return Task.CompletedTask;
        });

        await AddAsync(NewAddress());

        (await seen.Task).WorkItemId.ShouldBe(SeedIds.WorkItems.Ship101);
    }

    [Test]
    public async Task Edit_WhenTitleSent_ShouldChangeTheTitleAndKeepTheNote()
    {
        var added = await AddAsync(NewAddress(), "Old title", "Keep me");

        using var response = await Api.PatchJsonAsync($"api/link-cards/{added.Id}", new { title = "New title" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var edited = (await response.Content.ReadFromJsonAsync<LinkCardDto>(Wire))!;
        edited.Title.ShouldBe("New title");
        edited.Note.ShouldBe("Keep me");
        Cards(await ListAsync()).Single(c => c.Id == added.Id).Title.ShouldBe("New title");
    }

    [Test]
    public async Task Edit_WhenTitleIsEmpty_ShouldClearItBackToTheAddress()
    {
        var url = NewAddress();
        var added = await AddAsync(url, "Typed title");

        using var response = await Api.PatchJsonAsync($"api/link-cards/{added.Id}", new { title = "" });

        var edited = (await response.Content.ReadFromJsonAsync<LinkCardDto>(Wire))!;
        edited.Title.ShouldBe(url);
        edited.TitleSource.ShouldBe(LinkTitleSource.Url);
    }

    [Test]
    public async Task Archive_ShouldHideTheLinkAndAnswerNotFoundTheSecondTime()
    {
        var added = await AddAsync(NewAddress());

        using var archived = await Api.PostJsonAsync($"api/link-cards/{added.Id}/archive");
        using var again = await Api.PostJsonAsync($"api/link-cards/{added.Id}/archive");

        archived.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ApiClient.ReadApiErrorAsync(again, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        Cards(await ListAsync()).ShouldNotContain(c => c.Id == added.Id);
    }

    [Test]
    public async Task AddWorkItemLink_ThenList_ShouldReturnTheRowWithTheOtherItem()
    {
        using var response = await Api.PostJsonAsync($"{ItemUrl}/work-item-links", new { targetKey = "SHIP-91", type = "related" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var row = (await response.Content.ReadFromJsonAsync<WorkItemLinkRowDto>(Wire))!;
        row.Key.ShouldBe("SHIP-91");
        row.Type.ShouldBe(LinkType.Related);
        row.Title.ShouldNotBeNullOrWhiteSpace();
        row.State.Name.ShouldNotBeNullOrWhiteSpace();
        (await ListAsync()).WorkItems.ShouldContain(r => r.Id == row.Id);
        using var archived = await Api.PostJsonAsync($"api/link-cards/{row.Id}/archive");
        archived.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ListAsync()).WorkItems.ShouldNotContain(r => r.Id == row.Id);
    }

    [Test]
    public async Task AddWorkItemLink_WhenTargetKeyIsUnknown_ShouldBeNotFound()
    {
        using var response = await Api.PostJsonAsync($"{ItemUrl}/work-item-links", new { targetKey = "SHIP-99999", type = "related" });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task AddWorkItemLink_WhenLinkingTheItemToItself_ShouldBeValidation()
    {
        using var response = await Api.PostJsonAsync($"{ItemUrl}/work-item-links", new { targetKey = "SHIP-101", type = "related" });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task LinkCards_WhenNonMember_ShouldReturnNotFound()
    {
        var links = Api.ActingAs(TestUser.Kai);
        var seededCard = Cards(await ListAsync()).Single(c => c.Id == SeedIds.Links.Ship101Pull);

        var responses = new[]
        {
            await links.GetAsync($"{ItemUrl}/link-cards"),
            await links.PostJsonAsync($"{ItemUrl}/link-cards/preview", new { url = NewAddress() }),
            await links.PostJsonAsync($"{ItemUrl}/link-cards", new { url = NewAddress() }),
            await links.PostJsonAsync($"{ItemUrl}/work-item-links", new { targetKey = "SHIP-90", type = "related" }),
            await links.PostJsonAsync($"{ItemUrl}/link-cards/refresh"),
            await links.PatchJsonAsync($"api/link-cards/{seededCard.Id}", new { title = "Not yours" }),
            await links.PostJsonAsync($"api/link-cards/{seededCard.Id}/archive"),
        };

        foreach (var response in responses)
        {
            using (response)
            {
                (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
            }
        }
        Cards(await ListAsync()).Single(c => c.Id == seededCard.Id).Title.ShouldNotBe("Not yours");
    }

    // ---- L1: the real card service ----

    [Category("L1")]
    [TestCase("ftp://docs.example/spec")]
    [TestCase("javascript:alert(1)")]
    [TestCase("docs.example/spec")]
    [TestCase("")]
    public async Task AddUrl_WhenAddressIsNotAbsoluteHttp_ShouldBeInvalidUrlAndKeepNothing(string url)
    {
        var before = Cards(await ListAsync()).Count();

        using var response = await Api.PostJsonAsync($"{ItemUrl}/link-cards", new { url });

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.LinkInvalidUrl);
        error.Field.ShouldBe("url");
        Cards(await ListAsync()).Count().ShouldBe(before);
    }

    [Category("L1")]
    [Test]
    public async Task AddUrl_WhenAddressIsLongerThan2048_ShouldBeInvalidUrl()
    {
        using var response = await Api.PostJsonAsync($"{ItemUrl}/link-cards", new { url = $"https://docs.example/{new string('a', 2048)}" });

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.LinkInvalidUrl);
        error.Field.ShouldBe("url");
    }

    [Category("L1")]
    [Test]
    public async Task AddUrl_WhenSameAddressIsAddedTwice_ShouldBeLinkDuplicate()
    {
        var url = NewAddress();
        await AddAsync(url);

        using var response = await Api.PostJsonAsync($"{ItemUrl}/link-cards", new { url });

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict);
        error.Code.ShouldBe(ApiErrorCodes.LinkDuplicate);
        error.Field.ShouldBe("url");
        Cards(await ListAsync()).Count(c => c.Url == url).ShouldBe(1);
    }

    [Category("L1")]
    [Test]
    public async Task AddWorkItemLink_WhenSameTargetAndTypeAreAddedTwice_ShouldBeLinkDuplicate()
    {
        var body = new { targetKey = "SHIP-92", type = "blockedBy" };
        using var first = await Api.PostJsonAsync($"{ItemUrl}/work-item-links", body);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var second = await Api.PostJsonAsync($"{ItemUrl}/work-item-links", body);

        var error = await ApiClient.ReadApiErrorAsync(second, HttpStatusCode.Conflict);
        error.Code.ShouldBe(ApiErrorCodes.LinkDuplicate);
        error.Field.ShouldBe("targetKey");
    }

    [Category("L1")]
    [Test]
    public async Task AddUrl_WhenASourceHandlesTheAddress_ShouldStoreWhatTheSourceRead()
    {
        var url = NewAddress(Source.Host);
        Source.Respond(url, new LinkCardFetch(LinkKind.GithubPull, "Fix the picker", 12, "acme/storefront", "bob", LinkState.Open, "Fixes it.", null, null, LinkPreviewStatus.Ok));

        var added = await AddAsync(url);

        added.Title.ShouldBe("Fix the picker");
        added.TitleSource.ShouldBeOneOf(LinkTitleSource.Live, LinkTitleSource.Fetched);
        added.Kind.ShouldBe(LinkKind.GithubPull);
        added.Group.ShouldBe(LinkGroup.Code);
        added.Number.ShouldBe(12);
        added.Location.ShouldBe("acme/storefront");
        added.Author.ShouldBe("bob");
        added.State.ShouldBe(LinkState.Open);
        added.StateSource.ShouldBe(LinkStateSource.Live);
        added.Description.ShouldBe("Fixes it.");
        added.PreviewStatus.ShouldBe(LinkPreviewStatus.Ok);
        added.RefreshDue.ShouldBeFalse();
    }

    [Category("L1")]
    [Test]
    public async Task AddUrl_WhenATitleIsGivenAndASourceHandlesTheAddress_ShouldKeepTheTypedTitle()
    {
        var url = NewAddress(Source.Host);
        Source.Respond(url, FakeLinkCardSource.ReachablePage("Fetched title"));

        var added = await AddAsync(url, "My title");

        added.Title.ShouldBe("My title");
        added.TitleSource.ShouldBe(LinkTitleSource.User);
    }

    [Category("L1")]
    [Test]
    public async Task AddUrl_WhenTheSourceStoredAnImage_ShouldServeItFromShipLeanNeverTheExternalAddress()
    {
        var url = NewAddress(Source.Host);
        Source.Respond(url, new LinkCardFetch(LinkKind.Page, "With image", null, null, null, null, null, "links/test/image.png", "links/test/favicon.png", LinkPreviewStatus.Ok));

        var added = await AddAsync(url);

        added.ImageUrl.ShouldBe($"/api/link-cards/{added.Id}/image");
        added.FaviconUrl.ShouldBe($"/api/link-cards/{added.Id}/favicon");
    }

    [Category("L1")]
    [Test]
    public async Task AddUrl_WhenTheSourceCannotReachTheAddress_ShouldStillAddItAsDue()
    {
        var url = NewAddress(Source.Host);
        Source.Respond(url, FakeLinkCardSource.Unreachable());

        var added = await AddAsync(url);

        added.PreviewStatus.ShouldBe(LinkPreviewStatus.Unreachable);
        added.Title.ShouldBe(url);
        added.RefreshDue.ShouldBeTrue();
    }

    [Category("L1")]
    [Test]
    public async Task Preview_WhenASourceHandlesTheAddress_ShouldShowWhatItReadAndSaveNothing()
    {
        var url = NewAddress(Source.Host);
        Source.Respond(url, FakeLinkCardSource.ReachablePage("Previewed page"));
        var before = Cards(await ListAsync()).Count();

        using var response = await Api.PostJsonAsync($"{ItemUrl}/link-cards/preview", new { url });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var preview = (await response.Content.ReadFromJsonAsync<LinkPreviewDto>(Wire))!;
        preview.Title.ShouldBe("Previewed page");
        preview.PreviewStatus.ShouldBe(LinkPreviewStatus.Ok);
        preview.Message.ShouldNotBeNullOrWhiteSpace();
        Cards(await ListAsync()).Count().ShouldBe(before);
    }

    [Category("L1")]
    [Test]
    public async Task Preview_WhenTheHostIsUnreachable_ShouldAnswerOkWithAnUnreachablePreview()
    {
        var url = NewAddress(Source.Host);
        Source.Respond(url, FakeLinkCardSource.Unreachable());

        using var response = await Api.PostJsonAsync($"{ItemUrl}/link-cards/preview", new { url });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<LinkPreviewDto>(Wire))!.PreviewStatus.ShouldBe(LinkPreviewStatus.Unreachable);
    }

    [Category("L1")]
    [Test]
    public async Task Preview_WhenTheAddressIsNotHttp_ShouldBeInvalidUrl()
    {
        using var response = await Api.PostJsonAsync($"{ItemUrl}/link-cards/preview", new { url = "ftp://docs.example/spec" });

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.LinkInvalidUrl);
        error.Field.ShouldBe("url");
    }

    // ---- L4: refresh ----

    [Category("L4")]
    [Test]
    public async Task Refresh_WhenNothingIsDue_ShouldAnswerTheLinksUnchanged()
    {
        var url = NewAddress();
        await AddAsync(url);
        var before = await ListAsync();

        using var response = await Api.PostJsonAsync($"{ItemUrl}/link-cards/refresh");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var after = (await response.Content.ReadFromJsonAsync<LinkCardsDto>(Wire))!;
        Cards(after).Select(c => c.Id).ShouldBe(Cards(before).Select(c => c.Id));
    }

    [Category("L4")]
    [Test]
    public async Task Refresh_WhenACardIsDueAndTheAddressIsNowReadable_ShouldUpdateItAndClearDue()
    {
        var url = NewAddress(Source.Host);
        Source.Respond(url, FakeLinkCardSource.Unreachable());
        var added = await AddAsync(url);
        added.RefreshDue.ShouldBeTrue();
        Source.Respond(url, new LinkCardFetch(LinkKind.GithubPull, "Back online", 9, "acme/storefront", null, LinkState.Merged, null, null, null, LinkPreviewStatus.Ok));

        using var response = await Api.PostJsonAsync($"{ItemUrl}/link-cards/refresh");

        var refreshed = Cards((await response.Content.ReadFromJsonAsync<LinkCardsDto>(Wire))!).Single(c => c.Id == added.Id);
        refreshed.Title.ShouldBe("Back online");
        refreshed.State.ShouldBe(LinkState.Merged);
        refreshed.PreviewStatus.ShouldBe(LinkPreviewStatus.Ok);
        refreshed.RefreshDue.ShouldBeFalse();
    }
}
