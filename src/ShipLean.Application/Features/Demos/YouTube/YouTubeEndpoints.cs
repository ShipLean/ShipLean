namespace ShipLean.Application.Features.Demos.YouTube;

// Google's addresses. A test overrides them with services.Configure<YouTubeEndpoints>; there is no setting or variable for them.
public sealed class YouTubeEndpoints
{
    public string Authorization { get; set; } = "https://accounts.google.com/o/oauth2/v2/auth";

    public string Token { get; set; } = "https://oauth2.googleapis.com/token";

    public string Revoke { get; set; } = "https://oauth2.googleapis.com/revoke";

    public string Api { get; set; } = "https://www.googleapis.com/youtube/v3";

    public string Upload { get; set; } = "https://www.googleapis.com/upload/youtube/v3";
}
