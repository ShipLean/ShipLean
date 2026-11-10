using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Retros;
using ShipLean.TestSupport.Screens;

namespace ShipLean.WebApi.Tests.Retros.Support;

// POST and DELETE api/retros/points/{id}/support: a person's +1 on a retro point, taken back by stamping withdrawn_at.
public sealed class RetroSupportApiTests : ApiFixture
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    [Test]
    public async Task Support_WhenMember_ShouldAddTheCallerAndReturn204()
    {
        var api = Api.ActingAs(TestUser.Tom);
        var url = $"api/retros/points/{SeedIds.Screens.RetroPoint}/support";

        using var response = await api.PostJsonAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var point = await SprintPointAsync(api, SeedIds.Sprints.Sprint24, SeedIds.Screens.RetroPoint);
        point.Supporters.Select(user => user.Id).ShouldBe([SeedIds.Users.Priya, SeedIds.Users.Tom]);
        point.SupportedByMe.ShouldBeTrue();
    }

    [Test]
    public async Task Support_WhenPostedTwice_ShouldKeepOneLiveRow()
    {
        var point = await PointAsync();
        var api = Api.ActingAs(TestUser.Tom);

        using var first = await api.PostJsonAsync(Url(point));
        using var second = await api.PostJsonAsync(Url(point));

        (first.StatusCode, second.StatusCode).ShouldBe((HttpStatusCode.NoContent, HttpStatusCode.NoContent));
        var rows = await RowsAsync(point);
        rows.Count(row => row.WithdrawnAt is null).ShouldBe(1);
        rows.Count.ShouldBe(1);
    }

    [Test]
    public async Task Withdraw_WhenSupported_ShouldStampWithdrawnAndDropTheSupporter()
    {
        var point = await PointAsync();
        var api = Api.ActingAs(TestUser.Tom);
        using var support = await api.PostJsonAsync(Url(point));

        using var response = await api.DeleteAsync(Url(point));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var row = (await RowsAsync(point)).ShouldHaveSingleItem();
        row.WithdrawnAt.ShouldNotBeNull();
        row.UserId.ShouldBe(SeedIds.Users.Tom);
        var after = await CardPointAsync(point, SeedIds.Users.Tom);
        after.Supporters.ShouldBeEmpty();
        after.SupportedByMe.ShouldBeFalse();
    }

    [Test]
    public async Task Withdraw_WhenNotSupported_ShouldReturn204AndChangeNothing()
    {
        var point = await PointAsync();
        await using (var connection = await OpenDatabaseAsync())
        {
            await new RetroSupportBuilder(point, SeedIds.Users.Priya).InsertAsync(connection);
        }

        using var response = await Api.ActingAs(TestUser.Tom).DeleteAsync(Url(point));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var row = (await RowsAsync(point)).ShouldHaveSingleItem();
        (row.UserId, row.WithdrawnAt).ShouldBe((SeedIds.Users.Priya, (DateTimeOffset?)null));
    }

    [Test]
    public async Task Support_WhenWithdrawnThenSupportedAgain_ShouldAddANewRow()
    {
        var point = await PointAsync();
        var api = Api.ActingAs(TestUser.Tom);
        using var first = await api.PostJsonAsync(Url(point));
        using var withdraw = await api.DeleteAsync(Url(point));

        using var again = await api.PostJsonAsync(Url(point));

        again.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var rows = await RowsAsync(point);
        rows.Count.ShouldBe(2);
        rows.Count(row => row.WithdrawnAt is null).ShouldBe(1);
    }

    [Test]
    public async Task Support_WhenNotMember_ShouldReturnNotFound()
    {
        var point = await PointAsync();

        using var response = await Api.ActingAs(TestUser.Kai).PostJsonAsync(Url(point));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        (await RowsAsync(point)).ShouldBeEmpty();
    }

    [Test]
    public async Task Support_WhenPointArchived_ShouldReturnNotFound()
    {
        var point = await PointAsync(archived: true);

        using var response = await Api.ActingAs(TestUser.Tom).PostJsonAsync(Url(point));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        (await RowsAsync(point)).ShouldBeEmpty();
    }

    [Test]
    public async Task Support_WhenPointUnknown_ShouldReturnNotFound()
    {
        using var response = await Api.ActingAs(TestUser.Tom).PostJsonAsync(Url(Guid.NewGuid()));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Support_WhenPointIsAnAgents_ShouldBeAllowed()
    {
        var point = await PointAsync(agentName: "Claude Code");

        using var response = await Api.ActingAs(TestUser.Tom).PostJsonAsync(Url(point));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var row = (await RowsAsync(point)).ShouldHaveSingleItem();
        row.UserId.ShouldBe(SeedIds.Users.Tom);
    }

    private static string Url(Guid point) => $"api/retros/points/{point}/support";

    private async Task<Guid> PointAsync(bool archived = false, string agentName = "Test agent")
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: SeedIds.WorkItems.Ship90, AgentName: $"{agentName} {Guid.NewGuid():N}"));
        return await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "Agree with me.", Archived: archived));
    }

    private async Task<IReadOnlyList<ScreensRows.Support>> RowsAsync(Guid point)
    {
        await using var connection = await OpenDatabaseAsync();
        return await ScreensRows.SupportAsync(connection, point);
    }

    private async Task<RetroPointDto> CardPointAsync(Guid point, Guid actor)
    {
        await using var connection = await OpenDatabaseAsync();
        var retro = await connection.QuerySingleAsync<Guid>("SELECT retrospective_id FROM retro_point WHERE id = @point", new { point });
        Host.ActAs(actor);
        try
        {
            var cards = await Host.Services.GetRequiredService<IRetroCards>().LoadAsync([retro], CancellationToken.None);
            return cards.Single().Points.Single(p => p.Id == point);
        }
        finally
        {
            Host.ActAs(null);
        }
    }

    private static async Task<RetroPointDto> SprintPointAsync(ApiClient api, Guid sprint, Guid point)
    {
        using var response = await api.GetAsync($"api/sprints/{sprint}/retro");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var retro = JsonSerializer.Deserialize<SprintRetroDto>(await response.Content.ReadAsStringAsync(), Wire)!;
        return retro.Cards.SelectMany(card => card.Points).Single(p => p.Id == point);
    }
}
