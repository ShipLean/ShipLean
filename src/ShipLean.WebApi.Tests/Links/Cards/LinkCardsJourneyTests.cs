using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Links;
using ShipLean.Contracts.Persisted;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using Shouldly;

namespace ShipLean.WebApi.Tests.Links.Cards;

// The add, archive and error journeys of the link endpoints, one scenario per test, through the real routes on SHIP-101.
[TestFixture]
public class LinkCardsJourneyTests : ApiFixture
{
    private static readonly string ItemUrl = $"api/work-items/{SeedIds.WorkItems.Ship101}";

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    private static string NewAddress() => $"https://journey.example/{Guid.NewGuid():N}";

    private async Task<LinkCardDto> AddAsync(string url)
    {
        using var response = await Api.PostJsonAsync($"{ItemUrl}/link-cards", new { url });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<LinkCardDto>(Wire))!;
    }

    private async Task<LinkCardsDto> ListAsync()
    {
        using var response = await Api.GetAsync($"{ItemUrl}/link-cards");
        return (await response.Content.ReadFromJsonAsync<LinkCardsDto>(Wire))!;
    }

    [Test]
    public async Task Add_WhenDuplicateUrl_ShouldReturnLinkDuplicate()
    {
        var url = NewAddress();
        await AddAsync(url);

        using var response = await Api.PostJsonAsync($"{ItemUrl}/link-cards", new { url });

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict);
        error.Code.ShouldBe(ApiErrorCodes.LinkDuplicate);
        error.Field.ShouldBe("url");
    }

    [Test]
    public async Task Add_WhenInvalidUrl_ShouldReturnLinkInvalidUrl()
    {
        using var response = await Api.PostJsonAsync($"{ItemUrl}/link-cards", new { url = "ftp://journey.example/spec" });

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.LinkInvalidUrl);
        error.Field.ShouldBe("url");
    }

    [Test]
    public async Task AddWorkItemLink_WhenSelf_ShouldReturnValidation()
    {
        using var response = await Api.PostJsonAsync($"{ItemUrl}/work-item-links", new { targetKey = "SHIP-101", type = "related" });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task AddWorkItemLink_WhenOtherProject_ShouldReturnNotFound()
    {
        await using var connection = await OpenDatabaseAsync();
        var key = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE project_id <> @ship LIMIT 1", new { ship = SeedIds.Projects.Ship });

        using var response = await Api.PostJsonAsync($"{ItemUrl}/work-item-links", new { targetKey = key, type = "related" });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Add_ShouldPublishLinksChanged()
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
    public async Task Archive_ShouldHideCardAndKeepRow()
    {
        var added = await AddAsync(NewAddress());

        using var response = await Api.PostJsonAsync($"api/link-cards/{added.Id}/archive");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var links = await ListAsync();
        links.Code.Concat(links.Pages).ShouldNotContain(c => c.Id == added.Id);
        await using var connection = await OpenDatabaseAsync();
        (await connection.QuerySingleAsync<bool>("SELECT archived_at IS NOT NULL FROM work_item_link WHERE id = @id", new { id = added.Id })).ShouldBeTrue();
    }

    [Test]
    public async Task Add_WhenWorkItemKey_ShouldCreateRelationRow()
    {
        using var response = await Api.PostJsonAsync($"{ItemUrl}/work-item-links", new { targetKey = "SHIP-91", type = "related" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var row = (await response.Content.ReadFromJsonAsync<WorkItemLinkRowDto>(Wire))!;
        row.Key.ShouldBe("SHIP-91");
        await using var connection = await OpenDatabaseAsync();
        (await connection.QuerySingleAsync<int>(
            "SELECT count(*)::int FROM work_item_link WHERE id = @id AND source_id = @source AND target_id = @target AND link_type = 'related' AND archived_at IS NULL",
            new { id = row.Id, source = SeedIds.WorkItems.Ship101, target = row.WorkItemId })).ShouldBe(1);
    }

    [Test]
    public async Task AddWorkItemLink_WhenDuplicate_ShouldReturnLinkDuplicate()
    {
        var body = new { targetKey = "SHIP-92", type = "blockedBy" };
        using var first = await Api.PostJsonAsync($"{ItemUrl}/work-item-links", body);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var second = await Api.PostJsonAsync($"{ItemUrl}/work-item-links", body);

        var error = await ApiClient.ReadApiErrorAsync(second, HttpStatusCode.Conflict);
        error.Code.ShouldBe(ApiErrorCodes.LinkDuplicate);
        error.Field.ShouldBe("targetKey");
    }
}
