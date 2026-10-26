using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Realtime;

// The top bar's connection dot.
public sealed class LiveStatus(IPage page)
{
    public ILocator Dot => page.GetByTestId(RealtimeTestIds.LiveDot);

    public async Task WaitUntilLiveAsync() =>
        await Assertions.Expect(Dot).ToHaveAttributeAsync("data-state", "live");
}
