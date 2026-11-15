using System.Security.Cryptography;
using System.Text;
using ShipLean.Application.Features.TaskChanges.Reads.Page;

namespace ShipLean.UnitTest.TaskChanges;

[TestFixture]
public class GitDiffSplitterTests
{
    private const string Modified =
        "diff --git a/src/a.ts b/src/a.ts\nindex 111..222 100644\n--- a/src/a.ts\n+++ b/src/a.ts\n@@ -1,2 +1,3 @@\n one\n-two\n+three\n+four\n";

    private const string Added =
        "diff --git a/src/new.ts b/src/new.ts\nnew file mode 100644\nindex 0000000..222\n--- /dev/null\n+++ b/src/new.ts\n@@ -0,0 +1,2 @@\n+a\n+b\n";

    private const string Deleted =
        "diff --git a/src/gone.ts b/src/gone.ts\ndeleted file mode 100644\nindex 111..0000000\n--- a/src/gone.ts\n+++ /dev/null\n@@ -1,3 +0,0 @@\n-a\n-b\n-c\n";

    private const string Renamed =
        "diff --git a/src/old.ts b/src/new.ts\nsimilarity index 80%\nrename from src/old.ts\nrename to src/new.ts\nindex 111..222 100644\n--- a/src/old.ts\n+++ b/src/new.ts\n@@ -1 +1 @@\n-x\n+y\n";

    private const string RenamedUntouched =
        "diff --git a/src/old.ts b/src/moved.ts\nsimilarity index 100%\nrename from src/old.ts\nrename to src/moved.ts\n";

    private const string Binary =
        "diff --git a/img/logo.png b/img/logo.png\nnew file mode 100644\nindex 0000000..222\nBinary files /dev/null and b/img/logo.png differ\n";

    private const string Spaces =
        "diff --git a/docs/my notes.md b/docs/my notes.md\nindex 111..222 100644\n--- a/docs/my notes.md\t\n+++ b/docs/my notes.md\t\n@@ -1 +1 @@\n-a\n+b\n";

    [Test]
    public void Split_WhenModified_ShouldReturnPathCountsAndHunks()
    {
        var file = GitDiffSplitter.Split(Modified).ShouldHaveSingleItem();

        file.Path.ShouldBe("src/a.ts");
        file.OldPath.ShouldBeNull();
        file.Status.ShouldBe("modified");
        (file.Additions, file.Deletions).ShouldBe((2, 1));
        file.Patch.ShouldBe("@@ -1,2 +1,3 @@\n one\n-two\n+three\n+four\n");
        file.OpenComments.ShouldBe(0);
    }

    [Test]
    public void Split_WhenFileText_ShouldHashItsPatchWithSha1()
    {
        var file = GitDiffSplitter.Split(Modified).Single();

        file.PatchId.ShouldBe(Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(file.Patch))));
        file.PatchId.Length.ShouldBe(40);
    }

    [Test]
    public void Split_WhenNewFile_ShouldBeAdded()
    {
        var file = GitDiffSplitter.Split(Added).Single();

        (file.Path, file.OldPath, file.Status, file.Additions, file.Deletions).ShouldBe(("src/new.ts", null, "added", 2, 0));
    }

    [Test]
    public void Split_WhenDeletedFile_ShouldBeDeletedWithOldName()
    {
        var file = GitDiffSplitter.Split(Deleted).Single();

        (file.Path, file.OldPath, file.Status, file.Additions, file.Deletions).ShouldBe(("src/gone.ts", null, "deleted", 0, 3));
    }

    [Test]
    public void Split_WhenRenamedWithEdit_ShouldKeepBothPaths()
    {
        var file = GitDiffSplitter.Split(Renamed).Single();

        (file.Path, file.OldPath, file.Status, file.Additions, file.Deletions).ShouldBe(("src/new.ts", "src/old.ts", "renamed", 1, 1));
        file.Patch.ShouldStartWith("@@ -1 +1 @@");
    }

    [Test]
    public void Split_WhenRenamedWithoutEdit_ShouldHaveEmptyPatch()
    {
        var file = GitDiffSplitter.Split(RenamedUntouched).Single();

        (file.Path, file.OldPath, file.Status, file.Patch).ShouldBe(("src/moved.ts", "src/old.ts", "renamed", string.Empty));
    }

    [Test]
    public void Split_WhenBinary_ShouldHaveEmptyPatchAndNoCounts()
    {
        var file = GitDiffSplitter.Split(Binary).Single();

        (file.Path, file.Status, file.Patch, file.Additions, file.Deletions).ShouldBe(("img/logo.png", "added", string.Empty, 0, 0));
    }

    [Test]
    public void Split_WhenPathHasSpaces_ShouldKeepThem()
    {
        var file = GitDiffSplitter.Split(Spaces).Single();

        file.Path.ShouldBe("docs/my notes.md");
        file.Patch.ShouldBe("@@ -1 +1 @@\n-a\n+b\n");
    }

    [Test]
    public void Split_WhenSeveralFiles_ShouldReturnEachInOrderWithOwnPatch()
    {
        var files = GitDiffSplitter.Split(Modified + Added + Binary + Spaces);

        files.Select(f => f.Path).ShouldBe(["src/a.ts", "src/new.ts", "img/logo.png", "docs/my notes.md"]);
        files[1].Patch.ShouldBe("@@ -0,0 +1,2 @@\n+a\n+b\n");
    }

    [Test]
    public void Split_WhenEmpty_ShouldReturnNoFiles()
    {
        GitDiffSplitter.Split(string.Empty).ShouldBeEmpty();
    }
}
