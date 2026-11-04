using System.Reflection;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.UnitTest.Demos;

[TestFixture]
public class DemoPageObjectsTests
{
    [Test]
    public void DemoPageObjects_ShouldUseEveryDemoTestId()
    {
        var folder = Path.Combine(RepoPaths.Root, "src", "ShipLean.AcceptanceTests", "Pages", "Demos");
        var source = string.Concat(Directory.GetFiles(folder, "*.cs").Select(File.ReadAllText));
        var unused = typeof(DemosTestIds)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => field.Name)
            .Where(name => !source.Contains($"DemosTestIds.{name}", StringComparison.Ordinal))
            .ToList();

        unused.ShouldBeEmpty();
    }
}
