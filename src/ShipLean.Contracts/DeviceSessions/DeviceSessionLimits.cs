namespace ShipLean.Contracts.DeviceSessions;

// The numbers every IDeviceSessions and the endpoints share.
public static class DeviceSessionLimits
{
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(90);
    public static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(1);
    public const int NameMaxLength = 100;
}
