using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.WorkItem.Side;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.AcceptanceTests.Lanes.Touch;

// The phone route sweep (K9): every signed-in SPA route not exempt by decision, opened at 390×844 on the seed, checked
// against the touch rules. Run one route with --filter "FullyQualifiedName~TouchRouteSweepTests&Name~<screenId>".
[TestFixture]
public partial class TouchRouteSweepTests : SeededAcceptanceTest
{
    private const string AttachmentName = "touch-sweep.md";

    private Guid _markdownAttachment;

    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    public static IEnumerable<string> SweptRoutes() =>
        TouchRoutes.All
            .Where(r => !r.Value.SignedOut && !TouchExemptRoutes.All.ContainsKey(r.Key))
            .Select(r => r.Key)
            .Order(StringComparer.Ordinal);

    // workItemAttachment opens a Markdown attachment that renders, uploaded the way a teammate would.
    [OneTimeSetUp]
    public async Task UploadMarkdownAttachmentAsync()
    {
        var markdown = "# Saved card design\n\nTokens stay in the vault. See [the scope](https://example.com/scope).\n";
        await WorkItemAttachments.UploadAsTomAsync(BaseUrl, SeedIds.WorkItems.Ship101, AttachmentName, Encoding.UTF8.GetBytes(markdown));
        await using var connection = new NpgsqlConnection(ConnectionString);
        _markdownAttachment = await connection.QuerySingleAsync<Guid>(
            "SELECT id FROM attachment WHERE work_item_id = @itemId AND name = @name AND archived_at IS NULL",
            new { itemId = SeedIds.WorkItems.Ship101, name = AttachmentName });
    }

    [TestCaseSource(nameof(SweptRoutes))]
    public async Task Route_WhenOpenedOnAPhone_ShouldFollowTheTouchRules(string screenId)
    {
        await TouchRoutes.CheckAsync(Page, screenId, _markdownAttachment);
    }

    [Test]
    public void TouchRoutes_ShouldCoverEveryScreen()
    {
        TouchRoutes.All.Keys.ShouldBe(ScreenIds.All, ignoreOrder: true);
    }

    [Test]
    public void TouchPendingRoutes_ShouldNameKnownScreensAndAStory()
    {
        foreach (var (screenId, story) in TouchPendingRoutes.All)
        {
            ScreenIds.All.ShouldContain(screenId, $"TouchPendingRoutes lists {screenId}, which is not in ScreenIds.All");
            StoryKey().IsMatch(story).ShouldBeTrue($"TouchPendingRoutes gives {screenId} the owner '{story}', not a story key");
        }
    }

    [Test]
    public void TouchExemptRoutes_ShouldNameKnownScreensNotPendingWithAReason()
    {
        foreach (var (screenId, reason) in TouchExemptRoutes.All)
        {
            ScreenIds.All.ShouldContain(screenId, $"TouchExemptRoutes lists {screenId}, which is not in ScreenIds.All");
            TouchPendingRoutes.All.ShouldNotContainKey(screenId, $"{screenId} is both exempt and pending");
            reason.ShouldNotBeNullOrWhiteSpace($"TouchExemptRoutes gives {screenId} no reason");
        }
    }

    [GeneratedRegex(@"^SHIP-\d+$")]
    private static partial Regex StoryKey();
}
