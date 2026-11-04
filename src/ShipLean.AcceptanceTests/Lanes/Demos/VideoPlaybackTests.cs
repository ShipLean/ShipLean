using System.Text.Json;
using Microsoft.Playwright;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.Contracts;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Demos;

// An MP4 attachment really plays and seeks in Chromium on the app's own origin, served as an attachment with the safe headers.
// A video that can't answer a ranged request has nothing seekable, and a seek lands back at 0.
[TestFixture]
public class VideoPlaybackTests : SeededAcceptanceTest
{
    private const string PlayAndSeek = """
        async (url) => {
          const video = document.createElement('video');
          video.muted = true;
          video.preload = 'auto';
          document.body.appendChild(video);
          const once = (name) => new Promise((resolve) => video.addEventListener(name, resolve, { once: true }));
          const canPlay = once('canplay');
          video.src = url;
          await canPlay;
          const seekableEnd = video.seekable.length > 0 ? video.seekable.end(0) : 0;
          await video.play();
          while (video.currentTime < 0.3) {
            await once('timeupdate');
          }
          const playedTo = video.currentTime;
          video.pause();
          const seeked = once('seeked');
          video.currentTime = 6;
          await seeked;
          return { seekableEnd, playedTo, seekedTo: video.currentTime, duration: video.duration };
        }
        """;

    [Test]
    public async Task Video_WhenPlayedAndSeeked_ShouldServeRanges()
    {
        var added = await UploadVideoAsync();
        var media = new List<(int Status, string Range)>();
        Page.Response += (_, response) =>
        {
            if (new Uri(response.Url).AbsolutePath == added.GetProperty("downloadUrl").GetString())
            {
                media.Add((response.Status, response.Request.Headers.GetValueOrDefault("range") ?? string.Empty));
            }
        };
        await Page.GotoAsync("/");

        var result = await Page.EvaluateAsync<JsonElement>(PlayAndSeek, added.GetProperty("downloadUrl").GetString());

        result.GetProperty("seekableEnd").GetDouble().ShouldBeInRange(7.0, 9.0);
        result.GetProperty("playedTo").GetDouble().ShouldBeGreaterThan(0.2);
        result.GetProperty("seekedTo").GetDouble().ShouldBeInRange(5.5, 6.5);
        media.ShouldNotBeEmpty();
        media.ShouldContain(m => m.Status == 206 && m.Range.StartsWith("bytes=", StringComparison.Ordinal));
        media.ShouldNotContain(m => m.Status != 206 && m.Status != 200);
    }

    private async Task<JsonElement> UploadVideoAsync()
    {
        var bytes = await File.ReadAllBytesAsync(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DemoFixtures", "demo-fixture.mp4"));
        var response = await Page.APIRequest.PostAsync($"/api/work-items/{SeedIds.WorkItems.Ship90}/attachments", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { [ClientHeader.Name] = "acceptance-test" },
            Multipart = Page.APIRequest.CreateFormData().Set("file", new FilePayload { Name = "demo-fixture.mp4", MimeType = "video/mp4", Buffer = bytes }),
        });
        response.Ok.ShouldBeTrue(await response.TextAsync());
        return await response.JsonAsync() ?? throw new InvalidOperationException("The upload answered no body.");
    }
}
