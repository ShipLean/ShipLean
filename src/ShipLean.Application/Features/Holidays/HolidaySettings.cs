using Microsoft.Extensions.Configuration;

namespace ShipLean.Application.Features.Holidays;

// SHIPLEAN_HOLIDAYS_API_URL: the Nager.Date API base address. Unset means the public one; set to an empty value it turns the
// feed off (an instance with no internet, or a test). Read when asked, not at startup, so test overrides count.
public sealed class HolidaySettings(IConfiguration configuration)
{
    public const string UrlKey = "SHIPLEAN_HOLIDAYS_API_URL";
    public const string DefaultHost = "date.nager.at";

    // Null when the feed is off or the setting is not an absolute http(s) address.
    public Uri? BaseUrl
    {
        get
        {
            var value = (configuration[UrlKey] ?? $"https://{DefaultHost}/api/v3").Trim();
            return Uri.TryCreate(value, UriKind.Absolute, out var url) && url.Scheme is "http" or "https" ? url : null;
        }
    }
}
