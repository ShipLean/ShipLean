using System.Reflection;
using System.Text.RegularExpressions;

namespace ShipLean.UnitTest.Architecture;

// A feature is the first namespace segment after "ShipLean.Application.Features." or "ShipLean.WebApi.Features.".
public static partial class FeatureScan
{
    public static string? FeatureOf(Type type)
    {
        var match = FeatureNamespace().Match(type.Namespace ?? string.Empty);
        return match.Success ? match.Groups[1].Value : null;
    }

    // "ShipLean.Application.Features.A.Thing -> ShipLean.Application.Features.B.Other" for every reference
    // from a type in one feature to a type in a different feature.
    public static IReadOnlyList<string> CrossFeatureReferences(IEnumerable<Assembly> assemblies)
        => assemblies
            .SelectMany(a => a.GetTypes())
            .Select(type => (Type: type, Feature: FeatureOf(type)))
            .Where(t => t.Feature != null)
            .SelectMany(t => TypeReferences.Of(t.Type)
                .Where(referenced => FeatureOf(referenced) is { } other && other != t.Feature)
                .Select(referenced => $"{t.Type.FullName} -> {referenced.FullName}"))
            .Order(StringComparer.Ordinal)
            .ToList();

    [GeneratedRegex(@"^ShipLean\.(?:Application|WebApi)\.Features\.([^.]+)")]
    private static partial Regex FeatureNamespace();
}
