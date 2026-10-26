using System.Reflection;
using ShipLean.Application;
using ShipLean.Contracts;
using ShipLean.Core;
using ShipLean.TestSupport;
using ShipLean.WebApi;

namespace ShipLean.UnitTest.Architecture;

[TestFixture]
public class ArchitectureTests
{
    private static readonly Assembly[] Production =
    [
        typeof(DisplayDate).Assembly,
        typeof(ICurrentUser).Assembly,
        ApplicationMarker.Assembly,
        typeof(ApiMarker).Assembly,
    ];

    private static readonly string[] ProductionProjects =
    [
        "ShipLean.Core/ShipLean.Core.csproj",
        "ShipLean.Contracts/ShipLean.Contracts.csproj",
        "ShipLean.Application/ShipLean.Application.csproj",
        "ShipLean.WebApi/ShipLean.WebApi.csproj",
    ];

    [Test]
    public void Features_ShouldNotReferenceOtherFeatures()
    {
        FeatureScan.CrossFeatureReferences(Production).ShouldBeEmpty();
    }

    [Test]
    public void Production_ShouldNotReferenceTestSupport()
    {
        Production.SelectMany(a => AssemblyScan.ReferencesTo(a, "ShipLean.TestSupport")).ShouldBeEmpty();
        ProductionProjectFiles().SelectMany(f => AssemblyScan.ProjectFileReferences(f, "ShipLean.TestSupport")).ShouldBeEmpty();
    }

    [Test]
    public void Production_ShouldNotReferenceEfCore()
    {
        Production.SelectMany(a => AssemblyScan.ReferencesTo(a, "Microsoft.EntityFrameworkCore")).ShouldBeEmpty();
        ProductionProjectFiles().SelectMany(f => AssemblyScan.ProjectFileReferences(f, "EntityFrameworkCore")).ShouldBeEmpty();
    }

    // The checks above are only worth having if they can fail. These run the same scans over the test assembly,
    // which holds deliberate violations and does reference TestSupport.
    [Test]
    public void FeatureScan_WhenFeatureUsesAnotherFeature_ShouldReportEachKindOfReference()
    {
        var violations = FeatureScan.CrossFeatureReferences([typeof(ArchitectureTests).Assembly]);

        violations.ShouldBe(
        [
            "ShipLean.Application.Features.Yak.YakByCall -> ShipLean.Application.Features.Zebra.ZebraHelper",
            "ShipLean.Application.Features.Yak.YakByField -> ShipLean.Application.Features.Zebra.ZebraHelper",
            "ShipLean.Application.Features.Yak.YakByGeneric -> ShipLean.Application.Features.Zebra.ZebraHelper",
        ]);
    }

    [Test]
    public void AssemblyScan_WhenAssemblyUsesTestSupport_ShouldReportIt()
    {
        AssemblyScan.ReferencesTo(typeof(ArchitectureTests).Assembly, "ShipLean.TestSupport").ShouldBe(["ShipLean.TestSupport"]);
        AssemblyScan.ProjectFileReferences(
            Path.Combine(RepoPaths.Root, "src", "ShipLean.UnitTest", "ShipLean.UnitTest.csproj"), "ShipLean.TestSupport").ShouldNotBeEmpty();
    }

    [Test]
    public void AssemblyScan_WhenReferenceIsEfCore_ShouldReportIt()
    {
        AssemblyScan.WithPrefix(["System.Linq", "Microsoft.EntityFrameworkCore.Relational"], "Microsoft.EntityFrameworkCore")
            .ShouldBe(["Microsoft.EntityFrameworkCore.Relational"]);
        AssemblyScan.Mentioning(
            "<PackageReference Include=\"Microsoft.EntityFrameworkCore\" Version=\"10.0.0\" />", "EntityFrameworkCore")
            .ShouldBe(["EntityFrameworkCore"]);
    }

    private static IEnumerable<string> ProductionProjectFiles()
        => ProductionProjects.Select(p => Path.Combine(RepoPaths.Root, "src", p));
}
