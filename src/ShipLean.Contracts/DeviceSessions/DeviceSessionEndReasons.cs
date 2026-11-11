namespace ShipLean.Contracts.DeviceSessions;

// The values of UsageProps.Reason on device_session.ended.
public static class DeviceSessionEndReasons
{
    public const string AppSignOut = "app_sign_out";
    public const string WebSignOut = "web_sign_out";
    public const string Expired = "expired";
    public const string StampChanged = "stamp_changed";
    public const string UserDisabled = "user_disabled";
}
