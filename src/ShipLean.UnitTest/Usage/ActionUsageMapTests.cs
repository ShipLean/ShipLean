using System.Reflection;
using Microsoft.AspNetCore.Mvc.Routing;
using ShipLean.Contracts.Usage;
using ShipLean.WebApi.Features.Usage;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.UnitTest.Usage;

[TestFixture]
public class ActionUsageMapTests
{
    private static IReadOnlyList<string> Actions()
        => [.. typeof(ApiControllerBase).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(ApiControllerBase)))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
                .Select(m => $"{t.Name[..^"Controller".Length]}.{m.Name}"))
            .Distinct()
            .Order(StringComparer.Ordinal)];

    [Test]
    public void EveryAction_ShouldBeMapped()
    {
        var unmapped = Actions().Where(a => !ActionUsageMap.All.ContainsKey(a)).ToList();

        unmapped.ShouldBeEmpty("Add each of these to ActionUsageMap (Track, SelfEmitted or Untracked with a reason).");
    }

    [Test]
    public void EveryName_ShouldBeInCatalogue()
    {
        var unknown = ActionUsageMap.All.Where(e => e.Value.Kind == ActionUsageKind.Track && !UsageEventNames.All.Contains(e.Value.EventName ?? string.Empty)).Select(e => e.Key).ToList();

        unknown.ShouldBeEmpty();
    }

    [Test]
    public void EveryUntracked_ShouldHaveReason()
    {
        var blank = ActionUsageMap.All.Where(e => e.Value.Kind == ActionUsageKind.Untracked && string.IsNullOrWhiteSpace(e.Value.Reason)).Select(e => e.Key).ToList();

        blank.ShouldBeEmpty();
    }

    [Test]
    public void NoStaleKeys()
    {
        var actions = Actions().ToHashSet(StringComparer.Ordinal);

        var stale = ActionUsageMap.All.Keys.Where(k => !actions.Contains(k)).ToList();

        stale.ShouldBeEmpty();
    }
}
