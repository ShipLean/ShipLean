using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Links;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Links;

namespace ShipLean.WebApi.Tests.Links.Refresh;

// POST api/work-items/{id}/link-cards/refresh on SHIP-101 with a FakeLinkCardSource for "fake.example". Every test uses addresses of its own.
[TestFixture]
public class LinkCardsRefreshTests : ApiFixture
{
    private static readonly string ItemUrl = $"api/work-items/{SeedIds.WorkItems.Ship101}";

    private static readonly JsonSerializerOptions Wire = WireOptions();

    private FakeLinkCardSource Source { get; } = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton<ILinkCardSource>(Source);

    private static JsonSerializerOptions WireOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private static string NewAddress() => $"https://fake.example/{Guid.NewGuid():N}";

    private async Task<LinkCardDto> AddUnreachableAsync(string url)
    {
        Source.Respond(url, FakeLinkCardSource.Unreachable());
        using var response = await Api.PostJsonAsync($"{ItemUrl}/link-cards", new { url });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var added = (await response.Content.ReadFromJsonAsync<LinkCardDto>(Wire))!;
        added.RefreshDue.ShouldBeTrue();
        return added;
    }

    private async Task<LinkCardDto> RefreshedAsync(Guid id)
    {
        using var response = await Api.PostJsonAsync($"{ItemUrl}/link-cards/refresh");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var links = (await response.Content.ReadFromJsonAsync<LinkCardsDto>(Wire))!;
        return links.Code.Concat(links.Pages).Single(c => c.Id == id);
    }

    [Test]
    public async Task Refresh_WhenDue_ShouldUpdateStateAndPublishLinksChanged()
    {
        var url = NewAddress();
        var added = await AddUnreachableAsync(url);
        Source.Respond(url, new LinkCardFetch(LinkKind.GithubPull, "Back online", 9, "acme/storefront", null, LinkState.Merged, null, null, null, LinkPreviewStatus.Ok));
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

        var refreshed = await RefreshedAsync(added.Id);

        refreshed.State.ShouldBe(LinkState.Merged);
        refreshed.StateSource.ShouldBe(LinkStateSource.Live);
        refreshed.RefreshDue.ShouldBeFalse();
        (await seen.Task).WorkItemId.ShouldBe(SeedIds.WorkItems.Ship101);
    }

    [Test]
    public async Task Refresh_WhenFetchFails_ShouldKeepReportedState()
    {
        var url = NewAddress();
        var added = await AddUnreachableAsync(url);
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync(
                "UPDATE work_item_link SET state = 'closed', state_source = 'reported', state_as_of = @asOf, state_reported_by = 'Agent (unverified)' WHERE id = @id",
                new { id = added.Id, asOf = new DateTimeOffset(2026, 10, 25, 9, 0, 0, TimeSpan.Zero) });
        }

        var refreshed = await RefreshedAsync(added.Id);

        refreshed.State.ShouldBe(LinkState.Closed);
        refreshed.StateSource.ShouldBe(LinkStateSource.Reported);
        refreshed.ReportedBy.ShouldBe("Agent (unverified)");
        refreshed.PreviewStatus.ShouldBe(LinkPreviewStatus.Unreachable);
    }

    [Test]
    public async Task Refresh_WhenNonMember_ShouldReturnNotFound()
    {
        var url = NewAddress();
        var added = await AddUnreachableAsync(url);
        Source.Respond(url, new LinkCardFetch(LinkKind.GithubPull, "Not for Kai", 9, "acme/storefront", null, LinkState.Merged, null, null, null, LinkPreviewStatus.Ok));

        using var response = await Api.ActingAs(TestUser.Kai).PostJsonAsync($"{ItemUrl}/link-cards/refresh");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        Source.Fetched.Count(f => f == new Uri(url).AbsoluteUri).ShouldBe(1);
        (await RefreshedAsync(added.Id)).Title.ShouldBe("Not for Kai");
    }
}
