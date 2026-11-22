using Microsoft.Extensions.DependencyInjection;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.Contracts;
using ShipLean.Contracts.Analytics;
using ShipLean.TestSupport.TestIds;
using ShipLean.TestSupport.WebVitals;

namespace ShipLean.AcceptanceTests.Lanes.Shell;

// Web vitals from the real SPA to the real endpoint: Joel, an instance admin, turns the switch on or off through the
// real settings API, the browser measures with web-vitals, and leaving the page (about:blank hides it) sends the readings.
// The test host's IWebVitalsQueue is RecordingWebVitals, so the samples the endpoint accepted are asserted there.
[TestFixture]
public class WebVitalsTests : SeededAcceptanceTest
{
    private const string BacklogPath = "/p/SHIP/backlogs/stories";
    private const string WorkItemPath = "/p/SHIP/items/SHIP-124";
    private static readonly TimeSpan SampleTimeout = TimeSpan.FromSeconds(20);

    private RecordingWebVitals Recorder => (RecordingWebVitals)Host.Services.GetRequiredService<IWebVitalsQueue>();

    [Test]
    public async Task WebVitals_WhenSwitchOn_ShouldReportTheFirstScreen()
    {
        await SetSwitchAsync(true);

        await OpenMeasuredAsync(BacklogPath, PagesTestIds.BacklogPage);
        await Page.GotoAsync("about:blank");

        var sample = await Recorder.WaitForAsync(s => s.Screen == "storiesBacklog" && s.Navigation == WebVitalsNavigation.Hard && s.Lcp is not null, SampleTimeout);
        sample.Lcp!.Value.ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task WebVitals_WhenMovingToAnotherScreen_ShouldReportItAsSoft()
    {
        await SetSwitchAsync(true);

        await OpenMeasuredAsync(BacklogPath, PagesTestIds.BacklogPage);
        await Page.GetByTestId(ShellTestIds.NavSprints).ClickAsync();
        await Expect(Page.GetByTestId(PagesTestIds.SprintsPage)).ToBeVisibleAsync();
        await Page.GotoAsync("about:blank");

        var sample = await Recorder.WaitForAsync(s => s.Screen == "sprints" && s.Navigation == WebVitalsNavigation.Soft, SampleTimeout);
        sample.Ttfb.ShouldBeNull();
    }

    [Test]
    public async Task WebVitals_WhenSwitchOff_ShouldLoadNothingAndSendNothing()
    {
        await SetSwitchAsync(false);
        List<string> chunks = [];
        void Watch(object? sender, IRequest request)
        {
            if (IsReporterChunk(request.Url))
            {
                chunks.Add(request.Url);
            }
        }
        Page.Context.Request += Watch;
        try
        {
            var config = await Page.RunAndWaitForResponseAsync(
                () => Page.GotoAsync(BacklogPath),
                r => r.Url.EndsWith("/api/usage/web-vitals/config", StringComparison.Ordinal));
            (await config.TextAsync()).ShouldContain("\"enabled\":false");
            await Expect(Page.GetByTestId(PagesTestIds.BacklogPage)).ToBeVisibleAsync();
            await Page.GetByTestId(ShellTestIds.NavSprints).ClickAsync();
            await Expect(Page.GetByTestId(PagesTestIds.SprintsPage)).ToBeVisibleAsync();
            await Page.GotoAsync("about:blank");
        }
        finally
        {
            Page.Context.Request -= Watch;
        }

        // Only the reporter chunk sends readings, so no chunk means nothing can be sent. Playwright does not report the
        // keepalive POST a page sends while it unloads, so the sending side is checked on the server.
        chunks.ShouldBeEmpty();
        Recorder.Samples.ShouldBeEmpty();
    }

    [Test]
    public async Task WebVitals_WhenSwitchOn_ShouldNotSendTheRealUrl()
    {
        await SetSwitchAsync(true);

        await OpenMeasuredAsync(WorkItemPath, PagesTestIds.WorkItemPage);
        await Page.GotoAsync("about:blank");

        var sample = await Recorder.WaitForAsync(s => s.Screen == "workItem", SampleTimeout);
        sample.Screen.ShouldBe("workItem");
        Recorder.Samples.ShouldAllBe(s => !s.Screen.Contains("SHIP-") && !s.Screen.Contains('/'));
    }

    // Opens a page and waits until the reporter's chunk has loaded, so its observers are in place before the test leaves.
    private async Task OpenMeasuredAsync(string path, string pageTestId)
    {
        var chunk = await Page.RunAndWaitForResponseAsync(() => Page.GotoAsync(path), r => IsReporterChunk(r.Url));
        chunk.Ok.ShouldBeTrue();
        await Expect(Page.GetByTestId(pageTestId)).ToBeVisibleAsync();
    }

    private async Task SetSwitchAsync(bool on)
    {
        var response = await Page.Context.APIRequest.PutAsync("/api/instance/analytics", new APIRequestContextOptions
        {
            DataObject = new { provider = "umami", host = "https://umami.example.com", siteKey = "7f3c1a52-9d4e-4b8a-a1c6-2e5f0b9d3c71", sendNames = false, sendWebVitals = on },
            Headers = new Dictionary<string, string> { [ClientHeader.Name] = "web-vitals-test" },
        });
        response.Ok.ShouldBeTrue(await response.TextAsync());
        Recorder.Clear();
    }

    private static bool IsReporterChunk(string url) => url.Contains("/assets/startReporter-", StringComparison.Ordinal);
}
