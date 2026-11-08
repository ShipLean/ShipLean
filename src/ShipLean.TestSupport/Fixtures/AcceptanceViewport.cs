namespace ShipLean.TestSupport.Fixtures;

// The browser context an acceptance class runs in. A class opts into the phone by overriding AcceptanceFixture.Viewport.
public sealed record AcceptanceViewport(int Width, int Height, bool HasTouch, bool IsMobile, float DeviceScaleFactor)
{
    // Today's desktop context: Playwright's defaults at 1280×800.
    public static readonly AcceptanceViewport Desktop = new(1280, 800, false, false, 1);

    // A phone at 390×844 with touch, at scale 1 so screenshots and hit areas read in CSS pixels.
    public static readonly AcceptanceViewport Phone = new(390, 844, true, true, 1);
}
