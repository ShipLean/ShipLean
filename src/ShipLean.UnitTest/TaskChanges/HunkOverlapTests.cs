using ShipLean.Application.Features.TaskChanges.Comments;
using ShipLean.Contracts.TaskChanges;

namespace ShipLean.UnitTest.TaskChanges;

[TestFixture]
public class HunkOverlapTests
{
    // src/a.ts: old lines 10-14 (5 lines), new lines 20-23 (4 lines).
    private const string Edit =
        """
        diff --git a/src/a.ts b/src/a.ts
        index 111..222 100644
        --- a/src/a.ts
        +++ b/src/a.ts
        @@ -10,5 +20,4 @@ context
         one
        -two
        +three
        """;

    private const string Deleted =
        """
        diff --git a/src/gone.ts b/src/gone.ts
        deleted file mode 100644
        index 111..0000000
        --- a/src/gone.ts
        +++ /dev/null
        @@ -1,3 +0,0 @@
        -a
        -b
        -c
        """;

    private const string Renamed =
        """
        diff --git a/src/old.ts b/src/new.ts
        similarity index 90%
        rename from src/old.ts
        rename to src/new.ts
        @@ -5,2 +6,2 @@
         x
        -y
        +z
        """;

    [TestCase(CodeSide.Old, 10, true)]
    [TestCase(CodeSide.Old, 14, true)]
    [TestCase(CodeSide.Old, 9, false)]
    [TestCase(CodeSide.Old, 15, false)]
    [TestCase(CodeSide.New, 20, true)]
    [TestCase(CodeSide.New, 23, true)]
    [TestCase(CodeSide.New, 19, false)]
    [TestCase(CodeSide.New, 24, false)]
    public void HunkOverlap_WhenLineOnASide_ShouldMatchThatSidesNumbers(CodeSide side, int line, bool expected)
        => HunkOverlap.Touches(Edit, "src/a.ts", side, line, null).ShouldBe(expected);

    [TestCase(CodeSide.Old, 5, 10, true)]
    [TestCase(CodeSide.Old, 1, 9, false)]
    [TestCase(CodeSide.Old, 14, 30, true)]
    [TestCase(CodeSide.Old, 15, 30, false)]
    public void HunkOverlap_WhenRange_ShouldTouchWhenAnyLineIsInTheHunk(CodeSide side, int start, int end, bool expected)
        => HunkOverlap.Touches(Edit, "src/a.ts", side, end, start).ShouldBe(expected);

    [Test]
    public void HunkOverlap_WhenOtherPath_ShouldNotTouch()
        => HunkOverlap.Touches(Edit, "src/b.ts", CodeSide.New, 21, null).ShouldBeFalse();

    [Test]
    public void HunkOverlap_WhenDeletedFile_ShouldTouchOldSideOnly()
    {
        HunkOverlap.Touches(Deleted, "src/gone.ts", CodeSide.Old, 2, null).ShouldBeTrue();
        HunkOverlap.Touches(Deleted, "src/gone.ts", CodeSide.New, 1, null).ShouldBeFalse();
    }

    [Test]
    public void HunkOverlap_WhenRenamed_ShouldMatchOldPathOnOldSideAndNewPathOnNewSide()
    {
        HunkOverlap.Touches(Renamed, "src/old.ts", CodeSide.Old, 6, null).ShouldBeTrue();
        HunkOverlap.Touches(Renamed, "src/old.ts", CodeSide.New, 6, null).ShouldBeFalse();
        HunkOverlap.Touches(Renamed, "src/new.ts", CodeSide.New, 7, null).ShouldBeTrue();
    }

    [Test]
    public void HunkOverlap_WhenHeaderHasNoCount_ShouldCoverOneLine()
    {
        const string single = "diff --git a/f b/f\n--- a/f\n+++ b/f\n@@ -3 +3 @@\n-a\n+b\n";

        HunkOverlap.Touches(single, "f", CodeSide.New, 3, null).ShouldBeTrue();
        HunkOverlap.Touches(single, "f", CodeSide.New, 4, null).ShouldBeFalse();
    }

    [Test]
    public void HunkOverlap_WhenSecondFileHasTheLine_ShouldNotLeakFromFirstFile()
    {
        const string two = "diff --git a/f b/f\n@@ -1,2 +1,2 @@\n x\n-y\n+z\ndiff --git a/g b/g\n@@ -50,2 +50,2 @@\n x\n";

        HunkOverlap.Touches(two, "g", CodeSide.New, 1, null).ShouldBeFalse();
        HunkOverlap.Touches(two, "g", CodeSide.New, 51, null).ShouldBeTrue();
    }
}
