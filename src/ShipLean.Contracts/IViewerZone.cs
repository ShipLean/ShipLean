namespace ShipLean.Contracts;

// The time zone of the person the request acts as: the one place the viewer's zone comes from. Every label built for a
// person passes Zone to a zone-taking ShipLean.Core.DisplayDate format; nothing reads the host's zone. A singleton like
// ICurrentUser: it reads the request's user at call time.
public interface IViewerZone
{
    // The user's IANA id ("Asia/Bangkok"); null when the user has none, the stored id is not a zone this host knows, or
    // there is no request user.
    string? Id { get; }

    // The user's zone, else UTC.
    TimeZoneInfo Zone { get; }
}
