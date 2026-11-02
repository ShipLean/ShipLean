using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Retros;

// log_retrospective on a host whose themer is in auto mode. Its own class, because the mode belongs to the host.
[TestFixture]
public class LogRetrospectiveAutoModeTests : McpFixture
{
    protected override bool WithProbeTools => false;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        services.RemoveAll<IRetroThemer>();
        services.AddSingleton<IRetroThemer, AutoThemer>();
    };

    [Test]
    public async Task Log_WhenAutoMode_ShouldIgnoreSuppliedThemeWithWarning()
    {
        Guid story;
        string key;
        await using (var connection = await OpenDatabaseAsync())
        {
            story = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").InsertAsync(connection);
            key = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @story", new { story });
        }

        var bearer = await TokenAsync(TestUser.Joel, ReadWrite);
        var reply = await CallToolAsync(bearer, "log_retrospective", $$"""{"item":"{{key}}","improve":[{"text":"Auto themed","theme":"My own theme"}]}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var result = reply.Structured();
        result.GetProperty("theme_mode").GetString().ShouldBe("auto");
        result.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()).ShouldBe(["Themes are assigned by ShipLean; theme ignored"]);
        AutoThemer.Supplied.ShouldBeNull();
        await using var check = await OpenDatabaseAsync();
        (await check.ExecuteScalarAsync<int>("SELECT count(*)::int FROM retro_theme WHERE lower(name) = 'my own theme'")).ShouldBe(0);
    }

    // A test double: reports auto mode and remembers the theme it was asked to use.
    private sealed class AutoThemer : IRetroThemer
    {
        public static string? Supplied { get; private set; } = "unset";

        public RetroThemeMode Mode => RetroThemeMode.Auto;

        public Task<RetroThemeResult> AssignAsync(RetroThemeRequest request, CancellationToken cancellationToken)
        {
            Supplied = request.SuppliedTheme;
            return Task.FromResult(new RetroThemeResult(null, null, null, null));
        }
    }
}
