using System.Text.RegularExpressions;
using ShipLean.Contracts.Usage;

namespace ShipLean.UnitTest.Usage;

// The drift guard: ScreenIds.All is the server's copy of the client's route ids.
[TestFixture]
public class ScreenIdsTests
{
    [Test]
    public void ScreenIds_ShouldMatchClientRouteIds()
    {
        var routes = File.ReadAllText(Path.Combine(RepoRoot(), "src", "ShipLean.Client", "src", "routes.ts"));
        var start = routes.IndexOf("RouteDef<I> } = {", StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0);
        var body = routes[start..];
        var ids = Regex.Matches(body[..body.IndexOf("\n};", StringComparison.Ordinal)], @"^  (\w+):", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value)
            .ToList();

        ids.ShouldNotBeEmpty();
        ids.ShouldBe(ScreenIds.All, ignoreOrder: true);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "ShipLean.Client", "src", "routes.ts")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new DirectoryNotFoundException("The repository root was not found above the test output.");
    }
}
