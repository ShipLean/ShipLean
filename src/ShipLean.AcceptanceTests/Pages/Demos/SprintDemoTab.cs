using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Demos;

// A sprint's Demo tab: the status band and its Create button, the player, the chapters, the features, the script and the YouTube notice.
public sealed class SprintDemoTab(IPage page)
{
    public ILocator Tab => page.GetByTestId(DemosTestIds.SprintHeaderTabDemo);

    public ILocator Panel => page.GetByTestId(DemosTestIds.DemoPanel);

    public ILocator StatusBand => page.GetByTestId(DemosTestIds.DemoStatusBand);

    public ILocator StatusMessage => page.GetByTestId(DemosTestIds.DemoStatusMessage);

    public ILocator Create => page.GetByTestId(DemosTestIds.DemoCreate);

    public ILocator CreateBlocked => page.GetByTestId(DemosTestIds.DemoCreateBlocked);

    public ILocator ItemLink => page.GetByTestId(DemosTestIds.DemoItemLink);

    public ILocator Player => page.GetByTestId(DemosTestIds.DemoPlayer);

    public ILocator Video => page.GetByTestId(DemosTestIds.DemoVideo);

    public ILocator Embed => page.GetByTestId(DemosTestIds.DemoEmbed);

    public ILocator WatchOnYouTube => page.GetByTestId(DemosTestIds.DemoWatchOnYoutube);

    public ILocator PrivateNote => page.GetByTestId(DemosTestIds.DemoPrivateNote);

    public ILocator ChapterList => page.GetByTestId(DemosTestIds.DemoChapters);

    public ILocator Chapters => page.GetByTestId(DemosTestIds.DemoChapter);

    public ILocator FeatureList => page.GetByTestId(DemosTestIds.DemoFeatures);

    public ILocator Features => page.GetByTestId(DemosTestIds.DemoFeature);

    public ILocator Script => page.GetByTestId(DemosTestIds.DemoScript);

    public ILocator YouTubeNotice => page.GetByTestId(DemosTestIds.DemoYoutubeNotice);

    public ILocator YouTubeRetry => page.GetByTestId(DemosTestIds.DemoYoutubeRetry);

    public async Task GotoAsync(string projectKey, Guid sprintId)
    {
        await page.GotoAsync(Links.SprintDemo(projectKey, sprintId));
        await Panel.WaitForAsync();
    }

    public Task CreateDemoAsync() => Create.ClickAsync();

    // Clicks the chapter at the zero-based index: the player seeks to its start.
    public Task SeekAsync(int chapterIndex) => Chapters.Nth(chapterIndex).ClickAsync();

    public Task<double> CurrentTimeAsync() => Video.EvaluateAsync<double>("v => v.currentTime");

    // Waits for the video to sit within half a second of the given time with no seek pending (a media state, not a pause).
    public Task WaitForSeekedAsync(double seconds) =>
        Video.EvaluateAsync(
            "(v, s) => new Promise(resolve => { const done = () => !v.seeking && Math.abs(v.currentTime - s) < 0.5; if (done()) { resolve(); return; } const check = () => { if (done()) { v.removeEventListener('seeked', check); resolve(); } }; v.addEventListener('seeked', check); })",
            seconds);

    public Task RetryYouTubeAsync() => YouTubeRetry.ClickAsync();
}
