using ShipLean.Contracts.DeviceSessions;

namespace ShipLean.TestSupport.DeviceSessions;

// A device session made for a test and the client that sends its secret.
public sealed record DeviceBearerSession(HttpClient Client, DeviceSignIn SignIn) : IDisposable
{
    public Guid Id => SignIn.Session.Id;

    public void Dispose() => Client.Dispose();
}
