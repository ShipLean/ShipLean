using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;

namespace ShipLean.WebApi.Tests.Retros.Reads;

// Who agreed with a retro point, as the cards show it: live +1s oldest first, and whether the caller is one of them.
public sealed class RetroCardsSupportTests : ApiFixture
{
    private static readonly Guid Project = SeedIds.Projects.Ship;

    private IRetroCards Cards => Host.Services.GetRequiredService<IRetroCards>();

    [Test]
    public async Task Load_WhenPointSupported_ShouldListSupportersOldestFirst()
    {
        await using var connection = await OpenDatabaseAsync();
        var (retro, point) = await PointAsync(connection);
        await new RetroSupportBuilder(point, SeedIds.Users.Priya).WithCreatedAt(TestDates.Today.AddDays(-1)).InsertAsync(connection);
        await new RetroSupportBuilder(point, SeedIds.Users.Tom).WithCreatedAt(TestDates.Today.AddDays(-3)).InsertAsync(connection);
        await new RetroSupportBuilder(point, SeedIds.Users.Mei).WithCreatedAt(TestDates.Today.AddDays(-2)).WithWithdrawnAt(TestDates.Today.AddDays(-1)).InsertAsync(connection);

        var supported = await LoadPointAsync(retro, SeedIds.Users.Joel);

        supported.Supporters.Select(user => user.Id).ShouldBe([SeedIds.Users.Tom, SeedIds.Users.Priya]);
    }

    [Test]
    public async Task Load_WhenOtherUserSupported_ShouldNotMarkSupportedByMe()
    {
        await using var connection = await OpenDatabaseAsync();
        var (retro, point) = await PointAsync(connection);
        await new RetroSupportBuilder(point, SeedIds.Users.Priya).InsertAsync(connection);
        await new RetroSupportBuilder(point, SeedIds.Users.Joel).WithWithdrawnAt(TestDates.Today).InsertAsync(connection);

        var asJoel = await LoadPointAsync(retro, SeedIds.Users.Joel);
        var asPriya = await LoadPointAsync(retro, SeedIds.Users.Priya);

        asJoel.SupportedByMe.ShouldBeFalse();
        asPriya.SupportedByMe.ShouldBeTrue();
    }

    [Test]
    public async Task Load_WhenSeeded_ShouldShowPriyaAsSupporter()
    {
        var card = (await LoadAsActorAsync(SeedIds.Users.Tom, SeedIds.Screens.Retro)).Single();

        var point = card.Points.Single(p => p.Id == SeedIds.Screens.RetroPoint);
        point.Supporters.Select(user => user.Id).ShouldBe([SeedIds.Users.Priya]);
        point.SupportedByMe.ShouldBeFalse();
    }

    private async Task<RetroPointDto> LoadPointAsync(Guid retro, Guid actor)
        => (await LoadAsActorAsync(actor, retro)).Single().Points.Single();

    private async Task<IReadOnlyList<RetroCardDto>> LoadAsActorAsync(Guid actor, Guid retro)
    {
        Host.ActAs(actor);
        try
        {
            return await Cards.LoadAsync([retro], CancellationToken.None);
        }
        finally
        {
            Host.ActAs(null);
        }
    }

    private static async Task<(Guid Retro, Guid Point)> PointAsync(Npgsql.NpgsqlConnection connection)
    {
        var seed = new RetroSeed(connection, Project);
        var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: SeedIds.WorkItems.Ship90, AgentName: $"agent-{Guid.NewGuid():N}"));
        var point = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "Agreed with."));
        return (retro, point);
    }
}
