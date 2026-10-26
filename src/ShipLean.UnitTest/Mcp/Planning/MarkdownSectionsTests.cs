using ShipLean.Application.Features.Mcp.Planning.Markers;

namespace ShipLean.UnitTest.Mcp.Planning;

[TestFixture]
public class MarkdownSectionsTests
{
    private const string Description = """
        # Feature editor

        Intro.

        ## Contracts

        **C1 — Fields.** The editor's fields.

        ### C1 detail

        ```sh
        # not a heading
        ## Neither is this
        ```

        More on C1.

        ## Goal ##

        Ship it.

        # Appendix

        Last.
        """;

    [Test]
    public void MarkdownSections_Extract_ShouldStopAtSameLevelHeading()
    {
        var contracts = MarkdownSections.Extract(Description, PlanHeadings.Contracts);

        contracts.ShouldBe("""
            **C1 — Fields.** The editor's fields.

            ### C1 detail

            ```sh
            # not a heading
            ## Neither is this
            ```

            More on C1.
            """.ReplaceLineEndings("\n"));
        MarkdownSections.Extract(Description, "goal").ShouldBe("Ship it.");
        MarkdownSections.Extract(Description, "C1 detail").ShouldStartWith("```sh");
        MarkdownSections.Extract(Description, "Appendix").ShouldBe("Last.");
        MarkdownSections.Extract(Description.ReplaceLineEndings("\r\n"), "Goal").ShouldBe("Ship it.");
    }

    [Test]
    public void Extract_WhenHeadingHasNothingUnderIt_ShouldBeEmpty()
    {
        MarkdownSections.Extract("## Contracts\n\n## Goal\nShip it.", PlanHeadings.Contracts).ShouldBe(string.Empty);
    }

    [Test]
    public void MarkdownSections_Missing_ShouldBeNull()
    {
        MarkdownSections.Extract(Description, PlanHeadings.Lessons).ShouldBeNull();
        MarkdownSections.Extract("```\n## Contracts\n```\n", PlanHeadings.Contracts).ShouldBeNull();
        MarkdownSections.Extract("Contracts\n=========\n", PlanHeadings.Contracts).ShouldBeNull();
        MarkdownSections.Extract("##Contracts", PlanHeadings.Contracts).ShouldBeNull();
        MarkdownSections.Extract(string.Empty, PlanHeadings.Contracts).ShouldBeNull();
        MarkdownSections.Extract(null, PlanHeadings.Contracts).ShouldBeNull();
    }
}
