using System.Text.RegularExpressions;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Features.Mcp.Planning;

namespace ShipLean.UnitTest.Mcp.Planning;

[TestFixture]
public partial class PlanGuideTests
{
    [GeneratedRegex(@"\{\{[a-z_]+\}\}")]
    private static partial Regex Placeholder();

    private static IReadOnlyList<string> PlaceholdersIn(PlanGuidePart part)
        => Placeholder().Matches(PlanGuide.Read(part)).Select(m => m.Value).ToList();

    [Test]
    public void GuideTemplates_ShouldCarryExactlyTheirPlaceholdersInOrder()
    {
        PlaceholdersIn(PlanGuidePart.Brief).ShouldBe(PlanPlaceholders.Brief);
        PlaceholdersIn(PlanGuidePart.Lead).ShouldBe(PlanPlaceholders.Lead);

        foreach (var part in Enum.GetValues<PlanGuidePart>().Except([PlanGuidePart.Brief, PlanGuidePart.Lead]))
        {
            PlaceholdersIn(part).ShouldBeEmpty($"{part} carries no placeholder");
        }

        PlanGuide.Read(PlanGuidePart.Retro).ReplaceLineEndings("\n").ShouldContain("\n## What to change next time\n");
    }

    [Test]
    public void PlanGuide_EveryPart_ShouldReadItsEmbeddedFile()
    {
        foreach (var part in Enum.GetValues<PlanGuidePart>())
        {
            PlanGuide.Read(part).ShouldNotBeNullOrWhiteSpace($"{part}");
            PlanGuidePart.Parse(part.WireName()).ShouldBe(part);
        }

        PlanGuide.Read(PlanGuidePart.Guide).ShouldBeSameAs(PlanGuide.Read(PlanGuidePart.Guide));
        Enum.GetValues<PlanGuidePart>().Select(p => p.WireName())
            .ShouldBe(["work", "guide", "brief", "report", "plan_reviewer", "code_reviewer", "fix", "lead", "retro"]);
    }

    [Test]
    public void PlanGuidePart_Unknown_ShouldListTheParts()
    {
        var error = Should.Throw<ApiException>(() => PlanGuidePart.Parse("nope"));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("part");
        error.Message.ShouldContain("work, guide, brief, report, plan_reviewer, code_reviewer, fix, lead, retro");
    }
}
