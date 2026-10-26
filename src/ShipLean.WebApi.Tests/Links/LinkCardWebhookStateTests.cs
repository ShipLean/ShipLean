using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Links.Rows;
using ShipLean.Application.Features.Webhooks;
using ShipLean.Contracts.Links;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Webhooks;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Links;
using ShipLean.TestSupport.Webhooks;
using ShipLean.WebApi.Tests.Webhooks;

namespace ShipLean.WebApi.Tests.Links;

// A Feature's pull request card and the state a webhook last reported for the same address: the later as-of wins, a tie goes
// to the card, and the card says where its state came from. Each test uses a pull request number of its own on SHIP-101.
[TestFixture]
public class LinkCardWebhookStateTests : WebhookApiTest
{
    private static readonly string ItemUrl = $"api/work-items/{SeedIds.WorkItems.Ship101}";

    private static readonly JsonSerializerOptions CardWire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static readonly DateTimeOffset CardEarlier = new(2026, 10, 25, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CardLater = new(2026, 10, 25, 23, 0, 0, TimeSpan.Zero);

    // Fake sources that win over any production source (lowest Order) for the addresses these tests add, so nothing is fetched.
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton<ILinkCardSource>(new FakeLinkCardSource(order: 1, host: "github.com"));
        services.AddSingleton<ILinkCardSource>(new FakeLinkCardSource(order: 1, host: "gitlab.example.com"));
    };

    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?> { [WebhookSecrets.GithubKey] = GithubSecret };

    private async Task<LinkCardDto> AddOpenCardAsync(int number, DateTimeOffset stateAsOf)
    {
        using var response = await Api.PostJsonAsync($"{ItemUrl}/link-cards", new { url = WebhookDelivery.GithubUrl(number) });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var card = (await response.Content.ReadFromJsonAsync<LinkCardDto>(CardWire))!;
        using var scope = Host.Services.CreateScope();
        var rows = scope.ServiceProvider.GetRequiredService<LinkCardRows>();
        (await rows.UpdateStateAsync(card.Id, new LinkStateWrite(LinkState.Open, LinkStateSource.Reported, stateAsOf, "Claude Code (claude.ai)"), CancellationToken.None)).ShouldBeTrue();
        return card;
    }

    private async Task<LinkCardDto> ListedAsync(Guid id)
    {
        using var response = await Api.GetAsync($"{ItemUrl}/link-cards");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var links = (await response.Content.ReadFromJsonAsync<LinkCardsDto>(CardWire))!;
        return links.Code.Concat(links.Pages).Single(c => c.Id == id);
    }

    private Task<WebhookResultDto> MergeAsync(int number, DateTimeOffset at)
        => DeliverAsync(WebhookDelivery.Github(GithubSecret, "closed", number, "Rate grid", at, state: "closed", merged: true));

    [Test]
    public async Task List_WhenWebhookStateIsNewer_ShouldShowWebhookState()
    {
        var card = await AddOpenCardAsync(701, CardEarlier);
        var reportedAt = Next();
        await MergeAsync(701, reportedAt);

        var listed = await ListedAsync(card.Id);

        listed.State.ShouldBe(LinkState.Merged);
        listed.StateSource.ShouldBe(LinkStateSource.Webhook);
        listed.StateAsOf.ShouldBe(reportedAt);
        listed.ReportedBy.ShouldBeNull();
        listed.StateAsOfLabel.ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task List_WhenCardStateIsNewer_ShouldKeepCardState()
    {
        var card = await AddOpenCardAsync(702, CardLater);
        await MergeAsync(702, Next());

        var listed = await ListedAsync(card.Id);

        listed.State.ShouldBe(LinkState.Open);
        listed.StateSource.ShouldBe(LinkStateSource.Reported);
        listed.StateAsOf.ShouldBe(CardLater);
        listed.ReportedBy.ShouldBe("Claude Code (claude.ai)");
    }

    [Test]
    public async Task List_WhenNoWebhookState_ShouldBeUnchanged()
    {
        var card = await AddOpenCardAsync(703, CardEarlier);

        var listed = await ListedAsync(card.Id);

        listed.State.ShouldBe(LinkState.Open);
        listed.StateSource.ShouldBe(LinkStateSource.Reported);
        listed.StateAsOf.ShouldBe(CardEarlier);
        listed.ReportedBy.ShouldBe("Claude Code (claude.ai)");
    }

    [Test]
    public async Task List_WhenWebhookStateIsNewer_ShouldAgreeWithAddAndEdit()
    {
        var card = await AddOpenCardAsync(704, CardEarlier);
        await MergeAsync(704, Next());

        using var response = await Api.PatchJsonAsync($"api/link-cards/{card.Id}", new { note = "Checked" });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var edited = (await response.Content.ReadFromJsonAsync<LinkCardDto>(CardWire))!;

        edited.State.ShouldBe(LinkState.Merged);
        edited.StateSource.ShouldBe(LinkStateSource.Webhook);
    }

    // Collects the Ship project's LinksChanged events; Seen completes with the first one for the item, and is cancelled after ten seconds.
    private sealed class LinksChangedWatch : IDisposable
    {
        private readonly List<LinksChanged> _events = [];
        private readonly TaskCompletionSource _first = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenSource _timeout = new(TimeSpan.FromSeconds(10));
        private readonly CancellationTokenRegistration _onTimeout;
        private readonly IDisposable _subscription;

        public LinksChangedWatch(IProjectEvents projectEvents)
        {
            _onTimeout = _timeout.Token.Register(() => _first.TrySetCanceled(_timeout.Token));
            _subscription = projectEvents.Subscribe(SeedIds.Projects.Ship, e =>
            {
                if (e is LinksChanged changed)
                {
                    lock (_events)
                    {
                        _events.Add(changed);
                    }
                    _first.TrySetResult();
                }
                return Task.CompletedTask;
            });
        }

        public Task First => _first.Task;

        public IReadOnlyList<LinksChanged> Events
        {
            get
            {
                lock (_events)
                {
                    return [.. _events];
                }
            }
        }

        public void Dispose()
        {
            _subscription.Dispose();
            _onTimeout.Dispose();
            _timeout.Dispose();
        }
    }

    [Test]
    public async Task Delivery_WhenStateChanges_ShouldRaiseLinksChangedForLinkedItems()
    {
        await AddOpenCardAsync(705, CardEarlier);
        using var watch = new LinksChangedWatch(Host.Services.GetRequiredService<IProjectEvents>());

        await MergeAsync(705, Next());
        await watch.First;

        var raised = watch.Events.ShouldHaveSingleItem();
        raised.WorkItemId.ShouldBe(SeedIds.WorkItems.Ship101);
        raised.ProjectId.ShouldBe(SeedIds.Projects.Ship);
    }

    [Test]
    public async Task Delivery_WhenOlderThanRecorded_ShouldRaiseNothing()
    {
        await AddOpenCardAsync(706, CardEarlier);
        await MergeAsync(706, Next());
        var older = Next().AddHours(-1);
        using var watch = new LinksChangedWatch(Host.Services.GetRequiredService<IProjectEvents>());

        // The delivery is answered after its handler ran, so nothing is still on its way.
        await MergeAsync(706, older);

        watch.Events.ShouldBeEmpty();
    }
}
