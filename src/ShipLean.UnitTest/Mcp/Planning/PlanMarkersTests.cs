using ShipLean.Application.Features.Mcp.Planning.Markers;

namespace ShipLean.UnitTest.Mcp.Planning;

[TestFixture]
public class PlanMarkersTests
{
    [Test]
    public void PlanMarkers_EachFormat_ShouldBeReadBack()
    {
        var report = PlanMarkers.Report("done: F1 shipped");
        var review = PlanMarkers.Review("round 1: two findings");
        var retro = PlanMarkers.Retro("wave 1 went well");

        report.ShouldBe("Report · done: F1 shipped");
        (PlanMarkers.IsReport(report), PlanMarkers.IsReview(report), PlanMarkers.IsRetro(report)).ShouldBe((true, false, false));
        (PlanMarkers.IsReport(review), PlanMarkers.IsReview(review), PlanMarkers.IsRetro(review)).ShouldBe((false, true, false));
        (PlanMarkers.IsReport(retro), PlanMarkers.IsReview(retro), PlanMarkers.IsRetro(retro)).ShouldBe((false, false, true));

        var commit = PlanMarkers.Commit("3F2A9C1D8E7B6A5F4E3D2C1B0A9F8E7D6C5B4A39", "Feature fields: goal and status");
        commit.ShouldBe("commit 3f2a9c1 Feature fields: goal and status");
        PlanMarkers.IsCommit(commit).ShouldBeTrue();
        PlanMarkers.TryReadCommit(commit, out var sha, out var subject).ShouldBeTrue();
        (sha, subject).ShouldBe(("3f2a9c1", "Feature fields: goal and status"));

        var pr = PlanMarkers.PullRequest("https://example.test/org/repo/pull/12");
        pr.ShouldBe("PR https://example.test/org/repo/pull/12");
        PlanMarkers.IsPullRequest(pr).ShouldBeTrue();
        PlanMarkers.IsCommit(pr).ShouldBeFalse();
        PlanMarkers.IsPullRequest(commit).ShouldBeFalse();
    }

    [Test]
    public void Commit_WhenNoSubject_ShouldReadBackAnEmptySubject()
    {
        var commit = PlanMarkers.Commit("abcdef0", " ");

        commit.ShouldBe("commit abcdef0");
        PlanMarkers.TryReadCommit(commit, out var sha, out var subject).ShouldBeTrue();
        (sha, subject).ShouldBe(("abcdef0", string.Empty));
    }

    [Test]
    public void Commit_WhenSubjectHasLineBreaks_ShouldBeOneLine()
    {
        var commit = PlanMarkers.Commit("abcdef0", "\r\nFix the editor\r\n\r\nLonger body here\n");

        commit.ShouldBe("commit abcdef0 Fix the editor Longer body here");
        PlanMarkers.TryReadCommit(commit, out _, out var subject).ShouldBeTrue();
        subject.ShouldBe("Fix the editor Longer body here");
    }

    [TestCase("abcdef0", true)]
    [TestCase("ABCDEF0123456789", true)]
    [TestCase("abcdef", false)]
    [TestCase("abcdefg", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public void IsCommitSha_ShouldMatchWhatCommitAccepts(string? sha, bool expected)
    {
        PlanMarkers.IsCommitSha(sha).ShouldBe(expected);
    }

    [TestCase("abc12")]
    [TestCase("not-a-sha")]
    public void Commit_WhenShaIsNotSevenHex_ShouldThrow(string sha)
    {
        Should.Throw<ArgumentException>(() => PlanMarkers.Commit(sha, "subject"));
    }

    [TestCase("Commit abcdef0 subject")]
    [TestCase("commit abcdef subject")]
    [TestCase("commit ABCDEF0 subject")]
    [TestCase("a link")]
    public void TryReadCommit_WhenNotWrittenByCommit_ShouldBeFalse(string title)
    {
        PlanMarkers.TryReadCommit(title, out _, out _).ShouldBeFalse();
    }

    [TestCase("retro-2026-10-25-feature-editor.md", true)]
    [TestCase("retro-2026-10-25-wave1.md", true)]
    [TestCase("retro-2026-02-30-feature-editor.md", false)]
    [TestCase("retro-2026-10-25.md", false)]
    [TestCase("retro-2026-10-25-Feature_Editor.md", true)]
    [TestCase("RETRO-2026-10-25-wave 1.MD", true)]
    [TestCase("retro-Feature_Editor.md", false)]
    [TestCase("retro-2026-10-25-a/b.md", false)]
    [TestCase("retro-2026-10-25-a\\b.md", false)]
    [TestCase("retro-2026-10-25-.md", false)]
    [TestCase("retro-26-10-25-topic.md", false)]
    [TestCase("retro-2026-10-25-topic.txt", false)]
    [TestCase("my-retro-2026-10-25-topic.md", false)]
    [TestCase("PLAN.md", false)]
    [TestCase("common-brief.md", false)]
    public void PlanFiles_IsRetro_ShouldMatchOnlyDatedRetroNames(string name, bool expected)
    {
        PlanFiles.IsRetro(name).ShouldBe(expected);
    }
}
