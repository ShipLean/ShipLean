using System.Reflection;
using System.Text.RegularExpressions;
using ShipLean.Contracts.Usage;

namespace ShipLean.UnitTest.Usage;

[TestFixture]
public class UsageEventNamesTests
{
    private static readonly Regex Pattern = new("^[a-z]+(_[a-z]+)*\\.[a-z]+(_[a-z]+)*$", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static string[] Constants()
        => [.. typeof(UsageEventNames).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!)];

    [Test]
    public void Names_WhenChecked_ShouldMatchPatternAndFitUmamisLimit()
    {
        foreach (var name in UsageEventNames.All)
        {
            Pattern.IsMatch(name).ShouldBeTrue(name);
            name.Length.ShouldBeLessThanOrEqualTo(50, name);
        }
    }

    [Test]
    public void Names_WhenChecked_ShouldBeUnique()
    {
        var constants = Constants();

        constants.Distinct().Count().ShouldBe(constants.Length);
    }

    [Test]
    public void All_WhenChecked_ShouldEqualTheConstantsFoundByReflection()
    {
        UsageEventNames.All.SetEquals(Constants()).ShouldBeTrue();
        UsageEventNames.All.Count.ShouldBeGreaterThan(80);
    }

    [Test]
    public void ScreenIds_WhenChecked_ShouldHoldTheClientRoutes()
    {
        ScreenIds.All.ShouldContain("storiesBacklog");
        ScreenIds.All.ShouldContain("login");
    }
}
