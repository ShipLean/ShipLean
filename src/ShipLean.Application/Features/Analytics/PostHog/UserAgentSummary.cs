namespace ShipLean.Application.Features.Analytics.PostHog;

// The browser, operating system and device class PostHog's Web vitals tab groups by, read from a user agent string.
// A value that is not one of the known names is null, so the property is left out rather than guessed.
public sealed record UserAgentSummary(string? Browser, string? Os, string DeviceType)
{
    public static UserAgentSummary Parse(string userAgent)
    {
        return new UserAgentSummary(BrowserOf(userAgent), OsOf(userAgent), DeviceTypeOf(userAgent));
    }

    private static string? BrowserOf(string ua)
    {
        // Edge, Opera and the iOS wrappers also say Chrome or Safari, so the more specific names go first.
        if (ua.Contains("Edg/") || ua.Contains("EdgA/") || ua.Contains("EdgiOS/"))
        {
            return "Microsoft Edge";
        }
        if (ua.Contains("OPR/") || ua.Contains("Opera"))
        {
            return null;
        }
        if (ua.Contains("Firefox/") || ua.Contains("FxiOS/"))
        {
            return "Firefox";
        }
        if (ua.Contains("Chrome/") || ua.Contains("CriOS/"))
        {
            return "Chrome";
        }
        return ua.Contains("Safari/") && ua.Contains("Version/") ? "Safari" : null;
    }

    private static string? OsOf(string ua)
    {
        if (ua.Contains("iPhone") || ua.Contains("iPad") || ua.Contains("iPod"))
        {
            return "iOS";
        }
        if (ua.Contains("Android"))
        {
            return "Android";
        }
        if (ua.Contains("Windows"))
        {
            return "Windows";
        }
        if (ua.Contains("Macintosh") || ua.Contains("Mac OS X"))
        {
            return "Mac OS X";
        }
        return ua.Contains("Linux") ? "Linux" : null;
    }

    private static string DeviceTypeOf(string ua)
    {
        if (ua.Contains("iPad") || (ua.Contains("Android") && !ua.Contains("Mobile")))
        {
            return "Tablet";
        }
        return ua.Contains("Mobile") || ua.Contains("iPhone") || ua.Contains("iPod") ? "Mobile" : "Desktop";
    }
}
